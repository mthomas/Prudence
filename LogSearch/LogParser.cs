using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LogSearch
{
    public class LineParser
    {
        static Dictionary<string, Func<string, DateTime>> _parsers = new Dictionary<string, Func<string, DateTime>>()
        {
            {@"^\d+-\d+-\d+ \d+:\d+:\d+,\d+ ", s => DateTime.Parse(s.ToString().Replace(",", "."))},
            {@"^  \d+:\d+:\d+.\d+ ", s => DateTime.Now}
        };
        
        public static bool IsLineStart(string line)
        {
            return _parsers.Any(p => Regex.IsMatch(line, p.Key));
        }

        public static DateTime? ParseDateTime(string line)
        {
            foreach (var parser in _parsers)
            {
                var match = Regex.Match(line, parser.Key);

                if (match.Success)
                {
                    return parser.Value(match.ToString());
                }
            }

            return null;
        }
    }

    public class LogParser
    {
        public IEnumerable<LogEntry> Parse(IEnumerable<string> lines, string sourceFile, string sourceHost)
        {
            List<string> lineAccumulator = new List<string>();

            foreach (var line in lines)
            {
                if (LineParser.IsLineStart(line))
                {
                    if (lineAccumulator.Count > 0)
                    {
                        yield return CreateLogEntry(lineAccumulator, sourceFile, sourceHost);
                    }

                    lineAccumulator.Clear();
                }

                lineAccumulator.Add(line);
            }

            if (lineAccumulator.Count > 0)
            {
                yield return CreateLogEntry(lineAccumulator, sourceFile, sourceHost);
            }
        }

        private LogEntry CreateLogEntry(List<string> lineAccumulator, string sourceFile, string sourceHost)
        {
            return new LogEntry
            {
                Id = Guid.NewGuid(),
                SourceFile = sourceFile,
                SourceHost = sourceHost,
                Text = String.Join(Environment.NewLine, lineAccumulator),
                Timestamp = LineParser.ParseDateTime(lineAccumulator[0]) ?? DateTime.Now,
            };
        }
    }
}
