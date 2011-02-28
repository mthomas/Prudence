namespace Prudence.Configuration
{
    public class IndexerConfiguration
    {
        public string IncomingPath { get; set; }
        public string ProcessingPath { get; set; }
        public string ProcessedPath { get; set; }

        public string LockFilePath { get; set; }
    }
}