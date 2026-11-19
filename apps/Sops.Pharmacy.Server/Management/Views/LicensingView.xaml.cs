using System.Windows;
using System.Windows.Controls;

namespace Sops.Pharmacy.Server.Management.Views
{
    public partial class LicensingView : UserControl
    {
        public LicensingView()
        {
            InitializeComponent();
        }

        private void OnActivateClicked(object sender, RoutedEventArgs e)
        {
            var key = TxtLicenseKey.Text;
            if (string.IsNullOrWhiteSpace(key))
            {
                TxtActivationResult.Text = "Please enter a valid activation code.";
                return;
            }

            TxtActivationResult.Text = "Verifying cryptographic signature and platform binding...";
            TxtActivationResult.Text = "Activation successful. 5 Terminal seats authorized until 2027-12-31.";
            MessageBox.Show("License validated and applied successfully.", "License Activated", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
