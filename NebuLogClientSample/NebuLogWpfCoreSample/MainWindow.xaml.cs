using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using imady.NebuLog;

namespace NebuLogWpfCoreSample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //===================================================================================
        // 注意：factory.CreateLogger获取的虽然是INebuLogger实例，但只能声明为ILogger，否则会出现null。
        ILogger _logger;
        //INebuLogger _logger;
        //===================================================================================



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

            InitializeComponent();
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
    }
}
