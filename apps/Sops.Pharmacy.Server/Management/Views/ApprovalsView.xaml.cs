using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Sops.Pharmacy.Server.Management.ViewModels;

namespace Sops.Pharmacy.Server.Management.Views
{
    public class ApprovalItem
    {
        public string Id { get; set; } = string.Empty;
        public string ChangeType { get; set; } = string.Empty;
        public string Requester { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; }
    }

    public partial class ApprovalsView : UserControl
    {
        private readonly List<ApprovalItem> _items = new List<ApprovalItem>
        {
            new ApprovalItem
            {
                Id = "req_88192a",
                ChangeType = "Price Override",
                Requester = "cashier1",
                Reason = "Damaged packaging discount 10% for Augmentin 1g",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow.AddMinutes(-42)
            },
            new ApprovalItem
            {
                Id = "req_4410bc",
                ChangeType = "Stock Adjustment",
                Requester = "cashier1",
                Reason = "Broken bottle during stocking - Brufen Syrup 100ml (-1)",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            }
        };

        public ApprovalsView()
        {
            InitializeComponent();
            LstApprovals.ItemsSource = _items;
        }

        private void OnRefreshClicked(object sender, RoutedEventArgs e)
        {
            LstApprovals.ItemsSource = null;
            LstApprovals.ItemsSource = _items;
        }

        private void OnApproveClicked(object sender, RoutedEventArgs e)
        {
            if (LstApprovals.SelectedItem is ApprovalItem selected)
            {
                selected.Status = "Approved";
                OnRefreshClicked(sender, e);
                MessageBox.Show($"Change request {selected.Id} has been approved and applied.", "Request Approved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OnRejectClicked(object sender, RoutedEventArgs e)
        {
            if (LstApprovals.SelectedItem is ApprovalItem selected)
            {
                selected.Status = "Rejected";
                OnRefreshClicked(sender, e);
                MessageBox.Show($"Change request {selected.Id} has been rejected.", "Request Rejected", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
