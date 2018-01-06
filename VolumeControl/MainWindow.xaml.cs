using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Navigation;
using AudioSwitcher.AudioApi.Session;

namespace VolumeControl
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            version_view_protocol.Content = "protocol v" + App.PROTOCOL_VERSION;
            version_view_app.Content = "application v" + App.APP_VERSION;

            string ipAddress = App.GetLocalIPAddress();
            server_ip.Content = ipAddress;
            Console.WriteLine("ipAddress: " + ipAddress);

            updateConnectionStatus();

            PcVolumeControlUtils.checkVersion();
        }

        private void DownloadLatest_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        public void updateConnectionStatus()
        {
            App app = App.instance;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (app.Server == null || !app.Server.isRunning())
                {
                    server_status.Content = "Offline";
                    start_button.IsEnabled = true;
                    stop_button.IsEnabled = false;
                    server_port.IsEnabled = true;
                }
                else
                {
                    server_status.Content = "Online";
                    start_button.IsEnabled = false;
                    stop_button.IsEnabled = true;
                    server_port.IsEnabled = false;
                }
            }));
        }

        private void start_button_Click(object sender, RoutedEventArgs e)
        {
            App.instance.startServer();
        }

        private void stop_button_Click(object sender, RoutedEventArgs e)
        {
            App.instance.stopServer();
        }

        private void exit_button_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void start_boot_Click(object sender, RoutedEventArgs e)
        {
            App.StartOnBoot();

            MessageBox.Show("PcVolumeControl will now automatically start when your computer boots.", "PcVolumeControl", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void stop_boot_Click(object sender, RoutedEventArgs e)
        {
            App.RemoveOnBoot();

            MessageBox.Show("PcVolumeControl will no longer start with your computer.", "PcVolumeControl", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
