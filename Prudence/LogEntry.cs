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
using Lucene.Net.Documents;

namespace Prudence
{
    public class LogEntry
    {
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
    }
}