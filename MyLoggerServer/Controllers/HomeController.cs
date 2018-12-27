using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MeiyuMonitor.Models;
using Microsoft.AspNetCore.SignalR;

namespace MeiyuMonitor.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHubContext<MyLoggerHub> _hubContext;

        public HomeController(IHubContext<MyLoggerHub> context)
        {
            _hubContext = context;
        }



        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public async Task<IActionResult> Monitor()
        {
            await _hubContext.Clients.All.SendAsync("SendMessage", $"Home page loaded at: {DateTime.Now}");

            ViewData["Message"] = "苏州美娱网络科技有限公司";
            ViewData["Title"] = "Meiyu Logger Project";
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
