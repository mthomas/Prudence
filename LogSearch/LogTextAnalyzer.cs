using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lucene.Net.Analysis;

namespace LogSearch
{
    //public class LogText
    class LogTextAnalyzer : Analyzer
    {
        public override TokenStream TokenStream(System.String fieldName, System.IO.TextReader reader)
        {
            return new LowerCaseTokenizer(reader);
        }

        public override TokenStream ReusableTokenStream(System.String fieldName, System.IO.TextReader reader)
        {
            Tokenizer tokenizer = (Tokenizer)GetPreviousTokenStream();
            if (tokenizer == null)
            {
                tokenizer = new LowerCaseTokenizer(reader);
                SetPreviousTokenStream(tokenizer);
            }
            else
                tokenizer.Reset(reader);
            return tokenizer;
        }
    }
}
