using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sops.Configuration;
using Sops.Payments.Abstractions;
using Sops.Payments.Configuration;
using Sops.Payments.Extensions;
using Sops.Payments.Models;
using Sops.Payments.Providers.Paymob;
using Sops.Payments.Registration;
using Xunit;

namespace Sops.Payments.Tests
{
    public class CompatibilityTests
    {
        [Fact]
        public void StandaloneCreation_WithoutDi_Works()
        {
            var settings = new PaymentSettings
            {
                Paymob = new PaymobSettings
                {
                    SecretKey = "sec_test_secret"
                }
            };

            using var gateway = PaymobPaymentGateway.Create(settings);
            Assert.NotNull(gateway);
            Assert.IsAssignableFrom<IPaymentGateway>(gateway);
        }

        [Fact]
        public void ServiceCollection_AddSopsPayments_Configuration_ResolvesGateway()
        {
            var configData = new Dictionary<string, string?>
            {
                ["Payment:Provider"] = "Paymob",
                ["Payment:Paymob:SecretKey"] = "sec_test_key_123",
                ["Payment:Paymob:IntegrationId"] = "999"
            };

            var configuration = SopsConfiguration.CreateBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            var services = new ServiceCollection();
            services.AddSopsPayments(configuration);

            using var provider = services.BuildServiceProvider();

            var gateway = provider.GetRequiredService<IPaymentGateway>();
            Assert.NotNull(gateway);
            Assert.IsType<PaymobPaymentGateway>(gateway);

            var registry = provider.GetRequiredService<IPaymentGatewayRegistry>();
            Assert.True(registry.IsRegistered("Paymob"));
        }

        [Fact]
        public void ServiceCollection_AddSopsPayments_Delegate_ResolvesGateway()
        {
            var services = new ServiceCollection();
            services.AddSopsPayments(options =>
            {
                options.Paymob.SecretKey = "sec_delegate_key";
                options.Paymob.IntegrationId = 1234;
            });

            using var provider = services.BuildServiceProvider();

            var gateway = provider.GetRequiredService<IPaymentGateway>();
            Assert.NotNull(gateway);
            Assert.IsType<PaymobPaymentGateway>(gateway);
        }

        [Fact]
        public async Task ServiceCollection_AddPaymentGateway_AllowsCustomPluggableProvider()
        {
            var mockGateway = new MockPaymentGateway();

            var services = new ServiceCollection();
            services.AddSopsPayments(options =>
            {
                options.Provider = "Mock";
                options.Paymob.SecretKey = "dummy";
            });

            services.AddPaymentGateway("Mock", sp => mockGateway);

            using var provider = services.BuildServiceProvider();

            var gateway = provider.GetRequiredService<IPaymentGateway>();
            Assert.NotNull(gateway);
            Assert.Same(mockGateway, gateway);

            var customer = new PaymentCustomer("John", "Doe", "john@example.com", "+1234567890");
            var request = new PaymentRequest(Money.FromDecimal(10.00m, "USD"), customer);

            var result = await gateway.InitiatePaymentAsync(request);
            Assert.True(result.IsSuccess);
            Assert.Equal("mock-checkout-url", result.CheckoutUrl);
        }

        private class MockPaymentGateway : IPaymentGateway
        {
            public Task<PaymentResult> InitiatePaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(PaymentResult.Pending("mock-checkout-url", "mock-cs", request.OrderId));
            }

            public Task<PaymentVerificationResult> VerifyPaymentAsync(string transactionId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(PaymentVerificationResult.Success(transactionId, "ORD-MOCK", Money.FromDecimal(10m, "USD")));
            }

            public Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(RefundResult.Success("RF-1", request.TransactionId, request.Amount));
            }

            public Task<PaymentWebhookEvent?> ProcessWebhookAsync(string rawPayload, string? signature, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<PaymentWebhookEvent?>(null);
            }
        }
    }
}
