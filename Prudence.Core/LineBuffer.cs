using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Prudence
{
    public class LineBuffer : IDisposable
    {
        private readonly string _path;
        private readonly string _baseFileName;
        private readonly string _extension;
        private readonly int _chunkSizeInLines;

        private readonly List<string> _lines = new List<string>();

        private int _part = 0;

        public LineBuffer(string path, string  baseFileName, string extension, int chunkSizeInLines)
        {
            _path = path;
            _baseFileName = baseFileName;
            _extension = extension;
            _chunkSizeInLines = chunkSizeInLines;
        }

        public void AppendLine(string line)
        {
            _lines.Add(line);

            if(_lines.Count >= _chunkSizeInLines)
            {
                FlushLines();
            }
        }

        private void FlushLines()
        {
            if (_lines.Count > 0)
            {
                File.WriteAllLines(Path.Combine(_path, _baseFileName + "-pt" + _part + _extension), _lines);
                _part++;
                _lines.Clear();
            }
        }

        public void Dispose()
        {
            FlushLines();
        }
    }
}
