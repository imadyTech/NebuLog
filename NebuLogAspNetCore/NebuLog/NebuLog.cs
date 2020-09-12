using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Diagnostics;

namespace NebuLog
{
    public class NebuLog : INebuLog
    {
        string _categoryName { get; set; }
        NebuLogOption _option;
        HubConnection connection;

        #region =====构造函数=====
        public NebuLog(IOptions<NebuLogOption> nebulogOption) : this(nebulogOption.Value, "")
        {
        }

        public NebuLog(NebuLogOption nebulogOption, string categoryName)
        {
            _option = nebulogOption;
            _categoryName = categoryName;

            connection = new HubConnectionBuilder()
                .WithUrl(_option.NebuLogHubUrl, httpconnectionoptions =>
                {
                    httpconnectionoptions.HttpMessageHandlerFactory = (handler) =>
                    {
                        var newHandler = handler as HttpClientHandler;
                        newHandler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) =>
                        {
                            return true;
                        };
                        return newHandler;
                    };
                })
                // Frank 2020.09.07 已过时：asp.net core 2.2
                //.AddJsonProtocol()
                //--- ASP.NET CORE 3.0
                //.AddJsonProtocol()
                .AddMessagePackProtocol()
                .Build();

            connection.Closed += async (error) =>
            {
                await Task.Delay(2000);
                await connection.StartAsync();
            };
            connection.StartAsync().Wait();
            //Console.WriteLine("==============Nebulog init==============");
        }
        #endregion

        #region 析构函数（断开signalR连接）
        private bool IsDisposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected async void Dispose(bool Diposing)
        {
            await connection.StopAsync();

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
        ~NebuLog()
        {
            Dispose(false);
        }
        #endregion


        #region =====标准ILogger接口实现=====
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _option.LogLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            //Console.WriteLine ("************** Nebulog Log *****************");
            var datetime = DateTime.Now.ToString().Replace('/', '-');
            var task = connection.SendAsync(
                "OnILogging",
                DateTime.Now,
                _option.ProjectName,
                _categoryName,
                logLevel.ToString(),
                //new { Level = logLevel, Content = formatter(state, exception) });
                formatter(state, exception));
            task.Wait();
        }
        #endregion


        #region =====INebuLog扩展实现=====
        public void LogCustom(string sender, string message)
        {
            //Console.WriteLine ("============== Nebulog LogCustom ================n");
            //this._categoryName = sender;
            this.LogInformation(message);
        }


        public void LogException(string exceptionMessage)
        {
            var task = connection.SendAsync(
                "OnNebuLogException",
                DateTime.Now,
                _option.ProjectName,
                _categoryName,
                "Exception",//微软官方6种Log级别以外扩展出来的级别
                exceptionMessage
                );
            task.Wait();
        }

        /// <summary>
        /// 发送异常，以标准方式将Exception序列化后发送。
        /// </summary>
        /// <param name="exception"></param>
        public void LogException(Exception exception)
        {
            //Frank: exception已经被序列化，是为了减少服务器端拆箱/装箱的开销。
            //故此要求抛出exception的源需要将异常信息序列化后再传输。
            var formatter = new Func<Exception, string>(ExceptionFormatter);

            var task = connection.SendAsync(
                "OnNebuLogException",
                DateTime.Now,
                _option.ProjectName,
                _categoryName,
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
        public void LogException(Exception exception, Func<Exception, string> formatter)
        {
            //Frank: exception已经被序列化，是为了减少服务器端拆箱/装箱的开销。
            //故此要求抛出exception的源需要将异常信息序列化后再传输。

            var task = connection.SendAsync(
                "OnNebuLogException",
                DateTime.Now,
                _option.ProjectName,
                _categoryName,
                "Exception",
                //new { Level = logLevel, Content = formatter(state, exception) });
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
        public void AddCustomStats(string statId, string statTitle, string color)
        {
            var task = connection.SendAsync(
                "OnAddCustomStats",
                statId,
                statTitle,
                color);
            task.Wait();

        }

        /// <summary>
        /// 更新已经增加的stat条目
        /// </summary>
        /// <param name="statId">状态监控对象的Id</param>
        /// <param name="message">需要更新的信息</param>
        public void LogCustomStats(string statId, string message)
        {
            var task = connection.SendAsync(
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
        private string ExceptionFormatter(Exception arg)
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

        private void Logging(HubConnection connection, string PortName, DateTime time, params string[] parameters)
        {
            connection.StartAsync().Wait();
        }
        /*
        try
        {
            connection.StartAsync();
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        connection.InvokeAsync("SendMessage", "Frank","NebuLog test");
        */


        #endregion
    }




    public class NebuLog<TCategory> : NebuLog, INebuLog<TCategory>
    {
        #region =====构造函数=====
        public NebuLog(IOptions<NebuLogOption> nebulogOption) : base(nebulogOption.Value, typeof(TCategory).Name)
        {
        }


        #endregion
    }

}
