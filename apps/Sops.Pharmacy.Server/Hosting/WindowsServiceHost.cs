using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.ServiceProcess;

namespace Sops.Pharmacy.Server.Hosting
{
    public class WindowsServiceHost : ServiceBase
    {
        private ServerHost? _host;

        public const string ServiceNameConstant = "SopsPharmacyServer";
        public const string ServiceDisplayName = "SOPs Pharmacy Local Server";
        public const string ServiceDescription = "Authoritative local pharmacy server for Smart Operating Pharmacies (SOPs). Manages encrypted SQLCipher transactions, device enrollments, and client POS connections.";

        public WindowsServiceHost()
        {
            ServiceName = ServiceNameConstant;
            CanStop = true;
            CanShutdown = true;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                _host = new ServerHost();
                _host.StartAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry($"SOPs Pharmacy Server failed to start: {ex}", EventLogEntryType.Error);
                throw;
            }
        }

        protected override void OnStop()
        {
            try
            {
                _host?.Stop();
                _host?.Dispose();
                _host = null;
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry($"SOPs Pharmacy Server error during stop: {ex}", EventLogEntryType.Warning);
            }
        }

        protected override void OnShutdown()
        {
            OnStop();
            base.OnShutdown();
        }
    }

    [RunInstaller(true)]
    public class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            var processInstaller = new ServiceProcessInstaller
            {
                Account = ServiceAccount.LocalSystem
            };

            var serviceInstaller = new ServiceInstaller
            {
                ServiceName = WindowsServiceHost.ServiceNameConstant,
                DisplayName = WindowsServiceHost.ServiceDisplayName,
                Description = WindowsServiceHost.ServiceDescription,
                StartType = ServiceStartMode.Automatic
            };

            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}
