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
        INebuLogger _logger;

        public HomeController(INebuLogger<HomeController> logger)
        {
            _logger = logger;
            //(_logger as INebuLog).AddCustomStats("customStat", "customStat", "green");
        }


        public IActionResult Index()
        {
            //(_logger as INebuLog).LogException(new Exception("exception test"));
            //_logger.Log<HomeController>(LogLevel.Debug, new EventId(0, "Index"), this, null, (t, e)=> "=================="+ t.ToString());
            _logger.LogCustom(this.GetType().Name, "Home/Privacy");
            return View();
        }

        public IActionResult Privacy()
        {
            _logger.LogWarning("Home/Privacy");

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
