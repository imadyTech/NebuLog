using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Threading.Tasks;

namespace imady.NebuLog
{


    public class NebuLogHub: Hub
    {
        public delegate void OnILoggingEventHandler<R>(object sender, R request) where R: INebuLogRequest;

        public static OnILoggingEventHandler<NebuLogMessageRequest> OnILoggingMessageReceived;
        public static OnILoggingEventHandler<NebuLogAddStatRequest> OnAddStatRequestReceived;
        public static OnILoggingEventHandler<NebuLogRefreshStatRequest> OnRefreshStatRequestReceived;



        //public static event EventHandler OnILoggingEvent;
        public NebuLogHub()
        {
            Console.WriteLine($"=========={DateTime.Now} NebuLogHub ============");

        }



        [HubMethodName("OnILogging")]
        public async Task OnILogging(DateTime time, string projectname, string sourcename, string loglevel, string message)
        {
            var nebulogMessage = new NebuLogMessageRequest()
            {
                TimeOfLog = time,
                ProjectName = projectname,
                SenderName = sourcename,
                LogLevel = loglevel,
                LoggingMessage = message
            };

            //===================================================================================
            // 如果将NebuLogHub宿主在WPF客户端中，可以通过Event方式将收到的log消息发送给前端进行显示，而不必通过桌面客户端注册SignalR.Client来获取消息。
            OnILoggingMessageReceived(this,  (nebulogMessage));
            //===================================================================================

            //Console.WriteLine($"=========={DateTime.Now}:: OnILogging {message} ============");
            await Clients.All.SendAsync("OnILogging", time.ToString("yyyy-MM-dd hh:mm:ss.fff"), projectname, sourcename, loglevel, message);
            var context = Context;
            var manager = Groups;

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
        public async Task OnNebuLogCustom(string username, NebuLogMessageRequest log)
        {
            await Clients.All.SendAsync("OnLogging", username, log);
        }

        /// <summary>
        /// （动态）在监控界面右侧stats面板中创建一条stat条目
        /// </summary>
        /// <param name="statId">要增加的状态监控对象statId（必须保证不与其它id冲突）</param>
        /// <param name="statTitle">状态监控对象的标题</param>
        /// <param name="color">需要显示的颜色</param>
        [HubMethodName("OnAddCustomStats")]
        public async Task AddCustomStats(string statId, string statTitle, string color, [Optional]string value)
        {
            var requst = new NebuLogAddStatRequest()
            {
                StatId = statId,
                StatTitle = statTitle,
                StatColor = color,
                StatValue = (value == null) ? "???" : value
            };

            OnAddStatRequestReceived(this,  ( requst ));

            //await Clients.All.SendAsync("OnILogging", DateTime.Now, "OnAddCustomStats", statId, "Debug", statTitle);
            await Clients.All.SendAsync("OnAddCustomStats", statId, statTitle, color, value);

            //var context = Context;
            //var manager = Groups
            //Console.WriteLine($"=========={DateTime.Now}============{context.User?.FindFirst(ClaimTypes.Email)?.Value}::{context.UserIdentifier} of totalclients.");
        }

        /// <summary>
        /// 更新已经增加的stat条目
        /// </summary>
        /// <param name="statId">状态监控对象的Id</param>
        /// <param name="value">需要更新的信息</param>
        [HubMethodName("OnLogCustomStats")]
        public async Task LogCustomStats(string statId, string value)
        {
            var request = new NebuLogRefreshStatRequest()
            {
                StatId = statId,
                StatValue = value
            };
            OnRefreshStatRequestReceived(this, (request));

            //await Clients.All.SendAsync("OnILogging", DateTime.Now, "OnLogCustomStats", statId, "Debug", message);
            await Clients.All.SendAsync("OnLogCustomStats", statId, value);
        }


        public override Task OnConnectedAsync()
        {
            Console.WriteLine("====== client connected. ======" + base.Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            return base.OnDisconnectedAsync(exception);
        }

    }
}
