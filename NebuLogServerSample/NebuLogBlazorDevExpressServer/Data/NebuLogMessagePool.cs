using imady.NebuLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;


namespace NebuLogBlazorDevExpressServer
{
    public class NebuLogMessagePool
    {
        public delegate void OnNewMessgeReceivedHanlder(object sender, NebuLogMessageRequest request);
        public OnNewMessgeReceivedHanlder _blazorHandler;

        public NebuLogMessagePool()
        {
            //注册一个监听到NebuLogHub事件
            NebuLogHub.OnILoggingMessageReceived += OnMessageReceived;
        }

        private void OnMessageReceived(object sender, NebuLogMessageRequest request)
        {
            //再把收到的消息丢出去
            _blazorHandler?.Invoke(this, request);
        }
    }
}
