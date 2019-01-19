using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MeiyuMonitor
{
    public class MyLoggerHub: Hub
    {

        public MyLoggerHub()
        {
            
        }



        [HubMethodName("OnILogging")]
        public async Task SendMessage(DateTime time, string projectname, string sourcename, string loglevel, string message)
        {
            await Clients.All.SendAsync("OnILogging", time.ToString("yyyy-MM-dd hh:mm:ss.fff"), projectname, sourcename, loglevel, message);
            var context = Context;
            var manager = Groups;

            //Console.WriteLine($"=========={DateTime.Now}============{context.User?.FindFirst(ClaimTypes.Email)?.Value}::{context.UserIdentifier} of totalclients.");
        }
        
        [HubMethodName("OnMyLogCustom")]
        public async Task OnMyLogCustom(string username, LogInfo log)
        {
            await Clients.All.SendAsync("OnLogging", username, log);
        }

        [HubMethodName("OnMyLogException")]
        public async Task OnMyLogException(string username, Exception log)
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
