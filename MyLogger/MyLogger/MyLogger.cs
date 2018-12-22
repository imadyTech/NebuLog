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

        public MyLogger(MyLoggerOption option, string categoryName)
        {
            _option = option;
            _categoryName = categoryName;

            connection = new HubConnectionBuilder()
                //.WithUrl("http://monitor.imady.com/MyLoggerHub")
                .WithUrl(_option.MyLoggerHubUrl)
                .Build();

            connection.Closed += async (error) =>
            {
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await connection.StartAsync();
            };

        }
        public async Task LogCustom(string sender, string parameter)
        {
            /*
            #region snippet_ConnectionOn
            connection.On<string, string>("ReceiveMessage", (user, message) =>
            {
            });
            #endregion
            */

            try
            {
                await connection.StartAsync();
            }
            catch (Exception ex)
            {
            }

            #region snippet_ErrorHandling
            try
            {
                #region snippet_InvokeAsync
                await connection.InvokeAsync("SendMessage",
                    sender,
                    parameter);
                #endregion
            }
            catch (Exception ex)
            {
                await connection.InvokeAsync("SendMessage",
                    "ErrorSending", ex.Message);
            }
            #endregion

        }


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

            connection.StartAsync().Wait();
            var task = connection.SendAsync(
                "SendMessage",
                DateTime.Now,
                _categoryName,
                logLevel.ToString(),
                //new { Level = logLevel, Content = formatter(state, exception) });
                formatter(state, exception));
            task.Wait();
        }
    }



    /// <summary>
    /// 这是通过扩展来给ILogger增加新方法的尝试。
    /// </summary>
    public static class MyLoggerExtensionMethods
    {
        public static void LogCustom(this ILogger logger, string sender, string message)
        {

        }
    }
}
