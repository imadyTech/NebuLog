using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using System.Net.Http;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using imady.NebuLog;

namespace NebuLogApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IServiceProvider serviceProvider;
        private IHost _host;
        public App()
        {

            IServiceCollection services = new ServiceCollection();

            #region DEBUG 在WPF框架中运行SignalR Hub服务器的测试代码
            //===================================================================================
            //https://stackoverflow.com/questions/60152000/wpf-signalr-server/60153020
            //https://docs.microsoft.com/en-us/aspnet/core/signalr/hubcontext?view=aspnetcore-3.1

            if (_host!=null) _host.Dispose();
            _host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder => webBuilder
                    .UseUrls("http://*:5999")
                    .ConfigureServices(services =>
                    {
                        services.AddSignalR().AddMessagePackProtocol();
                        services.AddSingleton<MainWindow>();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapHub<NebuLogHub>("/NebuLogHub"));
                    }))
               .Build();


            _host.Start();
            //===================================================================================
            #endregion

        }

        protected void OnStartup(object sender, StartupEventArgs e)
        {
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        protected void OnExit(object sender, ExitEventArgs e)
        {
            if (_host != null) _host.Dispose();
        }

    }

}
