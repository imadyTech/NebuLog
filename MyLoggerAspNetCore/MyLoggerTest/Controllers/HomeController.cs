using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyLoggerTestApp.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http;
using MyLogger;
using Microsoft.Extensions.Logging;

namespace MyLoggerTestApp.Controllers
{
    public class HomeController : Controller
    {
        //这里需要把Ilogg改为IMyLogger
        IMyLogger _logger;

        public HomeController(IMyLogger<HomeController> logger)
        {
            _logger = logger;
        }


        public IActionResult Index()
        {
            //这是扩展后的IMyLogger新方法的尝试。
            _logger.LogCustom("Frank","Index action initiated.");

            //这些是兼容原ILogger的方法
            _logger.LogTrace("This is a Test: LogTrace.");
            _logger.LogDebug("This is a Test: LogDebug.");
            _logger.LogInformation("This is a Test: LogInformation.");
            _logger.LogWarning("This is a Test: LogWarning.");
            _logger.LogError("This is a Test: LogError.");
            _logger.LogCritical("This is a Test: LogCritical.");

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
