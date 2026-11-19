using System;
using System.Configuration.Install;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Sops.Pharmacy.Server.Configuration;
using Sops.Pharmacy.Server.Hosting;

namespace Sops.Pharmacy.Server
{
    public static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        [STAThread]
        public static int Main(string[] args)
        {
            var config = ServerConfigLoader.Load();

            // 1. Health check CLI probe
            if (HasArg(args, "--health"))
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                return RunHealthCheckProbe(config.Port);
            }

            // 2. Windows Service Mode (Session 0 safe, NO WPF INITIALIZED)
            if (HasArg(args, "--service"))
            {
                ServiceBase.Run(new WindowsServiceHost());
                return 0;
            }

            // 3. Service Installation commands
            if (HasArg(args, "--install-service"))
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                try
                {
                    Console.WriteLine("Installing SOPs Pharmacy Local Server Windows Service...");
                    ManagedInstallerClass.InstallHelper(new[] { Assembly.GetExecutingAssembly().Location });
                    Console.WriteLine("Service installed successfully.");
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to install service: {ex.Message}");
                    return 1;
                }
            }

            if (HasArg(args, "--uninstall-service"))
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                try
                {
                    Console.WriteLine("Uninstalling SOPs Pharmacy Local Server Windows Service...");
                    ManagedInstallerClass.InstallHelper(new[] { "/u", Assembly.GetExecutingAssembly().Location });
                    Console.WriteLine("Service uninstalled successfully.");
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to uninstall service: {ex.Message}");
                    return 1;
                }
            }

            // 4. Console Debug Mode
            if (HasArg(args, "--console"))
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                Console.WriteLine("Starting SOPs Pharmacy Local Server in Console Debug Mode...");
                using var host = new ServerHost(config);
                host.StartAsync().GetAwaiter().GetResult();
                Console.WriteLine($"Server running on port {config.Port}. Press CTRL+C to stop.");

                var exitEvent = new ManualResetEvent(false);
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    exitEvent.Set();
                };

                exitEvent.WaitOne();
                Console.WriteLine("Shutting down server...");
                host.Stop();
                return 0;
            }

            // 5. Native WPF Management Console Mode (Default interactive)
            return RunWpfManagementConsole(config);
        }

        private static bool HasArg(string[] args, string flag)
        {
            if (args == null || args.Length == 0) return false;
            foreach (var arg in args)
            {
                if (string.Equals(arg?.Trim(), flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static int RunHealthCheckProbe(int port)
        {
            var ports = new[] { port, port + 1, port + 2 };
            Exception? lastEx = null;

            foreach (var p in ports)
            {
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                    var response = client.GetAsync($"http://127.0.0.1:{p}/health").GetAwaiter().GetResult();
                    var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    Console.WriteLine(content);
                    return response.IsSuccessStatusCode ? 0 : 1;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                }
            }

            Console.Error.WriteLine($"Health probe failed: {lastEx?.Message}");
            return 1;
        }

        private static int RunWpfManagementConsole(PharmacyServerSettings config)
        {
            // Launch the WPF Application
            var app = new Management.App();
            app.InitializeComponent();
            return app.Run();
        }
    }
}
