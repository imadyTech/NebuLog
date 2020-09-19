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
using MahApps.Metro.Controls.Dialogs;
using ControlzEx.Theming;

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

        private List<NebuLogMessageRequest> _messageList;
        public List<NebuLogMessageRequest> messageList
        {
            get { if (_messageList == null) _messageList = new List<NebuLogMessageRequest>(); return _messageList; }
            set => _messageList = value;
        }

        private List<NebuLogAddStatRequest> _statList;
        private long _messageCount;
        public List<NebuLogAddStatRequest> statList
        {
            get { if (_statList == null) _statList = new List<NebuLogAddStatRequest>(); return _statList; }
            set => _statList = value;
        }


        public MainWindow(IServiceProvider services, ILoggerFactory factory) : base()
        {
            ThemeManager.Current.ChangeTheme(this, "Dark.Blue");

            //================================ Server console 演示 =============================
            NebuLogHub.OnILoggingMessageReceived += OnLoggingMessageReceived;
            NebuLogHub.OnAddStatRequestReceived += OnAddStatRequestReceived;
            //================================ Server console 演示 =============================

            InitializeComponent();
        }
        public void OnLoggingMessageReceived(object sender, NebuLogMessageRequest e)
        {
            var arg = e ;
            if (arg == null) return;

            try
            {
                messageList.Add(arg);

                this.Dispatcher.Invoke(() =>
                {
                    var log = arg;
                    MessageData.Items.Add(log);
                    MessageData.ScrollIntoView(log);//注意：AutoScroll会导致客户端渲染速度大幅下降
                    _messageCount++;
                    TestMessageBox.Text = $"Total received {_messageCount} messages.";

                }
                //MessageData.Add(new DataGridTextColumn {  })
                );
            }
            catch (Exception ex)
            {
                messageList.Add(new NebuLogMessageRequest()
                {
                    LogLevel = "Server",
                    LoggingMessage = ex.Message,
                    ProjectName = Application.Current.MainWindow.Name,
                    SenderName = Assembly.GetExecutingAssembly().GetName().Name,
                    TimeOfLog = DateTime.Now
                });
                //this.Dispatcher.Invoke(()=> TestMessageBox.Text = ex.Message);
            }
        }
        public void OnAddStatRequestReceived(object sender, NebuLogAddStatRequest arg)
        {
            if (arg == null) return;
            try
            {
                statList.Add(arg);

                this.Dispatcher.Invoke(() =>
                {
                    var log = arg;
                    StatDataGrid.Items.Add(log);
                    //StatDataGrid.ScrollIntoView(log);//注意：AutoScroll会导致客户端渲染速度大幅下降
                    _messageCount++;
                    TestMessageBox.Text = $"Total received {_messageCount} messages.";

                }
                //MessageData.Add(new DataGridTextColumn {  })
                );
            }
            catch (Exception ex)
            {
                messageList.Add(new NebuLogMessageRequest()
                {
                    LogLevel = "Server",
                    LoggingMessage = ex.Message,
                    ProjectName = Application.Current.MainWindow.Name,
                    SenderName = Assembly.GetExecutingAssembly().GetName().Name,
                    TimeOfLog = DateTime.Now
                });
                //this.Dispatcher.Invoke(()=> TestMessageBox.Text = ex.Message);
            }

        }

        //================================ Client sample code演示=============================
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
        //================================ Client sample code演示=============================


        private async void LaunchGitHubSite(object sender, RoutedEventArgs e)
        {
            // Launch the GitHub site...
            await this.ShowMessageAsync("This is the title", "Some message");
            //_logger.LogCustom("MainWindow", "LaunchGitHubSite");
        }

        private void ClearMessageList(object sender, RoutedEventArgs e)
        {
            messageList.Clear();
            MessageData.Items.Clear();
            // deploy some CupCakes...
            //_logger.LogCustom("MainWindow", "DeployCupCakes");
        }

        #region ============================= 退出时的处理 =============================
        private bool _shutdown;
        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (e.Cancel)
            {
                return;
            }

            if (_shutdown == false)
            {
                e.Cancel = true;

                // We have to delay the execution through BeginInvoke to prevent potential re-entrancy
                Dispatcher.BeginInvoke(new Action(async () => await this.ConfirmShutdown()));
            }
            else
            {
            }
        }
        private async Task ConfirmShutdown()
        {
            var mySettings = new MetroDialogSettings
            {
                AffirmativeButtonText = "Quit",
                NegativeButtonText = "Cancel",
                AnimateShow = true,
                AnimateHide = false
            };

            var result = await this.ShowMessageAsync("Quit application?",
                                                     "Sure you want to quit application?",
                                                     MessageDialogStyle.AffirmativeAndNegative, mySettings);

            _shutdown = result == MessageDialogResult.Affirmative;

            if (_shutdown)
            {
                Application.Current.Shutdown();
            }
        }
        #endregion
    }

}
