#region license

// //Copyright (c) 2011 Michael Thomas
// //
// //Permission is hereby granted, free of charge, to any person obtaining
// //a copy of this software and associated documentation files (the
// //"Software"), to deal in the Software without restriction, including
// //without limitation the rights to use, copy, modify, merge, publish,
// //distribute, sublicense, and/or sell copies of the Software, and to
// //permit persons to whom the Software is furnished to do so, subject to
// //the following conditions:
// //
// //The above copyright notice and this permission notice shall be
// //included in all copies or substantial portions of the Software.
// //
// //THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// //EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// //MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// //NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// //LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// //OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// //WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lucene.Net.Analysis;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Directory = System.IO.Directory;

namespace Prudence
{
    internal class Program
    {
        private static string RootDir = @"C:\PrudenceInstallation\data\";
        private static readonly string IncomingDir = RootDir + @"Incoming\";
        private static readonly string ProcessingDir = RootDir + @"Processing\";
        private static readonly string ProcessedDir = RootDir + @"Processed\";
        public static string IndexDir = RootDir + @"Index\";

        private static readonly string LockFile = RootDir + "lockfile.lock";

        private static IndexWriter indexWriter;

        private static bool stopped;
        private static readonly List<Task> outstandingTasks = new List<Task>();


        private static void Main(string[] args)
        {
            var sw = Stopwatch.StartNew();

            Console.CancelKeyPress += Console_CancelKeyPress;

           
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();

            //TODO this should be so much better

            Console.WriteLine("Got unobserved exception, stopping execution.");

            Console.WriteLine(e.ToString());

            stopped = true;
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            Console.WriteLine("Stopping...");
            stopped = true;
        }

        private static void OpenIndexWriter()
        {
            var dir = FSDirectory.Open(new DirectoryInfo(IndexDir));

            //create an analyzer to process the text
            //var analyzer = new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_29);
            var analyzer = new SimpleAnalyzer();

            //create the index writer with the directory and analyzer defined.
            indexWriter = new IndexWriter(dir, analyzer, true, IndexWriter.MaxFieldLength.UNLIMITED);
            indexWriter.SetMaxBufferedDocs(IndexWriter.DISABLE_AUTO_FLUSH);
            //indexWriter.SetRAMBufferSizeMB(1024);
        }

        private static void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(RootDir))
            {
                throw new Exception("Cannot access root directory " + RootDir);
            }

            var dirs = new[] {IncomingDir, ProcessingDir, ProcessedDir, IndexDir};

            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
        }

        private static void WaitForFilesInProcessingDirectory()
        {
            while (!stopped)
            {
                var file = Directory.EnumerateFiles(IncomingDir).FirstOrDefault();

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


        private static void AcquireFile(string file)
        {
            var dest = Path.Combine(ProcessingDir, MakeUnique(Path.GetFileName(file)));

            if (TryMove(file, dest))
            {
                var task = new Task(() => { ProcessFile(dest); });

                TrackTask(task);

                task.Start();
            }
        }

        private static string MakeUnique(string fileName)
        {
            return
                fileName +
                Guid.NewGuid() +
                ".log";
        }

        private static void TrackTask(Task task)
        {
            lock (outstandingTasks)
            {
                var doneTasks = outstandingTasks.Where(t => t.IsCompleted).ToList();
                outstandingTasks.RemoveAll(t => doneTasks.Contains(t));
                outstandingTasks.Add(task);

                //TODO handle faulted tasks
            }
        }

        private static void ProcessFile(string dest)
        {
            ParseFile(dest);

            indexWriter.Commit();

            File.Move(dest, Path.Combine(ProcessedDir, Path.GetFileName(dest)));
        }

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

        private static void AcquireLock()
        {
            try
            {
                using (var fs = new FileStream(LockFile, FileMode.CreateNew))
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

        private static void ReleaseLock()
        {
            File.Delete(LockFile);
        }


        private static void ParseFile(string path)
        {
            Console.WriteLine("Parsing: " + path);

            var file = Path.GetFileName(path);

            var parser = new LogParser();

            var entries = parser.Parse(EnumerateLines(path), file, file);

            foreach (var entry in entries)
            {
                indexWriter.AddDocument(entry.ToDocument());
            }
        }

        private static IEnumerable<string> EnumerateLines(string path)
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

    public class DisposableAction : IDisposable
    {
        private readonly Action _disposer;

        public DisposableAction(Action disposer)
        {
            _disposer = disposer;
        }

        #region IDisposable Members

        public void Dispose()
        {
            _disposer();
        }

        #endregion
    }
}