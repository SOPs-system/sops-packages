using System.Windows;
using System.Windows.Controls;
using Sops.Pharmacy.Server.Domain.Devices;
using Sops.Pharmacy.Server.Management.ViewModels;

namespace Sops.Pharmacy.Server.Management.Views
{
    public partial class DevicesView : UserControl
    {
        public DevicesView()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadDevicesAsync();
        }

        private async void OnReloadClicked(object sender, RoutedEventArgs e)
        {
            await LoadDevicesAsync();
        }

        private async System.Threading.Tasks.Task LoadDevicesAsync()
        {
            if (DataContext is MainViewModel vm)
            {
                var devices = await vm.Client.GetTerminalsAsync();
                LstTerminals.ItemsSource = devices;
            }
        }

        private async void OnApproveClicked(object sender, RoutedEventArgs e)
        {
            if (LstTerminals.SelectedItem is TerminalDto selected && DataContext is MainViewModel vm)
            {
                await vm.Client.ApproveTerminalAsync(selected.Id);
                await LoadDevicesAsync();
            }
        }

        private async void OnRevokeClicked(object sender, RoutedEventArgs e)
        {
            if (LstTerminals.SelectedItem is TerminalDto selected && DataContext is MainViewModel vm)
            {
                await vm.Client.RevokeTerminalAsync(selected.Id);
                await LoadDevicesAsync();
            }
        }
    }
}
