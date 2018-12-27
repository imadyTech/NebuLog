using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace MyLoggerTestApp
{
    public class MyLoggerAttribute : Attribute
    {
        HubConnection connection;


        private string _parameter;
        public string Parameter
        {
            get { return _parameter; }
        }
        private HttpContext _context;

        public MyLoggerAttribute(string parameter, HttpContext context)
        {
            _parameter = parameter;
            _context = context;

            connection = new HubConnectionBuilder()
                .WithUrl("http://monitor.imady.com/MyLoggerHub")
                .Build();

            connection.Closed += async (error) =>
            {
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await connection.StartAsync();
            };


        }
        public async Task Logging()
        {
            #region snippet_ConnectionOn
            connection.On<string, string>("ReceiveMessage", (user, message) =>
            {
            });
            #endregion

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
                    _parameter, _context.Request.Method);
                #endregion
            }
            catch (Exception ex)
            {
                await connection.InvokeAsync("SendMessage",
                    "MeiyuLogger", ex.Message);
            }
            #endregion
        }
    }
}
