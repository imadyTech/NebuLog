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
        ILogger _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            _logger.LogTrace("LogTrace.");
            _logger.LogDebug("LogDebug.");
            _logger.LogInformation("LogInformation.");
            _logger.LogWarning("LogWarning.");
            _logger.LogError("LogError.");
            _logger.LogCritical("LogCritical.");

            //这是通过扩展来给ILogger增加新方法的尝试。
            //_logger.LogCustom("Frank","Index action initiated.");

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
