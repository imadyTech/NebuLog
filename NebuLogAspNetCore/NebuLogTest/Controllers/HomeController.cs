using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NebuLogTestApp.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http;
using NebuLog;
using Microsoft.Extensions.Logging;

namespace NebuLogTestApp.Controllers
{
    public class HomeController : Controller
    {
        //这里需要把Ilogg改为INebuLog
        INebuLog _logger;

        public HomeController(INebuLog<HomeController> logger)
        {
            _logger = logger;
            //(_logger as INebuLog).AddCustomStats("customStat", "customStat", "green");
        }


        public IActionResult Index()
        {
            //(_logger as INebuLog).LogException(new Exception("exception test"));
            return View();
        }

        public IActionResult Privacy()
        {

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
