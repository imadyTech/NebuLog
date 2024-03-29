﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NebuLogTestApp.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http;
using imady.NebuLog;
using Microsoft.Extensions.Logging;
using System.Reflection;
using imady.NebuLog.Loggers;

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
            //测试exception记录
            _logger.LogException(
                new Exception("This is a test for logging an exception."));

            return View();
        }

        public IActionResult TypeListing()
        {
            Assembly a = Assembly.GetExecutingAssembly();

            Type[] mytypes = a.GetTypes();
            foreach (Type t in mytypes)
            {
                _logger.LogWarning($"Type {t.Name} is currently in executing assembly.");
            }
            return View();
        }
        public IActionResult AddStatPage()
        {
            //_logger.LogWarning("Home/Privacy");

            return View();
        }
        public void AddStat(string statID, string statTitle)
        {
            _logger.AddCustomStats(statID, statTitle, "green", "NA");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
