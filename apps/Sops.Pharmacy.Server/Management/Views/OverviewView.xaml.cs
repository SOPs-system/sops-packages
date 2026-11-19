using System.Windows;
using System.Windows.Controls;
using Sops.Pharmacy.Server.Management.ViewModels;

namespace Sops.Pharmacy.Server.Management.Views
{
    public partial class OverviewView : UserControl
    {
        public OverviewView()
        {
            InitializeComponent();
        }

        private async void OnRefreshClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                await vm.RefreshStatusAsync();
            }
        }

        private async void OnQuickBackupClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsBusy = true;
                await vm.Client.TriggerBackupAsync();
                await vm.RefreshStatusAsync();
                vm.IsBusy = false;
                MessageBox.Show("Encrypted backup successfully created in the backup directory.", "Backup Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
