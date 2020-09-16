using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebuLog;


namespace NebuLogWpfCoreSample
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IServiceProvider serviceProvider;
        public App()
        {
            IServiceCollection services = new ServiceCollection();

            ConfigureServices(services);

            this.Configure(services);//2020-09-14 实际上没有使用loggerfactory.UseNebuLogWpf()来获取logger实例


            #region DEBUG 截获WPF信息的测试代码
            /*
            //===================================================================================
            //这是基于WPF系统事件的思路
            Exit += OnExit;
            void OnExit(object sender, EventArgs e) { }

            //System.Diagnostics思路
            System.Diagnostics.Debug.Print("======System.Diagnostics.Debug.Print======");
            //Trace思路
            Trace.Listeners.Add(new TextWriterTraceListener("TextWriterOutput.log", "myListener"));
            Trace.WriteLine("====== Test message. ======");
            Trace.Flush();
            Trace.Close();
            //===================================================================================
            */
            #endregion

            #region DEBUG 直接连接SignalR服务器的测试代码
            //https://docs.microsoft.com/zh-cn/aspnet/core/signalr/dotnet-client?view=aspnetcore-3.1&tabs=visual-studio
            /* This part of code worked, the SignalR server written in APS.NET Core 3.1.7.
             * The NebuLog instance was basically activated in this way, operated by the loggerFactory in imady.NebuLog.dll:
            HubConnection connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5999/NebuLogHub", httpconnectionoptions =>
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
                .AddMessagePackProtocol()
                .Build();
            connection.Closed += async (error) =>
            {
                await Task.Delay(2000);
                await connection.StartAsync();
            };
            connection.StartAsync().Wait();

            var task = connection.SendAsync(
                "OnILogging",
                DateTime.Now,
                "NebuLogWPFCore",
                "App",
                "Debug",//微软官方6种Log级别以外扩展出来的级别
                "Test signalr connected."
                );
            task.Wait();
             * 
            */

            /* ============== This works as well ==============
            INebuLog logger = new NebuLog.NebuLog(
                new NebuLogOption()
                {
                    NebuLogHubUrl = "https://nebulog.yingyu88.cn/NebuLogHub",
                    LogLevel = LogLevel.Trace,
                    ProjectName = "ProjectName"
                },
                "MainWindow");
            logger.LogCustom("MainWindow", "MainWindow init");
            */

            /* ============== This works as well ============== 
            var option = services.BuildServiceProvider().GetService<IOptions<NebuLogOption>>();
            var logger = factory.CreateLogger<INebuLog>();

            // ============== OR ==============

            var logger = serviceProvider.GetRequiredService<INebuLog<MainWindow>>();
            logger.LogCustom("MainWindow", "NebuLogApp test");

            */
            #endregion
        }

        protected void OnStartup(object sender, StartupEventArgs e)
        {
            var mainWindow = serviceProvider.GetService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            //===================================================================================
            services.Configure<NebuLogOption>(option =>
            {
                option.NebuLogHubUrl = ConfigurationManager.AppSettings["NebuLogHubUrl"];

                object level = LogLevel.Trace;
                Enum.TryParse(typeof(LogLevel), ConfigurationManager.AppSettings["LogLevel"], out level);
                option.LogLevel = (LogLevel)level;

                option.ProjectName = ConfigurationManager.AppSettings["ProjectName"];
            }
                );
            //===================================================================================
            //注意：AddNebuLogFactory是专门为WPF框架运行流程写的。
            //通常是调用AddNebuLog()
            services.AddNebuLogFactory();
            //设置系统日志输出的最小级别
            services.AddLogging(builder =>
            {
                builder
                    //.AddConfiguration(Configuration.GetSection("NebuLogOption"))
                    // filter for all providers
                    //.AddFilter("System", LogLevel.Trace)
                    // Only for Debug logger, using the provider type or it's alias
                    //.AddFilter("Debug", LogLevel.Trace)
                    // Only for Console logger by provider type
                    .AddFilter<NebuLogWpfProvider>("Microsoft", LogLevel.Trace);
            });
            //===================================================================================


            services.AddSingleton(typeof(MainWindow));

        }


        public void Configure(IServiceCollection services)
        {

            //===================================================================================
            //如果想开启系统日志输出到INebuLog则使用以下代码
            //var option = services.BuildServiceProvider().GetService<IOptions<NebuLogOption>>();
            //loggerFactory.UseNebuLog(services);//extension方式添加NebuLog，两种写法效果等同
            //loggerFactory.AddProvider(new NebuLogProvider(option));
            //===================================================================================


            //===================================================================================
            //针对WPF框架专门的写法；如果在MainWindow构造器直接注入INebuLogger会出现Null错误
            serviceProvider = services.BuildServiceProvider();
            var factory = serviceProvider.GetService<ILoggerFactory>();
            var option = serviceProvider.GetRequiredService<IOptions<NebuLogOption>>();
            factory.UseNebuLogWpf(option.Value, GetConnection(option.Value));
            //===================================================================================

            //测试能否获取logger实例并发送信息
            //var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("");
            //logger.Log<App>(LogLevel.Information, new EventId(0, ""), this, null, (app, e) => "NebuLogWpfCoreSample");
        }

        private HubConnection GetConnection(NebuLogOption option)
        {
            HubConnection connection = new HubConnectionBuilder()
            .WithUrl(option.NebuLogHubUrl, httpconnectionoptions =>
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
            .AddMessagePackProtocol()
            .Build();
            connection.Closed += async (error) =>
            {
                await Task.Delay(2000);
                await connection.StartAsync();
            };
            connection.StartAsync().Wait();
            return connection;
            /*
            var task = connection.SendAsync(
                "OnILogging",
                DateTime.Now,
                "NebuLogWPFCore",
                "App",
                "Debug",//微软官方6种Log级别以外扩展出来的级别
                "Test signalr connected."
                );
            task.Wait();
            */

        }
    }
}
