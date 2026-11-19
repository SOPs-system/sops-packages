using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sops.WhatsApp.Abstractions;
using Sops.WhatsApp.Configuration;
using Sops.WhatsApp.Extensions;
using Sops.WhatsApp.Providers.MetaCloud;
using Sops.WhatsApp.Registration;
using Xunit;

namespace Sops.WhatsApp.Tests
{
    public class RegistrationAndCompatibilityTests
    {
        [Fact]
        public void Registry_PreRegistersMetaCloud()
        {
            var registry = new WhatsAppClientRegistry();

            Assert.True(registry.IsRegistered("MetaCloud"));
            Assert.Contains("MetaCloud", registry.RegisteredProviders);

            var client = registry.CreateClient("MetaCloud");
            Assert.NotNull(client);
            Assert.Equal("MetaCloud", client.ProviderName);
        }

        [Fact]
        public void Registry_CanRegisterCustomProvider()
        {
            var registry = new WhatsAppClientRegistry();
            var customClient = Substitute.For<IWhatsAppClient>();
            customClient.ProviderName.Returns("CustomTwilio");

            registry.Register("CustomTwilio", sp => customClient);

            Assert.True(registry.IsRegistered("CustomTwilio"));
            var resolved = registry.CreateClient("CustomTwilio");
            Assert.Same(customClient, resolved);
        }

        [Fact]
        public void Registry_UnregisteredProvider_ThrowsNotSupportedException()
        {
            var registry = new WhatsAppClientRegistry();

            var ex = Assert.Throws<NotSupportedException>(() => registry.CreateClient("NonExistentProvider"));
            Assert.Contains("NonExistentProvider", ex.Message);
        }

        [Fact]
        public void AddSopsWhatsApp_ResolvesClientAndRegistryViaDI()
        {
            var configData = new Dictionary<string, string?>
            {
                ["WhatsApp:Provider"] = "MetaCloud",
                ["WhatsApp:Enabled"] = "true",
                ["WhatsApp:BaseUrl"] = "https://graph.facebook.com",
                ["WhatsApp:PhoneNumberId"] = "102553952458",
                ["WhatsApp:AccessToken"] = "EAAB123"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = new ServiceCollection();
            services.AddSopsWhatsApp(configuration);

            using var provider = services.BuildServiceProvider();

            var client = provider.GetRequiredService<IWhatsAppClient>();
            var metaClient = provider.GetRequiredService<MetaCloudWhatsAppClient>();
            var registry = provider.GetRequiredService<IWhatsAppClientRegistry>();

            Assert.NotNull(client);
            Assert.NotNull(metaClient);
            Assert.NotNull(registry);
            Assert.Equal("MetaCloud", client.ProviderName);
        }

        [Fact]
        public void Standalone_ClientInstantiation_WorksWithoutDI()
        {
            var settings = new WhatsAppSettings
            {
                PhoneNumberId = "102553952458",
                AccessToken = "EAAB_standalone_token"
            };

            using var client = new MetaCloudWhatsAppClient(settings);
            Assert.Equal("MetaCloud", client.ProviderName);
        }
    }
}
