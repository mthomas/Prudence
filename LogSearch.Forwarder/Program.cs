using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Security.Cryptography;

namespace LogSearch.Forwarder
{

    //TODO ADD PERSISTANCE

    class Program
    {
        static Dictionary<string, long> filePositions { get; set; }

        const string DirectoryToWatch = @"C:\LogSearch\test\";
        const string ForwardLogDirectory = @"C:\LogSearch\data\ForwardLog\";

        const string TargetDireectory = @"C:\LogSearch\data\Incoming\";

        static bool processing = false;
        static bool done = false;
        static void Main(string[] args)
        {
            filePositions = LoadFilePositions();

            System.Threading.Timer timer = new System.Threading.Timer(new TimerCallback(monitorFiles), null, 0, 100);

            Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);

            while (!done)
            {
                Thread.Sleep(100);
            }

            Console.WriteLine("Finishing processing");

            while (processing)
            {
                Thread.Sleep(100);
            }
        }

        private static Dictionary<string, long> LoadFilePositions()
        {
            var filePositions = new Dictionary<string, long>();

            var files = Directory.GetFiles(ForwardLogDirectory, "*.dat");

            foreach (var file in files)
            {
                long position = long.Parse(File.ReadAllText(file));

                filePositions[Path.GetFileNameWithoutExtension(file)] = position;
            }

            return filePositions;
        }

        static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Ctrl+C pressed");

            e.Cancel = true;
            done = true;
        }

        static void monitorFiles(object state)
        {
            lock (rock)
            {
                processing = true;

                var files = Directory.GetFiles(DirectoryToWatch);

                foreach (var file in files)
                {
                    ProcessLogFile(file);
                }

                processing = false;

                PersistFilePositions(filePositions);
            }
        }

        private static void PersistFilePositions(Dictionary<string, long> filePositions)
        {
            foreach (var pair in filePositions)
            {
                File.WriteAllText(Path.Combine(ForwardLogDirectory, pair.Key + ".dat"), pair.Value.ToString());
            }
        }

        static object rock = new object();
        private static void ProcessLogFile(string file)
        {

            var x = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Write | FileShare.Delete);

            var firstLine = GetFirstLine(x);


            var lineHash = Hash(firstLine);

            long start = 0;

            if (filePositions.ContainsKey(lineHash))
            {
                start = filePositions[lineHash];
            }

            if (start < x.Length)
            {

                long end = ProcessLogFileFrom(x, start);
                filePositions[lineHash] = end;

                FlushLineBuffer();
            }

            x.Dispose();

            
        }

        private static long ProcessLogFileFrom(FileStream x, long start)
        {
            x.Seek(start, SeekOrigin.Begin);

            var reader = new StreamReader(x);
            
                string line = null;
                while ((line = reader.ReadLine()) != null)
                {
                    ProcessLogFileLine(line);
                }
            

            return x.Length;
        }

        static List<string> lineBuffer = new List<string>();

        private static void ProcessLogFileLine(string line)
        {
            lineBuffer.Add(line);

            if (lineBuffer.Count > 1000)
            {
                FlushLineBuffer();
            }
        }

        private static void FlushLineBuffer()
        {

            File.WriteAllLines(Path.Combine(TargetDireectory, Guid.NewGuid().ToString() + ".log"), lineBuffer);

            lineBuffer.Clear();
        }

        static SHA256 hasher = System.Security.Cryptography.SHA256CryptoServiceProvider.Create();

        private static string Hash(string firstLine)
        {
            return BitConverter.ToString(hasher.ComputeHash(Encoding.Default.GetBytes(firstLine)));
        }

        private static string GetFirstLine(FileStream x)
        {
            var reader = new StreamReader(x);
            
                return reader.ReadLine();
            
        }
    }
}
