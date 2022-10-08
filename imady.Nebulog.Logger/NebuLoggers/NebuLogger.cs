using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Diagnostics;
using System.Runtime.InteropServices;
using imady.NebuLog.DataModel;

namespace imady.NebuLog.Loggers
{
    public class NebuLogger : INebuLogger
    {
        private string _categoryName { get; set; }
        private NebuLogOption _option;
        private HubConnection _connection;
        private bool _isHubConnected;

        /// <summary>
        /// 对外暴露hubconnection，使client也能够接收消息推送。
        /// </summary>
        public HubConnection NebulogHubConnection { get { return _connection; } }


        #region ===== 构造函数 =====
        public NebuLogger(IOptions<NebuLogOption> nebulogOption, HubConnection hubConnection, string categoryName)
        {
            _option = nebulogOption.Value;
            _connection = hubConnection;
            _categoryName = categoryName;

        }

        public NebuLogger(IOptions<NebuLogOption> nebulogOption) : this(nebulogOption.Value, nebulogOption.Value.ProjectName)
        {
        }

        public NebuLogger(NebuLogOption nebulogOption, string categoryName)
        {
            _option = nebulogOption;
            _categoryName = categoryName;

            Action initiateNebulogHubConnection = async () =>
            {
                await StartSignalRAsync(_option.NebuLogHubUrl);
            };
            initiateNebulogHubConnection.Invoke();
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
            await _connection.StopAsync();

            if (!IsDisposed)
            {
                if (Diposing)
                {
                    //Clean Up managed resources  
                }
                //Clean up unmanaged resources  
                //can't remove all registered listeners from eventhandler by a single line command...... you may improve......
                if (NebulogConnected != null)
                {
                    foreach (Delegate d in NebulogConnected.GetInvocationList())
                    {
                        NebulogConnected -= (EventHandler)d;
                    }
                }
            }
            IsDisposed = true;
        }
        ~NebuLogger()
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
            var task = _connection.SendAsync(
                "OnILogging",
                DateTime.Now,
                _option.ProjectName,
                _categoryName,
                logLevel.ToString(),
                //new { Level = logLevel, Content = formatter(state, exception) });
                formatter(state, exception));
            //task.Wait();//2022-09-16 Update
        }
        #endregion


        #region =====INebuLog扩展实现=====
        public bool IsHubConnected => _isHubConnected;

        public async Task LogCustom(string sender, string message)
        {
            //Console.WriteLine ("============== Nebulog LogCustom ================n");
            //this._categoryName = sender;
            this.LogInformation(message);
        }


        public async Task LogException(string exceptionMessage)
        {
            await _connection.SendAsync(
                "OnNebuLogException",
                DateTime.Now,
                _option.ProjectName,
                _categoryName,
                "Exception",//微软官方6种Log级别以外扩展出来的级别
                exceptionMessage
                );
        }

        /// <summary>
        /// 发送异常，以标准方式将Exception序列化后发送。
        /// </summary>
        /// <param name="exception"></param>
        public async Task LogException(Exception exception)
        {
            //Frank: exception已经被序列化，是为了减少服务器端拆箱/装箱的开销。
            //故此要求抛出exception的源需要将异常信息序列化后再传输。
            var formatter = new Func<Exception, string>(ExceptionFormatter);

            await _connection.SendAsync(
                "OnNebuLogException",
                DateTime.Now,
                _option.ProjectName,
                _categoryName,
                "Exception",//微软官方6种Log级别以外扩展出来的级别
                formatter(exception)
                );
        }

        public async Task LogException(string message, string projectName, string categoryName)
        {
            await _connection.SendAsync(
                "OnNebuLogException",
                DateTime.Now,
                projectName,
                categoryName,
                "Exception",//微软官方6种Log级别以外扩展出来的级别
                message);
        }

        /// <summary>
        /// 发送异常，允许调用者提供一个序列化器
        /// </summary>
        /// <param name="exception">异常消息体</param>
        /// <param name="formatter">调用者自己提供的客制化序列化器</param>
        public async Task LogException(Exception exception, Func<Exception, string> formatter)
        {
            //Frank: exception已经被序列化，是为了减少服务器端拆箱/装箱的开销。
            //故此要求抛出exception的源需要将异常信息序列化后再传输。

            await _connection.SendAsync(
                "OnNebuLogException",
                DateTime.Now,
                _option.ProjectName,
                _categoryName,
                "Exception",
                //new { Level = logLevel, Content = formatter(state, exception) });
                formatter(exception)
                );
        }

        /// <summary>
        /// （动态）在监控界面右侧stats面板中创建一条stat条目
        /// </summary>
        /// <param name="statId">要增加的状态监控对象statId（必须保证不与其它id冲突）</param>
        /// <param name="statTitle">状态监控对象的标题</param>
        /// <param name="color">需要显示的颜色</param>
        public async Task AddCustomStats(string statId, string statTitle, string color, string value)
        {
            await _connection.SendAsync(
                "OnAddCustomStats",
                statId,
                statTitle,
                color,
                value ?? "NA");
        }

        /// <summary>
        /// 更新已经增加的stat条目
        /// </summary>
        /// <param name="statId">状态监控对象的Id</param>
        /// <param name="message">需要更新的信息</param>
        public async Task LogCustomStats(string statId, string message)
        {
            await _connection.SendAsync(
                "OnLogCustomStats",
                statId,
                message
                );
        }

        public event EventHandler NebulogConnected;
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

        private async Task Logging(HubConnection connection, string PortName, DateTime time, params string[] parameters)
        {
            await connection.StartAsync();
        }

        private async Task StartSignalRAsync(string nebulogURI)
        {
            if (this._connection == null)
            {
                this._connection = new HubConnectionBuilder()
                    .WithUrl(nebulogURI)
                    .AddJsonProtocol()
                    .Build();

                this._connection.Closed += async (error) =>
                {
                    _isHubConnected = false;
                    await Task.Delay(100);
                    await this._connection.StartAsync();
                    _isHubConnected = true;
                };
                await this._connection.StartAsync();

                _isHubConnected = true;
                //触发一个连接完成的事件，通知事件监听者加载后续模块
                if (NebulogConnected != null) NebulogConnected(this, new EventArgs());
            }
            else
            {
            }
        }
        #endregion
    }




    public class NebuLog<TCategory> : NebuLogger, INebuLogger<TCategory>
    {
        #region =====构造函数=====
        public NebuLog(IOptions<NebuLogOption> nebulogOption) : base(nebulogOption.Value, typeof(TCategory).Name)
        {
        }


        #endregion
    }

}
