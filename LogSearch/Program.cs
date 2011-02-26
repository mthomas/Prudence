using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Search;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;

using Directory = System.IO.Directory;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace LogSearch
{
    class Program
    {
        static string RootDir = @"c:\LogSearch\data\";
        static string IncomingDir = RootDir + @"Incoming\";
        static string ProcessingDir = RootDir + @"Processing\";
        static string ProcessedDir = RootDir + @"Processed\";
        public static string IndexDir = RootDir + @"Index\";

        static string LockFile = RootDir + "lockfile.lock";

        static Lucene.Net.Index.IndexWriter indexWriter;

        static bool stopped = false;


        static void Main(string[] args)
        {         
            var sw = Stopwatch.StartNew();

            Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);

            TaskScheduler.UnobservedTaskException += new EventHandler<UnobservedTaskExceptionEventArgs>(TaskScheduler_UnobservedTaskException);

            AcquireLock();
            try
            {
                EnsureDirectoriesExist();

                OpenIndexWriter();

                WaitForFilesInProcessingDirectory();

                Console.WriteLine("Waiting for pending tasks to complete...");

                lock (outstandingTasks)
                {
                    foreach (var task in outstandingTasks)
                    {
                        task.Wait();
                    }
                }
                
                Console.WriteLine("Closing index writer...");

                indexWriter.Close();
            }
            finally
            {
                Console.WriteLine("Releasing lock...");
                ReleaseLock();

                sw.Stop();

                Console.WriteLine("Complete in {0}ms", sw.ElapsedMilliseconds);
            }
        }

        static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();

            //TODO this should be so much better

            Console.WriteLine("Got unobserved exception, stopping execution.");

            Console.WriteLine(e.ToString());

            stopped = true;
        }

        static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
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

            var dirs = new[] { IncomingDir, ProcessingDir, ProcessedDir, IndexDir };

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
            string dest = Path.Combine(ProcessingDir, MakeUnique(Path.GetFileName(file)));

            if (TryMove(file, dest))
            {
                Task task = new Task(() =>
                {
                    ProcessFile(dest);
                });

                TrackTask(task);

                task.Start();
            }
        }

        private static string MakeUnique(string fileName)
        {
            return
                fileName +
                Guid.NewGuid().ToString() +
                ".log";
        }

        static List<Task> outstandingTasks = new List<Task>();
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
        private Action _disposer;

        public DisposableAction(Action disposer)
        {
            _disposer = disposer;
        }

        public void Dispose()
        {
            _disposer();
        }
    }
}
