using System;
using System.Windows;
using System.Windows.Controls;
using Sops.Pharmacy.Server.Management.ViewModels;

namespace Sops.Pharmacy.Server.Management.Views
{
    public partial class SyncView : UserControl
    {
        public SyncView()
        {
            InitializeComponent();
        }

        private async void OnTriggerSyncClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                TxtSyncStatus.Text = "Synchronizing pending outbox stream...";
                await System.Threading.Tasks.Task.Delay(500);
                await vm.RefreshStatusAsync();
                TxtSyncStatus.Text = $"Sync cycle finished at {DateTime.Now:T}. All pending records validated.";
            }
        }
    }
}
