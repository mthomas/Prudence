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
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Prudence
{
    public class LogForwarder : ApplicationComponent
    {
        private const string PositionFileExtension = ".dat";
        private readonly SHA256 _hasher = SHA256.Create();
        private readonly HashSet<char> _invalidFileNameChars = new HashSet<char>(Path.GetInvalidFileNameChars());
        private readonly List<string> _lineBuffer = new List<string>();

        private Thread _background;
        private Dictionary<string, long> _filePositions;
        private bool _stopped;

        public override void Start()
        {
            LoadFilePositions();

            _background = new Thread(() =>
                                         {
                                             while (!_stopped)
                                             {
                                                 MonitorFiles(null);

                                                 Thread.Sleep(Config.Forwarder.PollPeriodMiliseconds);
                                             }
                                         });

            _background.Start();
        }

        private void LoadFilePositions()
        {
            _filePositions = new Dictionary<string, long>();

            var paths = Directory.GetFiles(Config.Forwarder.ForwardLogPath, "*" + PositionFileExtension);

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

            _filePositions[Path.GetFileNameWithoutExtension(path)] = position;
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

        public override void Stop()
        {
            _stopped = true;

            _background.Join();
        }

        public void MonitorFiles(object state)
        {
            foreach (var path in Config.Forwarder.PathsToWatch)
            {
                var files = Directory.GetFiles(path);

                foreach (var file in files)
                {
                    ProcessLogFile(file);
                }
            }

            SaveFilePositions();
        }

        private void ProcessLogFile(string logFilePath)
        {
            FileStream stream;

            try
            {
                stream = File.Open(logFilePath, FileMode.Open, FileAccess.Read,
                                   FileShare.Read | FileShare.Write | FileShare.Delete);
            }
            catch (IOException ex)
            {
                Log.Info("Unable to open file " + logFilePath, ex);
                return;
            }

            using (stream)
            {
                var firstLine = GetFirstLine(stream);


                var lineHash = Hash(firstLine);

                long start = 0;

                if (_filePositions.ContainsKey(lineHash))
                {
                    start = _filePositions[lineHash];
                }

                if (start < stream.Length)
                {
                    var end = ProcessLogFileFrom(stream, start);

                    _filePositions[lineHash] = end;

                    FlushLineBuffer();
                }
            }
        }

        private long ProcessLogFileFrom(FileStream stream, long start)
        {
            Log.InfoFormat("Processing bytes {0} to {1} of {2}", start, stream.Length, "UNKNOWN");

            stream.Seek(start, SeekOrigin.Begin);

            var reader = new StreamReader(stream);

            string line;

            while ((line = reader.ReadLine()) != null)
            {
                ProcessLogFileLine(line);
            }


            return stream.Length;
        }

        private void ProcessLogFileLine(string line)
        {
            _lineBuffer.Add(line);

            if (_lineBuffer.Count > 1000)
            {
                FlushLineBuffer();
            }
        }

        private void FlushLineBuffer()
        {
            File.WriteAllLines(
                Path.Combine(Config.Forwarder.TargetPath,
                             MakeFileNameSafe(GetMachineName() + "|" + GetFileName() + "|" + GetByteRange() + "|" +
                                              Guid.NewGuid()) +
                             ".log"), _lineBuffer);

            _lineBuffer.Clear();
        }

        private string MakeFileNameSafe(string fileName)
        {
            return fileName.Where(c => !_invalidFileNameChars.Contains(c)).Aggregate("", (s, c) => s += c);
        }

        private string GetByteRange()
        {
            return "unknown-unknown";
        }

        private string GetFileName()
        {
            return "UNKNOWN";
        }

        private string GetMachineName()
        {
            return Environment.MachineName;
        }

        private string Hash(string firstLine)
        {
            return BitConverter.ToString(_hasher.ComputeHash(Encoding.Default.GetBytes(firstLine)));
        }

        private string GetFirstLine(FileStream stream)
        {
            var reader = new StreamReader(stream);

            return reader.ReadLine();
        }

        private void SaveFilePositions()
        {
            foreach (var pair in _filePositions)
            {
                File.WriteAllText(Path.Combine(Config.Forwarder.ForwardLogPath, pair.Key + PositionFileExtension),
                                  pair.Value.ToString());
            }
        }
    }
}