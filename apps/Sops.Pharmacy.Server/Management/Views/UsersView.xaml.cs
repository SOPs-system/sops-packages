using System.Windows;
using System.Windows.Controls;
using Sops.Pharmacy.Server.Management.ViewModels;

namespace Sops.Pharmacy.Server.Management.Views
{
    public partial class UsersView : UserControl
    {
        public UsersView()
        {
            InitializeComponent();
            Loaded += async (s, e) => await LoadUsersAsync();
        }

        private async void OnReloadClicked(object sender, RoutedEventArgs e)
        {
            await LoadUsersAsync();
        }

        private async System.Threading.Tasks.Task LoadUsersAsync()
        {
            if (DataContext is MainViewModel vm)
            {
                var users = await vm.Client.GetUsersAsync();
                LstUsers.ItemsSource = users;
            }
        }

        private async void OnAddUserClicked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("User accounts can be enrolled via the Management Console or synced from administrative cloud management.", "User Enrollment", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadUsersAsync();
        }
    }
}
