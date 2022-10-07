//This demo project used MahApps.Metro code and resources under Mit License//
//----------https://github.com/MahApps/MahApps.Metro----------



using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using MahApps.Metro.Controls;
using Microsoft.Extensions.Logging;
using imady.NebuLog;
using System.Reflection;
using MahApps.Metro.Controls.Dialogs;
using ControlzEx.Theming;
using imady.NebuLog.DataModel;

namespace NebuLogApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        //===================================================================================
        // 注意：factory.CreateLogger获取的虽然是INebuLogger实例，但只能声明为ILogger，否则会出现null。
        //ILogger _logger;
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
            NebuLogHub.OnRefreshStatRequestReceived += OnRefreshStatRequestRecieved;
            //================================ Server console 演示 =============================

            InitializeComponent();
        }


        #region 响应来自NebuLogHub的事件，进行前端视图的处理
        public void OnLoggingMessageReceived(object sender, NebuLogMessageRequest request)
        {
            if (request == null) return;

            try
            {
                messageList.Add(request);

                this.Dispatcher.Invoke(() =>
                {
                    MessageData.Items.Add( request);
                    MessageData.ScrollIntoView( request);//注意：AutoScroll会导致客户端渲染速度大幅下降
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
        public void OnAddStatRequestReceived(object sender, NebuLogAddStatRequest request)
        {
            if (request == null) return;
            try
            {
                statList.Add(request);

                this.Dispatcher.Invoke(() =>
                {
                    StatDataGrid.Items.Add(request);
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

        private void OnRefreshStatRequestRecieved(object sender, NebuLogRefreshStatRequest request)
        {
            if (request == null) return;
            try
            {
                var item = statList.Find(stat=> stat.StatId.Equals( request.StatId ));

                this.Dispatcher.Invoke(() =>
                {
                    item.StatValue = request.StatValue;
                    StatDataGrid.Items.Refresh();
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
        #endregion




        private async void LaunchGitHubSite(object sender, RoutedEventArgs e)
        {
            // Launch the GitHub site...
            string hashtagUrl = "https://github.com/imadyTech/NebuLog";

            try
            {
                System.Diagnostics.Process.Start(hashtagUrl);
            }
            catch
            {
                // TODO: Warn the user? Log the error? Do nothing since Witty itself is not affected?
            }

            e.Handled = true;
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
