using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NebuLogServer
{
    public class NebuLogHub: Hub
    {

        public NebuLogHub()
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
        
        [HubMethodName("OnNebuLogException")]
        public async Task OnNebuLogException(DateTime time, string projectname, string sourcename, string loglevel, string exception)
        {
            //Frank: exception已经被序列化，是为了减少服务器端拆箱/装箱的开销。
            //故此要求抛出exception的源需要将异常信息序列化后再传输。

            await Clients.All.SendAsync(
                "OnNebuLogException", 
                time.ToString("yyyy-MM-dd hh:mm:ss.fff"), 
                projectname, 
                sourcename, 
                loglevel, 
                exception);
            var context = Context;
            var manager = Groups;
        }

        [HubMethodName("OnNebuLogCustom")]
        public async Task OnNebuLogCustom(string username, LogInfo log)
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
