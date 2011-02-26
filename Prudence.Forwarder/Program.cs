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
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Prudence.Forwarder
{
    //TODO ADD PERSISTANCE

    internal class Program
    {
        private const string DirectoryToWatch = @"C:\Prudence\test\";
        private const string ForwardLogDirectory = @"C:\Prudence\data\ForwardLog\";

        private const string TargetDireectory = @"C:\Prudence\data\Incoming\";

        private static bool processing;
        private static bool done;
        private static readonly object rock = new object();
        private static readonly List<string> lineBuffer = new List<string>();
        private static readonly SHA256 hasher = SHA256.Create();
        private static Dictionary<string, long> filePositions { get; set; }

        private static void Main(string[] args)
        {
            filePositions = LoadFilePositions();

            var timer = new Timer(monitorFiles, null, 0, 100);

            Console.CancelKeyPress += Console_CancelKeyPress;

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
                var position = long.Parse(File.ReadAllText(file));

                filePositions[Path.GetFileNameWithoutExtension(file)] = position;
            }

            return filePositions;
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Ctrl+C pressed");

            e.Cancel = true;
            done = true;
        }

        private static void monitorFiles(object state)
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

        private static void ProcessLogFile(string file)
        {
            var x = File.Open(file, FileMode.Open, FileAccess.Read,
                              FileShare.Read | FileShare.Write | FileShare.Delete);

            var firstLine = GetFirstLine(x);


            var lineHash = Hash(firstLine);

            long start = 0;

            if (filePositions.ContainsKey(lineHash))
            {
                start = filePositions[lineHash];
            }

            if (start < x.Length)
            {
                var end = ProcessLogFileFrom(x, start);
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
            File.WriteAllLines(Path.Combine(TargetDireectory, Guid.NewGuid() + ".log"), lineBuffer);

            lineBuffer.Clear();
        }

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