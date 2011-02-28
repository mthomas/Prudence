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
using System.Threading;
using System.Threading.Tasks;
using Lucene.Net.Analysis;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Directory = System.IO.Directory;
using LuceneDirectory = Lucene.Net.Store.Directory;

namespace Prudence
{
    public class LogIndexer : ApplicationComponent
    {
        private readonly List<Task> _outstandingTasks = new List<Task>();
        private IndexWriter _indexWriter;

        private bool _stopped;
        private Thread _waitingForFilesInProcessingDirectory;

        public override void Start()
        {
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            AcquireLock();

            EnsureDirectoriesExist();

            OpenIndexWriter();

            _waitingForFilesInProcessingDirectory = new Thread(WaitForFilesInProcessingDirectory);

            _waitingForFilesInProcessingDirectory.Start();
        }


        public override void Stop()
        {
            _stopped = true;

            try
            {
                _waitingForFilesInProcessingDirectory.Join();

                lock (_outstandingTasks)
                {
                    foreach (var task in _outstandingTasks)
                    {
                        task.Wait();
                    }
                }

                Console.WriteLine("Closing index writer...");

                _indexWriter.Close();
            }
            finally
            {
                ReleaseLock();
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();

            //TODO this should be so much better

            Console.WriteLine("Got unobserved exception, stopping execution.");

            Console.WriteLine(e.ToString());

            try
            {
                Stop();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to stop cleanly.", ex);
            }

            throw new Exception("Internal error", e.Exception);
        }

        private void OpenIndexWriter()
        {
            var dir = FSDirectory.Open(new DirectoryInfo(Config.IndexPath));

            //create an analyzer to process the text
            var analyzer = new SimpleAnalyzer();

            //create the index writer with the directory and analyzer defined.
            _indexWriter = new IndexWriter(dir, analyzer, true, IndexWriter.MaxFieldLength.UNLIMITED);
            _indexWriter.SetMaxBufferedDocs(IndexWriter.DISABLE_AUTO_FLUSH);
        }

        private void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(Config.RootPath))
            {
                throw new Exception("Cannot access root directory " + Config.RootPath);
            }

            var dirs = new[]
                           {
                               Config.Indexer.IncomingPath, Config.Indexer.ProcessingPath, Config.Indexer.ProcessedPath,
                               Config.IndexPath
                           };

            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
        }

        private void WaitForFilesInProcessingDirectory()
        {
            while (!_stopped)
            {
                //TODO error handling
                var file = Directory.EnumerateFiles(Config.Indexer.IncomingPath).FirstOrDefault();

                if (file != null)
                {
                    AcquireFile(file);
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }


        private void AcquireFile(string file)
        {
            //TODO error handling
            var dest = Path.Combine(Config.Indexer.ProcessingPath, MakeUnique(Path.GetFileName(file)));

            if (TryMove(file, dest))
            {
                var task = new Task(() => ProcessFile(dest));

                TrackTask(task);

                task.Start();
            }
        }

        //TODO refactor to general path helpers
        private string MakeUnique(string fileName)
        {
            return
                fileName +
                Guid.NewGuid() +
                ".log";
        }

        private void TrackTask(Task task)
        {
            lock (_outstandingTasks)
            {
                var doneTasks = _outstandingTasks.Where(t => t.IsCompleted).ToList();
                _outstandingTasks.RemoveAll(doneTasks.Contains);
                _outstandingTasks.Add(task);

                //TODO handle faulted tasks
            }
        }

        private void ProcessFile(string dest)
        {
            ParseFile(dest);

            _indexWriter.Commit();

            //TODO error handling
            //TODO use file system abstraction
            File.Move(dest, Path.Combine(Config.Indexer.ProcessedPath, Path.GetFileName(dest)));
        }

        //TODO move to file system abstraction
        private static bool TryMove(string file, string dest)
        {
            try
            {
                File.Move(file, dest);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void AcquireLock()
        {
            try
            {
                //TODO use file system abstraction
                using (var fs = new FileStream(Config.Indexer.LockFilePath, FileMode.CreateNew))
                {
                    using (var writer = new StreamWriter(fs))
                    {
                        writer.WriteLine(DateTime.Now.ToString());
                    }
                }
            }
            catch (IOException ex)
            {
                throw new Exception("Unable to acquire lock.", ex);
            }
        }

        private void ReleaseLock()
        {
            //TODO error handling
            //TODO use file system abstraction
            File.Delete(Config.Indexer.LockFilePath);
        }


        private void ParseFile(string path)
        {
            Console.WriteLine("Parsing: " + path);

            var file = Path.GetFileName(path);

            var parser = new LogParser();

            var entries = parser.Parse(EnumerateLines(path), file, file);

            foreach (var entry in entries)
            {
                _indexWriter.AddDocument(entry.ToDocument());
            }
        }

        //TODO move to file system abstractions
        private IEnumerable<string> EnumerateLines(string path)
        {
            using (var file = File.OpenText(path))
            {
                string line;

                while ((line = file.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }
    }
}