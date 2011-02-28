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
using System.Linq;
using System.Text.RegularExpressions;

namespace Prudence
{
    public class LineParser
    {
        private static readonly Dictionary<string, Func<string, DateTime>> _parsers = new Dictionary
            <string, Func<string, DateTime>>
                                                                                          {
                                                                                              {
                                                                                                  @"^\d+-\d+-\d+ \d+:\d+:\d+,\d+ "
                                                                                                  ,
                                                                                                  s =>
                                                                                                  DateTime.Parse(
                                                                                                      s.ToString().
                                                                                                          Replace(",",
                                                                                                                  "."))
                                                                                                  },
                                                                                              {
                                                                                                  @"^  \d+:\d+:\d+.\d+ "
                                                                                                  ,
                                                                                                  s => DateTime.Now
                                                                                                  }
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
            var lineAccumulator = new List<string>();

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