using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MyLogger
{
    public class MyLogger : IMyLogger
    {
        string _categoryName { get; set; }
        MyLoggerOption _option;
        HubConnection connection;

        #region =====构造函数=====
        public MyLogger(IOptions<MyLoggerOption> wxOption) : this(wxOption.Value, "")
        {
        }

        public MyLogger( MyLoggerOption option, string categoryName)
        {
            _option = option;
            _categoryName = categoryName;

            connection = new HubConnectionBuilder()
                //.WithUrl("http://monitor.imady.com/MyLoggerHub")
                .WithUrl(_option.MyLoggerHubUrl)
                .Build();

            connection.Closed += async (error) =>
            {
                await Task.Delay(2000);
                await connection.StartAsync();
            };
            connection.StartAsync().Wait();

        }
        #endregion


        #region =====ILogger实现=====
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


        #region =====IMyLogger扩展实现=====
        public void LogCustom(string sender, string message)
        {
            //this._categoryName = sender;
            this.LogInformation(message);
        }
        #endregion


        #region =====支撑方法=====
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
        connection.InvokeAsync("SendMessage", "Frank","MyLogger test");
        */


        #endregion
    }




    public class MyLogger<TCategory> : MyLogger, IMyLogger<TCategory>
    {
        #region =====构造函数=====
        public MyLogger(IOptions<MyLoggerOption> wxOption) : base (wxOption.Value, typeof(TCategory).Name)
        {
        }


        #endregion
    }

}
