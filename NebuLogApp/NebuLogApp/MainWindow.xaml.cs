using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MahApps.Metro.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http;
using imady.NebuLog;
using Microsoft.Extensions.Options;
using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace NebuLogApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        //===================================================================================
        // 注意：factory.CreateLogger获取的虽然是INebuLogger实例，但只能声明为ILogger，否则会出现null。
        ILogger _logger;
        //INebuLogger _logger;
        //===================================================================================


        public List<NebuLogMessage> messageList { get; set; }


        public MainWindow(IServiceProvider services, ILoggerFactory factory) : base()
        {
            //====================================sample code演示=================================
            // 注意：无法直接从DI框架获取ILogger实例，如果通过构造器注入会得到null
            // 调用CreateLogger也不能直接用this.Name，因为MainWindow是在App.Xaml.cs中OnStartup()通过依赖注入的，XAML中的Name属性标签无效。
            _logger = factory.CreateLogger(this.GetType().Name);
            _logger.Log<MainWindow>(
                LogLevel.Information, //loglevel
                new EventId(0, this.Name), //eventId
                this, //state
                null, //exception
                (app, e) => $"{Assembly.GetExecutingAssembly().GetName().Name}.{app.GetType().Name} initated.");
            // ILogger记录的日志信息能够被NebuLog服务器收到，可能是asp.net core日志系统已经添加了INebuLogger，但是程序代码获取实例写得不正确。
            //===================================================================================

            
            
            NebuLogHub.onILoggingEventHandler += OnLoggingMessageReceived;
            messageList = new List<NebuLogMessage>();
            //MessageData.ItemsSource = messageList;

            InitializeComponent();
        }

        private void OnLoggingMessageReceived(object sender, OnILoggingEventArgs e)
        {
            if (e.LoggingMessage == null) return;

            try
            {
                messageList.Add(e.LoggingMessage);

                this.Dispatcher.Invoke (() =>
                {
                    var log = e.LoggingMessage;
                    MessageData.Items.Add(log);
                    MessageData.ScrollIntoView(log);//注意：AutoScroll会导致客户端渲染速度大幅下降
                    TestMessageBox.Text = $"Total received {messageList.Count} messages.";

                }
                //MessageData.Add(new DataGridTextColumn {  })
                );
            }
            catch(Exception ex)
            {
                messageList.Add(new NebuLogMessage()
                {
                    LogLevel= "Server",
                    LoggingMessage = ex.Message,
                     ProjectName= Application.Current.MainWindow.Name,
                     SenderName = Assembly.GetExecutingAssembly().GetName().Name,
                      TimeOfLog = DateTime.Now
                });
                 //this.Dispatcher.Invoke(()=> TestMessageBox.Text = ex.Message);
            }
        }

        private void OnTestButtonClick(object sender, RoutedEventArgs e)
        {
            var message = TestMessageBox.Text;
            try
            {
                _logger.Log<MainWindow>(
                    LogLevel.Information, //loglevel
                    new EventId(0, sender.GetType().Name), //eventId
                    this, //state
                    null, //exception
                    (app, e) => $"{(sender as Button).Name}: {message}");
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.Message);
            }
        }


        private void LaunchGitHubSite(object sender, RoutedEventArgs e)
        {
            // Launch the GitHub site...
            _logger.LogCustom("MainWindow", "LaunchGitHubSite");
        }

        private void DeployCupCakes(object sender, RoutedEventArgs e)
        {
            // deploy some CupCakes...
            _logger.LogCustom("MainWindow", "DeployCupCakes");
        }
    }

}
