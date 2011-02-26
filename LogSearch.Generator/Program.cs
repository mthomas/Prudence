using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net.Config;
using log4net;
using System.Threading;

namespace LogSearch.Generator
{
    class Program
    {
        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();

            var log = LogManager.GetLogger("Test");

            var types =
                typeof(Int32).Assembly.GetTypes().Where(x => x.Name.Contains("Exception"))
                .ToList();

            while (true)
            {
                var type = types.Select(t => new { t, id = Guid.NewGuid() })
                    .OrderBy(x => x.id)
                    .Select(x => x.t)
                    .First();

                Exception ex;

                try
                {
                    ex = type.GetConstructors().First().Invoke(null) as Exception;

                    throw ex;
                }
                catch (Exception x)
                {
                    log.Error(x);
                }

                //Thread.Sleep(1);
            }   
        }
    }
}
