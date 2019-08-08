using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NebuLog
{
    public interface INebuLog: ILogger
    {
        void LogCustom( string sender, string message);

        /// <summary>
        /// 发送异常，以标准方式将Exception序列化后发送。
        /// </summary>
        /// <param name="exception">异常消息体</param>
        void LogException(Exception exception);

        /// <summary>
        /// 发送异常，允许调用者提供一个序列化器
        /// </summary>
        /// <param name="exception">异常消息体</param>
        /// <param name="formatter">调用者自己提供的客制化序列化器</param>
        void LogException(Exception exception, Func<Exception, string> formatter);
    }

    public interface INebuLog<out TCategoryName> : INebuLog
    {

    }

}
