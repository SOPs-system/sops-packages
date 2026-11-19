using System;
using System.Windows;
using System.Windows.Controls;
using Sops.Pharmacy.Server.Management.ViewModels;

namespace Sops.Pharmacy.Server.Management
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            var client = new ManagementClient();
            _viewModel = new MainViewModel(client);
            DataContext = _viewModel;

            Closed += (s, e) =>
            {
                // Closing the management console does NOT terminate the Windows service.
                _viewModel.Dispose();
            };
        }

        private void OnNavClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string key)
            {
                SwitchView(key);
            }
        }

        public void SwitchView(string key)
        {
            ViewOverview.Visibility = key == "Overview" ? Visibility.Visible : Visibility.Collapsed;
            ViewServerControl.Visibility = key == "ServerControl" ? Visibility.Visible : Visibility.Collapsed;
            ViewDevices.Visibility = key == "Devices" ? Visibility.Visible : Visibility.Collapsed;
            ViewDatabase.Visibility = key == "Database" ? Visibility.Visible : Visibility.Collapsed;
            ViewSync.Visibility = key == "Sync" ? Visibility.Visible : Visibility.Collapsed;
            ViewBackupRestore.Visibility = key == "BackupRestore" ? Visibility.Visible : Visibility.Collapsed;
            ViewApprovals.Visibility = key == "Approvals" ? Visibility.Visible : Visibility.Collapsed;
            ViewLicensing.Visibility = key == "Licensing" ? Visibility.Visible : Visibility.Collapsed;
            ViewUsers.Visibility = key == "Users" ? Visibility.Visible : Visibility.Collapsed;
            ViewLogs.Visibility = key == "Logs" ? Visibility.Visible : Visibility.Collapsed;
            ViewSettings.Visibility = key == "Settings" ? Visibility.Visible : Visibility.Collapsed;

            _viewModel.Navigate(key);
        }

        private async void OnGlobalRefreshClicked(object sender, RoutedEventArgs e)
        {
            await _viewModel.RefreshStatusAsync();
        }

        private void OnThemeToggleClicked(object sender, RoutedEventArgs e)
        {
            _viewModel.ToggleTheme();
        }

        private void OnRtlToggleClicked(object sender, RoutedEventArgs e)
        {
            _viewModel.ToggleRtl();
        }
    }
}
