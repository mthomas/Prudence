using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lucene.Net.Documents;

namespace LogSearch
{
    public class LogEntry
    {
        public Guid Id { get; set; }
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }

        public string SourceHost { get; set; }
        public string SourceFile { get; set; }

        public Document ToDocument()
        {
            var doc = new Document();

            doc.Add(new Field("Id", Id.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field("Text", Text, Field.Store.YES, Field.Index.ANALYZED));

            var timestamp = new NumericField("Timestamp", Field.Store.YES, true);
            timestamp.SetLongValue(Timestamp.Ticks);
            doc.Add(timestamp);

            doc.Add(new Field("SourceHost", SourceHost, Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field("SourceFile", SourceFile, Field.Store.YES, Field.Index.NOT_ANALYZED));

            return doc;
        }

        public LogEntry()
        {

        }

        public LogEntry(Document document)
        {
            Id = Guid.Parse(document.Get("Id"));
            Text = document.Get("Text");
            Timestamp = new DateTime(long.Parse(document.Get("Timestamp")));
            SourceHost = document.Get("SourceHost");
            SourceFile = document.Get("SourceFile");
        }
    }
}
