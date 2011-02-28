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
using System.Linq;
using Lucene.Net.Analysis;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Version = Lucene.Net.Util.Version;

namespace Prudence
{
    public static class LogSearcher
    {
        private static readonly IndexSearcher searcher;

        static LogSearcher()
        {
            var dir = FSDirectory.Open(new DirectoryInfo(Program.IndexDir));

            searcher = new IndexSearcher(dir, true);
        }

        public static IList<LogEntry> Search(string q, DateTime start, DateTime end, int skip, int take)
        {
            var parser = new QueryParser(Version.LUCENE_29, "Text", new SimpleAnalyzer());

            var query = parser.Parse(q);

            var filter = NumericRangeFilter.NewLongRange("Timestamp", start.Ticks, end.Ticks, true, true);


            var hits = searcher.Search(query, filter, (skip + 1)*take,
                                       new Sort(new SortField("Timestamp", SortField.LONG, true)));

            var rangeOfHits = hits.scoreDocs.Skip(skip).Take(take);

            return rangeOfHits
                .Select(scoreDoc => new LogEntry(searcher.Doc(scoreDoc.doc)))
                .ToList();
        }
    }
}