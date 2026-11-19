using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Sops.Pharmacy.Server.Domain.Auth;
using Sops.Pharmacy.Server.Management.ViewModels;

namespace Sops.Pharmacy.Server.Management.Views
{
    public partial class LogsView : UserControl
    {
        private IReadOnlyList<AuditEventDto> _allLogs = Array.Empty<AuditEventDto>();

        public LogsView()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadLogsAsync();
        }

        private async void OnRefreshClicked(object sender, RoutedEventArgs e)
        {
            await LoadLogsAsync();
        }

        private async System.Threading.Tasks.Task LoadLogsAsync()
        {
            if (DataContext is MainViewModel vm)
            {
                _allLogs = await vm.Client.GetAuditLogsAsync();
                ApplyFilter();
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var filter = TxtSearch.Text?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(filter))
            {
                LstLogs.ItemsSource = _allLogs;
                return;
            }

            var filtered = _allLogs.Where(l =>
                (l.Action?.ToLowerInvariant().Contains(filter) == true) ||
                (l.UserId?.ToLowerInvariant().Contains(filter) == true) ||
                (l.EntityType?.ToLowerInvariant().Contains(filter) == true) ||
                (l.DetailsJson?.ToLowerInvariant().Contains(filter) == true)).ToList();

            LstLogs.ItemsSource = filtered;
        }
    }
}
