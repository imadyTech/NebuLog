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
using NebuLog;
using Microsoft.Extensions.Options;

namespace NebuLogApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private INebuLogger _logger;

        //default constructor works, as it doesn't request an active SignalrR connection.

        // If the default constructor is not available, then this construtor requested an INebuLog goes to dead lock, as it try to activate the connection with NebuLog Server (ASPNETCore.SignalR 3.1.7).
        public MainWindow(ILoggerFactory factory, IServiceProvider services, INebuLogger logger)
        {

            _logger = logger;
            InitializeComponent();
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
