using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace LogSearch.Web.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/

        public ActionResult Index(string q, DateTime? from, DateTime? to)
        {
            if (q != null)
            {
                var results = LogSearcher.Search(q, from.Value, to.Value, 0, 100);
                return View(new SearchResultViewData { Entries = results });
            }

            return View(new SearchResultViewData());
        }

    }

    public class SearchResultViewData
    {
        public IList<LogEntry> Entries { get; set; }
    }
}
