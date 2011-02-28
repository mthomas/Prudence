using System.IO;

namespace Prudence
{
    public static class ExtensionsForStream
    {
        public static string ReadFirstLine(this Stream stream)
        {
            var reader = new StreamReader(stream);

            return reader.ReadLine();
        }
    }
}