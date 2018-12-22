using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MeiyuMonitor
{
    public class MyLoggerHub: Hub
    {
        public async Task SendMessage(DateTime time, string sourcename, string loglevel, string message)
        {
            await Clients.All.SendAsync("SendMessage", time.ToString("yyyy-MM-dd hh:mm:ss.fff"), sourcename, loglevel, message);
            Console.WriteLine("====================== "+DateTime.Now);
        }

        public async Task Logging(string username, LogInfo log)
        {
            await Clients.All.SendAsync("OnLogging", username, log);
        }

        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            return base.OnDisconnectedAsync(exception);
        }
    }
}
