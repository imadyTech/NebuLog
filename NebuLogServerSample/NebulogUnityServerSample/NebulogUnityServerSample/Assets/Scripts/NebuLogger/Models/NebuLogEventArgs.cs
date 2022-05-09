using System;
using System.Collections.Generic;
using System.Text;

namespace imady.NebuLog
{
    /// <summary>
    /// 通过C#的Event消息机制通知同一进程内的监听者。
    /// </summary>
    [Obsolete ("Please use imady.Event system.")]
    public class OnILoggingEventArgs<T> : EventArgs where T: INebuLogRequest
    {
        public T LoggingMessage { get; set; }

        public OnILoggingEventArgs(T message)
        {
            LoggingMessage = message;
        }
    }

}
