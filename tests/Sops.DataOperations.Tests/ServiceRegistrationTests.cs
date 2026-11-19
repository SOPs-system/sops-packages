using System;
using Microsoft.Extensions.DependencyInjection;
using Sops.DataOperations.Abstractions;
using Sops.DataOperations.Extensions;
using Xunit;

namespace Sops.DataOperations.Tests
{
    public class ServiceRegistrationTests
    {
        [Fact]
        public void AddSopsDataOperations_RegistersAllRequiredServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSopsDataOperations();

            using var provider = services.BuildServiceProvider();

            // Verify core coordinator interfaces resolve
            var sync = provider.GetService<ISyncCoordinator>();
            Assert.NotNull(sync);

            var approvals = provider.GetService<IChangeApprovalCoordinator>();
            Assert.NotNull(approvals);

            var backups = provider.GetService<IEncryptedBackupCoordinator>();
            Assert.NotNull(backups);

            var restores = provider.GetService<IRestoreCoordinator>();
            Assert.NotNull(restores);

            var operations = provider.GetService<IDataOperationsCoordinator>();
            Assert.NotNull(operations);

            var status = provider.GetService<IOperationsStatusProvider>();
            Assert.NotNull(status);
        }
    }
}
