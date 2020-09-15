using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NebuLog
{
    public interface INebuLogger: ILogger, IDisposable
    {
        /// <summary>
        /// 发送自定义的字符串信息；
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="message"></param>
        void LogCustom( string sender, string message);

        /// <summary>
        /// 发送序列化以后的exception字符信息。
        /// </summary>
        /// <param name="exceptionMessage">序列化以后的exception字符串。</param>
        void LogException(string exceptionMessage);

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


        /// <summary>
        /// （动态）在监控界面右侧stats面板中创建一条stat条目
        /// </summary>
        /// <param name="statId">要增加的状态监控对象statId（必须保证不与其它id冲突）</param>
        /// <param name="statTitle">状态监控对象的标题</param>
        /// <param name="color">需要显示的颜色</param>
        void AddCustomStats(string statId, string statTitle, string color);

        /// <summary>
        /// 更新已经增加的stat条目
        /// </summary>
        /// <param name="statId">状态监控对象的Id</param>
        /// <param name="message">需要更新的信息</param>
        void LogCustomStats(string statId, string message);
    }

    public interface INebuLogger<out TCategoryName> : INebuLogger
    {

    }

}
