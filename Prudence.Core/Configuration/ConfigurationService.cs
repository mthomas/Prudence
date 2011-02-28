using System.IO;
using Newtonsoft.Json;

namespace Prudence.Configuration
{
    public class ConfigurationService
    {
        private static PrudenceConfiguration _config;

        private static readonly object Rock = new object();

        public void Init(string path)
        {
            lock (Rock)
            {
                _config = JsonConvert.DeserializeObject<PrudenceConfiguration>(File.ReadAllText(path));
            }
        }

        public PrudenceConfiguration GetConfiguration()
        {
            return _config;
        }
    }
}