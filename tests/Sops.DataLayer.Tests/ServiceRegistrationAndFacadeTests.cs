using System;
using Microsoft.Extensions.DependencyInjection;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.DependencyInjection;
using Xunit;

namespace Sops.DataLayer.Tests
{
    public class ServiceRegistrationAndFacadeTests
    {
        [Fact]
        public void AddSopsDataLayer_Registers_All_Required_Contracts()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSopsDataLayer(options =>
            {
                options.LocalServerAddress = new Uri("http://127.0.0.1:5000");
                options.TerminalId = "POS-01";
            });

            using var provider = services.BuildServiceProvider();

            // Verify core facade
            var dataLayer = provider.GetService<ISopsDataLayer>();
            Assert.NotNull(dataLayer);

            // Verify all child services
            Assert.NotNull(dataLayer!.Client);
            Assert.NotNull(dataLayer.Auth);
            Assert.NotNull(dataLayer.Drugs);
            Assert.NotNull(dataLayer.Patients);
            Assert.NotNull(dataLayer.Inventory);
            Assert.NotNull(dataLayer.Transactions);
            Assert.NotNull(dataLayer.Shifts);
            Assert.NotNull(dataLayer.Settings);
            Assert.NotNull(dataLayer.Categories);
            Assert.NotNull(dataLayer.Warehouses);
            Assert.NotNull(dataLayer.Licensing);
            Assert.NotNull(dataLayer.Sync);
            Assert.NotNull(dataLayer.Connectivity);
            Assert.NotNull(dataLayer.Events);
            Assert.NotNull(dataLayer.Live);
            Assert.NotNull(dataLayer.Cache);
            Assert.NotNull(dataLayer.Files);
            Assert.NotNull(dataLayer.Authentication);
            Assert.NotNull(dataLayer.Authorization);
        }
    }
}
