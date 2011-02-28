#region license

// Copyright (c) 2011 Michael Thomas
// 
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Prudence
{
    public class LogForwarder : ApplicationComponent
    {
        private const string PositionFileExtension = ".dat";

        private readonly SHA256 _hasher = SHA256.Create();
        
        private readonly List<string> _lineBuffer = new List<string>();

        private Thread _background;
        private Dictionary<string, long> _filePositions;
        private bool _stopped;

        private string _currentFile;
        private long _currentByteStart;
        private long _currentByteEnd;

        public override void Start()
        {
            LoadFilePositions();

            _background = new Thread(MonitorFiles);

            _background.Start();
        }

        public override void Stop()
        {
            _stopped = true;

            Log.Info("Stopping execution.  Waiting for background process to complete.");

            _background.Join();
        }

        public void MonitorFiles()
        {
            while (!_stopped)
            {
                Log.Debug("Checking for changed log files.");

                foreach (var path in Config.Forwarder.PathsToWatch)
                {
                    Log.DebugFormat("Checking for changed log files in {0}", path);

                    var files = GetMonitoredFiles(path);

                    foreach (var file in files)
                    {
                        ProcessLogFile(file);
                    }
                }

                Thread.Sleep(Config.Forwarder.PollPeriodMiliseconds);
            }
        }

        public IEnumerable<string> GetMonitoredFiles(string path)
        {
            try
            {
                return Directory.EnumerateFiles(path);
            }
            catch(IOException ex)
            {
                Log.Error("Unable to enumerate log files in " + path, ex);
                return new string[] {};
            }
        }

        private void LoadFilePositions()
        {
            _filePositions = new Dictionary<string, long>();

            var paths = Directory.GetFiles(Config.Forwarder.ForwardLogPath, "*" + PositionFileExtension);

            Log.InfoFormat("Loading {0} persisted log file positions.", paths.Length);

            foreach (var path in paths)
            {
                try
                {
                    LoadFilePosition(path);
                }
                catch (Exception ex)
                {
                    Log.Warn(
                        "Error loading log file position from " + path + ".  May result in duplicate log entries.", ex);
                    TryDelete(path);
                }
            }
        }

        private void LoadFilePosition(string path)
        {
            var position = long.Parse(File.ReadAllText(path));
            var fileHash = Path.GetFileNameWithoutExtension(path);

            if (fileHash == null)
            {
                Log.WarnFormat("Unable to parse file hash from file name {0}", path);
            }
            else
            {
                Log.DebugFormat("Setting log file position for {0} to {1}", fileHash, position);
                _filePositions[fileHash] = position;
            }
        }

        private void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException ex)
            {
                Log.Warn("Unable to delete " + path, ex);
            }
        }

        private void ProcessLogFile(string logFilePath)
        {
            _currentFile = logFilePath;

            Log.DebugFormat("Processing log file {0}", logFilePath);

            FileStream stream;

            try
            {
                //this combination seems to allow us to read log files
                //as they are being written to.  don't know if that causes
                //potential issues.  is there a way to snapshot the file 
                //while we do this?
                stream = File.Open(logFilePath, FileMode.Open, FileAccess.Read,
                                   FileShare.Read | FileShare.Write | FileShare.Delete);
            }
            catch (IOException ex)
            {
                Log.Info("Unable to open log file " + logFilePath, ex); //only info level since sometimes rolling log files get moved out from under us
                return;
            }

            using (stream)
            {
                var firstLine = stream.ReadFirstLine();

                var lineHash = GetLogFileHash(firstLine);

                long start = 0;
                long end = stream.Length;

                if (_filePositions.ContainsKey(lineHash))
                {
                    start = _filePositions[lineHash];
                }

                if (start < end)
                {
                    ProcessLogFileFrom(stream, start, end);

                    _filePositions[lineHash] = end;

                    SaveFilePosition(lineHash, end);
                }
            }
        }

        private void ProcessLogFileFrom(FileStream stream, long start, long end)
        {
            _currentByteStart = start;
            _currentByteEnd = end;

            Log.DebugFormat("Processing bytes {0} to {1} of {2}", _currentByteStart, _currentByteEnd, _currentFile);

            stream.Seek(start, SeekOrigin.Begin);

            var reader = new StreamReader(stream);

            using (var buffer = new LineBuffer(Config.Forwarder.TargetPath, GetBaseFileName(), ".log", Config.Forwarder.ChunkSizeInLines))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    buffer.AppendLine(line);
                }
            }
        }

        private string GetBaseFileName()
        {
            return (GetMachineName() + "_" + GetFileName() + "_" + GetByteRange()).ToSafeFilename();
        }

        private string GetByteRange()
        {
            return String.Format("{0}-{1}", _currentByteStart, _currentByteEnd);
        }

        private string GetFileName()
        {
            return _currentFile.Replace('_', '-');
        }

        private string GetMachineName()
        {
            return Environment.MachineName.Replace('_', '-');
        }

        /// <summary>
        /// We hash the first line of a log file to create a unique identifer for this log file.
        /// This allows us to track a specific file across renames.  
        /// </summary>
        /// <param name="firstLine"></param>
        /// <returns></returns>
        private string GetLogFileHash(string firstLine)
        {
            return BitConverter.ToString(_hasher.ComputeHash(Encoding.Default.GetBytes(firstLine)));
        }

        private void SaveFilePosition(string logFileHash, long position)
        {
                File.WriteAllText(Path.Combine(Config.Forwarder.ForwardLogPath, logFileHash + PositionFileExtension),
                                  position.ToString());
        }
    }
}