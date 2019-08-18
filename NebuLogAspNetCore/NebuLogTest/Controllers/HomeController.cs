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
        ILogger _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }


        public IActionResult Index()
        {

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
