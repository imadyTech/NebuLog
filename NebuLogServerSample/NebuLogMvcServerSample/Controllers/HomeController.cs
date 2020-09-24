using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using imady.NebuLogServer;
using Microsoft.AspNetCore.SignalR;
using imady.NebuLog;

namespace NebuLogServer.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHubContext<NebuLogHub> _hubContext;

        public HomeController(IHubContext<NebuLogHub> context)
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

        public IActionResult Monitor()
        {


            ViewData["Message"] = "苏州美娱网络科技有限公司";
            ViewData["Title"] = "NebuLog - Nebula Logger";
            //await _hubContext.Clients.All.SendAsync("OnILogging", DateTime.Now, "project", "source", "Debug", "NebuLog");
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
