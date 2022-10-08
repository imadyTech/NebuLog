using imady.Event;
using imady.Message;
using imady.NebuLog;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NebulogUnityServer
{
    public class UnityNebulogger : IUnityNebulog, IDisposable
    {

        private HubConnection nebulogHubConnection;
        private NebulogManager manager;

        public event EventHandler NebulogConnected;

        /// <summary>
        /// NebulogHub如果未完成连接则禁止调用。(这个指示其实可有可无)
        /// </summary>
        public static bool IsHubConnected = false;

        public static string defaulNebulogHubUri = "http://localhost:5999/NebuLogHub";

        public UnityNebulogger()
        {
            Action initiateNebulogHubConnection =
                async () => 
                { 
                    await StartSignalRAsync(defaulNebulogHubUri); 
                };
            initiateNebulogHubConnection.Invoke();

        }

        public UnityNebulogger AddManager(NebulogManager manager)
        {
            this.manager = manager;
            return this;
        }

        async Task StartSignalRAsync(string nebulogURI)
        {
            if (this.nebulogHubConnection == null)
            {
                this.nebulogHubConnection = new HubConnectionBuilder()
                    .WithUrl(nebulogURI)
                    .AddJsonProtocol()
                    .Build();

                this.nebulogHubConnection.Closed += async (error) =>
                {
                    IsHubConnected = false;
                    await Task.Delay(100);
                    await this.nebulogHubConnection.StartAsync();
                    IsHubConnected = true;
                };
                await this.nebulogHubConnection.StartAsync();

                IsHubConnected = true;
                Debug.Log("Signalr connected ...");

                //TODO: improve the imady.Event system to support dynamic provider/observer mapping.
                nebulogHubConnection.On<string, string, string, string, string>("OnILogging", this.ReceiveOnILoggingTestor);

                //触发一个连接完成的事件，通知事件监听者加载后续模块
                NebulogConnected(this, new EventArgs());
            }
            else
            {
                Debug.Log("Signalr already connected ...");
            }

        }

        public void ReceiveOnILoggingTestor(string time, string projectname, string sourcename, string loglevel, string message)
        {
            Debug.Log($"[Nebulog ReceiveOnILogging] {projectname}-{sourcename}");
        }

        #region 析构函数（断开signalR连接）
        private bool IsDisposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected async void Dispose(bool Diposing)
        {
            await nebulogHubConnection.StopAsync();

            if (!IsDisposed)
            {
                if (Diposing)
                {
                    //Clean Up managed resources  
                }
                //Clean up unmanaged resources  
            }
            IsDisposed = true;
        }
        ~UnityNebulogger()
        {
            Dispose(false);
        }
#endregion

        /// <summary>
        /// HandleNebuLog
        /// </summary>
        /// <param name="nebumessage"></param>
        /// <param name="stackTrace"></param>
        /// <param name="type"></param>
        public void HandleUnityLogs(string message, string stackTrace, LogType type)
        {
            if (!IsHubConnected)
                return;

#region LogType/LogLevel lookup
            // --- enum UnityEngine.LogType | enum Microsoft.Extensions.Logging.LogLevel ---
            //                              |   Trace = 0,
            //  Log = 3,                    |   Debug = 1,
            //                              |   Information = 2,
            //  Warning = 2,                |   Warning = 3,
            //  Error = 0,                  |   Error = 4,
            //
            //  LogType used for Asserts. (These indicate an error inside Unity itself.)
            //  用于断言(Assert)的日志类型（这些表明Unity自身的一个错误）。
            //  Assert = 1,                 |   Critical = 5,
            //  Exception = 4               |   NebuLog.Exception (N/A in Microsoft)
            //                              |   none = 6
            //---------------------------------------------------------------------------------
#endregion

            string nebumessage = StackTraceFormatter(message, stackTrace);
            var projectName = Application.productName;
            var categoryName = SceneManager.GetActiveScene().name;

            switch (type)
            {
                case LogType.Error:
                    LogMessage(nebumessage, "UnityError", projectName, categoryName);
                    break;
                case LogType.Assert:
                    LogMessage( nebumessage, "UnityAssert", projectName, categoryName);
                    break;
                case LogType.Warning:
                    LogMessage( nebumessage, LogLevel.Warning.ToString(), projectName, categoryName);
                    break;
                case LogType.Log:
                    LogMessage( nebumessage, "UnityLog", projectName, categoryName);
                    break;
                case LogType.Exception:
                    //NebuLog针对Exception有专门的接口
                    LogException(nebumessage, projectName, categoryName);
                    break;
                default:
                    LogMessage( nebumessage, LogLevel.Debug.ToString(), projectName, categoryName);
                    break;
            }
        }


#region =====INebuLog扩展实现=====
        public void LogMessage( string message, string loglevel, string projectName, string categoryName)
        {
            if (!IsHubConnected) return;

            var task = nebulogHubConnection.SendAsync(
                "OnILogging",
                DateTime.Now,
                projectName,
                categoryName,
                loglevel,
                message
                );
            task.Wait();
        }

        public void LogException(string exceptionMessage, string projectName, string categoryName)
        {
            if (!IsHubConnected) return;

            var task = nebulogHubConnection.SendAsync(
                "OnNebuLogException",
                DateTime.Now,
                projectName,
                categoryName,
                "Exception",//微软官方6种Log级别以外扩展出来的级别
                exceptionMessage
                );
            task.Wait();
        }

        /// <summary>
        /// 发送异常，以标准方式将Exception序列化后发送。
        /// </summary>
        /// <param name="exception"></param>
        public void LogException(Exception exception, string projectName, string categoryName)
        {
            //if (!IsHubConnected) return;

            //Frank: exception已经被序列化，是为了减少服务器端拆箱/装箱的开销。
            //故此要求抛出exception的源需要将异常信息序列化后再传输。
            var formatter = new Func<Exception, string>(this.ExceptionFormatter);

            var task = nebulogHubConnection.SendAsync(
                "OnNebuLogException",
                DateTime.Now,
                projectName,
                categoryName,
                "Exception",//微软官方6种Log级别以外扩展出来的级别
                formatter(exception)
                );
            task.Wait();
        }

        /// <summary>
        /// 发送异常，允许调用者提供一个序列化器
        /// </summary>
        /// <param name="exception">异常消息体</param>
        /// <param name="formatter">调用者自己提供的客制化序列化器</param>
        public void LogException(Exception exception, string projectName, string categoryName, Func<Exception, string> formatter)
        {
            //Frank: exception已经被序列化，是为了减少服务器端拆箱/装箱的开销。
            //故此要求抛出exception的源需要将异常信息序列化后再传输。

            var task = nebulogHubConnection.SendAsync(
                "OnNebuLogException",
                DateTime.Now,
                projectName,
                categoryName,
                "Exception",
                formatter(exception)
                );
            task.Wait();
        }

        /// <summary>
        /// （动态）在监控界面右侧stats面板中创建一条stat条目
        /// </summary>
        /// <param name="statId">要增加的状态监控对象statId（必须保证不与其它id冲突）</param>
        /// <param name="statTitle">状态监控对象的标题</param>
        /// <param name="color">需要显示的颜色</param>
        public void AddCustomStats(string statId, string statTitle, string color, string value)
        {
            if (!IsHubConnected) return;

            var task = nebulogHubConnection.SendAsync(
                "OnAddCustomStats",
                statId,
                statTitle,
                color,
                value ?? "???");
            task.Wait();

        }

        /// <summary>
        /// 更新已经增加的stat条目
        /// </summary>
        /// <param name="statId">状态监控对象的Id</param>
        /// <param name="message">需要更新的信息</param>
        public void LogCustomStats(string statId, string message)
        {
            if (!IsHubConnected) return;

            var task = nebulogHubConnection.SendAsync(
                "OnLogCustomStats",
                statId,
                message
                );
            task.Wait();

        }
        #endregion



#region =====private methods 支撑方法=====
        //private string ExceptionFormatterResult;
        /// <summary>
        /// Exception的序列化
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        public string ExceptionFormatter(Exception arg)
        {
            var ExceptionFormatterResult = "";

            ExceptionFormatterResult += $"{arg.Message}</br>";
            ExceptionFormatterResult += $"Source: {arg.Source}</br>";
            ExceptionFormatterResult += $"StackTrace: {arg.StackTrace}</br> ";


            if (arg.InnerException != null)

            {
                var formatter = new Func<Exception, string>(ExceptionFormatter);
                ExceptionFormatterResult += formatter(arg.InnerException);
            }
            return ExceptionFormatterResult;
        }

        public string StackTraceFormatter(string message, string  stacktrace)
        {
            var StackTraceFormatterResult = string.Empty;
            //Nebulog客户端会将unity debug.log中的<方法名称>误认为xml,因此先替换掉
            stacktrace=stacktrace
                .Replace("<", "")
                .Replace(">", ".")
            //unity debug.log中包含换行字符,替换为br标记在HTML中显示比较友善.
                .Replace("\n", "<br>");

            StackTraceFormatterResult += $"{message}<br>";
            //StackTraceFormatterResult += $"Source: {Application.productName + "::" + SceneManager.GetActiveScene().name}<br>";
            StackTraceFormatterResult += stacktrace;

            return StackTraceFormatterResult;
        }
#endregion

    }
}
