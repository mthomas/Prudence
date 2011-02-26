using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lucene.Net.Store;
using System.IO;
using Lucene.Net.QueryParsers;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Search;
using Lucene.Net.Analysis;

namespace LogSearch
{
    public static class LogSearcher
    {
        static Lucene.Net.Search.IndexSearcher searcher;

        static LogSearcher()
        {
            
            var dir =
                FSDirectory.Open(new DirectoryInfo(Program.IndexDir));



            searcher = new Lucene.Net.Search.IndexSearcher(dir, true);
        }

       public static IList<LogEntry> Search(string q,  DateTime start, DateTime end, int skip, int take)
        { 
            QueryParser parser = new QueryParser(Lucene.Net.Util.Version.LUCENE_29, "Text", new SimpleAnalyzer());

            var query = parser.Parse(q);

            var filter = NumericRangeFilter.NewLongRange("Timestamp", start.Ticks, end.Ticks, true, true);


            
            var hits = searcher.Search(query, filter, (skip + 1) * take, new Sort(new SortField("Timestamp", SortField.LONG, true)));

            var rangeOfHits = hits.scoreDocs.Skip(skip).Take(take);

            var results = new List<LogEntry>();

            foreach (var scoreDoc in rangeOfHits)
            {
                var doc = searcher.Doc(scoreDoc.doc);
                var entry = new LogEntry(doc);
                results.Add(entry);
            }

            return results;
        }
    }
}
