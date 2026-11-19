using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Sops.Pharmacy.Server.Management.ViewModels;

namespace Sops.Pharmacy.Server.Management.Views
{
    public partial class ServerControlView : UserControl
    {
        public ServerControlView()
        {
            InitializeComponent();
        }

        private async void OnPingHealthClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var sw = Stopwatch.StartNew();
                var ok = await vm.Client.IsServerReachableAsync();
                sw.Stop();

                TxtPingResult.Text = ok
                    ? $"Ping status: 200 OK — Round-trip latency: {sw.ElapsedMilliseconds} ms ({DateTime.Now:T})"
                    : $"Ping status: Connection Failed — Server unreachable on port {vm.Client.Port}";
            }
        }

        private async void OnStartServiceClicked(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsBusy = true;
                await vm.Client.EnsureServerRunningAsync();
                await vm.RefreshStatusAsync();
                vm.IsBusy = false;
                TxtPingResult.Text = "Ping status: Service started and active.";
            }
        }
    }
}
