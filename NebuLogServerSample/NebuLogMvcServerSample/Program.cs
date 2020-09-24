using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace imady.NebuLogServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                //.AddJsonFile("hosting.json", optional: true)
                .Build();

                // Frank 2020.09.07 已过时：asp.net core 2.2
                //CreateWebHostBuilder(args, config).Build().Run();
                CreateHostBuilder(args)
                .Build()
                .Run();
        }



        // Frank 2020.09.07 已过时：asp.net core 2.2
        /*
        public static IWebHostBuilder CreateWebHostBuilder(string[] args, IConfiguration config) =>
                WebHost.CreateDefaultBuilder(args)
                .UseConfiguration(config)
                .UseKestrel()
                .UseUrls("http://localhost:5999")
                .UseStartup<Startup>();
        */

        //--- ASP.NET CORE 3.0
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(option =>
                {
                    option.AddConsole()
                    .AddDebug();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(serverOptions =>
                    {
                        // Set properties and call methods on options
                    })
                    .UseUrls("http://*:5999")
                    .UseIISIntegration()
                    .UseStartup<Startup>();
                });
    }
}
