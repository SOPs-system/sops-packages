using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sops.Configuration;
using Sops.Mail.Abstractions;
using Sops.Mail.Configuration;
using Sops.Mail.Extensions;
using Sops.Mail.Models;
using Sops.Mail.Providers;
using Sops.Mail.Registration;
using Xunit;

namespace Sops.Mail.Tests
{
    public class CompatibilityTests
    {
        [Fact]
        public void StandaloneCreation_WithoutDi_Works()
        {
            var settings = new MailSettings
            {
                FromAddress = "system@sops.internal",
                FromName = "SOPs System",
                Smtp = new SmtpSettings
                {
                    Host = "127.0.0.1",
                    Port = 2525
                }
            };

            using var sender = SmtpMailSender.Create(settings);
            Assert.NotNull(sender);
            Assert.IsAssignableFrom<IMailSender>(sender);
        }

        [Fact]
        public void ServiceCollection_AddSopsMail_Configuration_ResolvesSender()
        {
            var configData = new Dictionary<string, string?>
            {
                ["Mail:Provider"] = "Smtp",
                ["Mail:FromAddress"] = "di@sops.internal",
                ["Mail:Smtp:Host"] = "mail.internal.net",
                ["Mail:Smtp:Port"] = "587"
            };

            var configuration = SopsConfiguration.CreateBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = new ServiceCollection();
            services.AddSopsMail(configuration);

            using var provider = services.BuildServiceProvider();

            var sender = provider.GetRequiredService<IMailSender>();
            Assert.NotNull(sender);
            Assert.IsType<SmtpMailSender>(sender);

            var registry = provider.GetRequiredService<IMailProviderRegistry>();
            Assert.True(registry.IsRegistered("Smtp"));
        }

        [Fact]
        public void ServiceCollection_AddSopsMail_Delegate_ResolvesSender()
        {
            var services = new ServiceCollection();
            services.AddSopsMail(options =>
            {
                options.FromAddress = "delegate@sops.internal";
                options.Smtp.Host = "smtp.office365.com";
                options.Smtp.Port = 587;
                options.Smtp.Security = SmtpSecurity.StartTls;
            });

            using var provider = services.BuildServiceProvider();

            var sender = provider.GetRequiredService<IMailSender>();
            Assert.NotNull(sender);
            Assert.IsType<SmtpMailSender>(sender);
        }

        [Fact]
        public async Task ServiceCollection_AddMailProvider_AllowsPluggableCustomProvider()
        {
            var fakeSender = new FakeMailSender();

            var services = new ServiceCollection();
            services.AddSopsMail(options =>
            {
                options.Provider = "Fake";
                options.FromAddress = "fake@sops.internal";
            });

            services.AddMailProvider("Fake", sp => fakeSender);

            using var provider = services.BuildServiceProvider();

            var sender = provider.GetRequiredService<IMailSender>();
            Assert.NotNull(sender);
            Assert.Same(fakeSender, sender);

            var message = new MailMessage("user@example.com", "Test", "Content");
            var result = await sender.SendAsync(message);

            Assert.True(result.IsSuccess);
            Assert.Equal("fake-id-123", result.MessageId);
            Assert.Single(fakeSender.SentMessages);
        }

        private class FakeMailSender : IMailSender
        {
            public List<MailMessage> SentMessages { get; } = new List<MailMessage>();

            public Task<MailSendResult> SendAsync(MailMessage message, CancellationToken cancellationToken = default)
            {
                SentMessages.Add(message);
                return Task.FromResult(MailSendResult.Success("fake-id-123"));
            }
        }
    }
}
