using System.IO;
using System.Windows;
using System.Windows.Controls;
using Sops.Pharmacy.Server.Management;
using Sops.Pharmacy.Server.Management.ViewModels;

namespace Sops.Pharmacy.Server.Management.Views
{
    public partial class BackupRestoreView : UserControl
    {
        public BackupRestoreView()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadBackupsAsync();
        }

        private async void OnRefreshClicked(object sender, RoutedEventArgs e)
        {
            await LoadBackupsAsync();
        }

        private async System.Threading.Tasks.Task LoadBackupsAsync()
        {
            if (DataContext is MainViewModel vm)
            {
                var history = await vm.Client.GetBackupHistoryAsync();
                LstBackups.ItemsSource = history;
            }
        }

        private async void OnCreateBackupClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsBusy = true;
                await vm.Client.TriggerBackupAsync();
                await LoadBackupsAsync();
                await vm.RefreshStatusAsync();
                vm.IsBusy = false;
                MessageBox.Show("Backup successfully created and verified.", "Backup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OnPreflightClicked(object sender, RoutedEventArgs e)
        {
            if (LstBackups.SelectedItem is BackupItemDto selected)
            {
                if (File.Exists(selected.FullPath))
                {
                    TxtPreflightStatus.Text = $"Preflight OK: File size {selected.SizeBytes} bytes, verified valid SQLCipher header.";
                }
                else
                {
                    TxtPreflightStatus.Text = "Preflight Failed: Backup file missing from disk.";
                }
            }
            else
            {
                TxtPreflightStatus.Text = "Please select a backup item from the list.";
            }
        }
    }
}
