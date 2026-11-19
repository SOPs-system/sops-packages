using System.Windows;
using Sop.Ui;

namespace Sops.Pharmacy.Server.Management
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Theme.Initialize(this);

            var window = new MainWindow();
            Theme.Attach(window);
            MainWindow = window;
            window.Show();
        }
    }
}
