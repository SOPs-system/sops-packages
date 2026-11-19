using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sops.Payments.Abstractions;
using Sops.Payments.Configuration;
using Sops.Payments.Models;
using Sops.Payments.Providers.Paymob;
using Xunit;

namespace Sops.Payments.Tests
{
    public class PaymobGatewayTests
    {
        private readonly TestHttpMessageHandler _httpHandler;
        private readonly HttpClient _httpClient;

        public PaymobGatewayTests()
        {
            _httpHandler = new TestHttpMessageHandler();
            _httpClient = new HttpClient(_httpHandler);
        }

        [Fact]
        public async Task InitiatePaymentAsync_IntentionApi_ConstructsCorrectRequestAndReturnsCheckoutUrl()
        {
            var settings = new PaymentSettings
            {
                Paymob = new PaymobSettings
                {
                    BaseUrl = "https://accept.paymob.com",
                    SecretKey = "sec_test_secret_123",
                    PublicKey = "sec_test_public_456",
                    IntegrationId = 8888,
                    DefaultCurrency = "EGP"
                }
            };

            string responseJson = @"{
                ""cs"": ""client_secret_xyz_789"",
                ""payment_keys"": [
                    { ""key"": ""tok_111"", ""integration_id"": 8888 }
                ]
            }";

            _httpHandler.EnqueueResponse(HttpStatusCode.Created, responseJson);

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);

            var customer = new PaymentCustomer("Mohamed", "Ali", "mohamed@example.com", "+201000000000");
            var request = new PaymentRequest(Money.FromDecimal(150.00m, "EGP"), customer, "ORD-555", "IDEMP-555");
            request.Items.Add(new PaymentItem("Pro Plan", Money.FromDecimal(150.00m, "EGP"), 1));

            var result = await gateway.InitiatePaymentAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Equal(PaymentStatus.Pending, result.Status);
            Assert.Equal("client_secret_xyz_789", result.ClientSecret);
            Assert.Equal("ORD-555", result.OrderId);
            Assert.Contains("unifiedcheckout/?publicKey=sec_test_public_456&clientSecret=client_secret_xyz_789", result.CheckoutUrl);

            Assert.Single(_httpHandler.SentRequests);
            var sentRequest = _httpHandler.SentRequests[0];
            Assert.Equal("https://accept.paymob.com/v1/intention/", sentRequest.RequestUri!.ToString());
            Assert.Contains("Token sec_test_secret_123", sentRequest.Headers.GetValues("Authorization"));

            string requestContent = _httpHandler.SentRequestBodies[0];
            using var doc = JsonDocument.Parse(requestContent);
            var root = doc.RootElement;
            Assert.Equal(15000, root.GetProperty("amount").GetInt64());
            Assert.Equal("EGP", root.GetProperty("currency").GetString());
            Assert.Equal("IDEMP-555", root.GetProperty("special_reference").GetString());
        }

        [Fact]
        public async Task InitiatePaymentAsync_LegacyFlow_Executes3StepAuthOrderAndKey()
        {
            var settings = new PaymentSettings
            {
                Paymob = new PaymobSettings
                {
                    BaseUrl = "https://accept.paymob.com",
                    ApiKey = "api_key_legacy_123",
                    IntegrationId = 4444,
                    IframeId = 9999,
                    DefaultCurrency = "EGP"
                }
            };

            // Step 1: Auth response
            _httpHandler.EnqueueResponse(HttpStatusCode.Created, @"{ ""token"": ""auth_tok_111"" }");
            // Step 2: Order response
            _httpHandler.EnqueueResponse(HttpStatusCode.Created, @"{ ""id"": 777666 }");
            // Step 3: Payment Key response
            _httpHandler.EnqueueResponse(HttpStatusCode.Created, @"{ ""token"": ""pay_token_333"" }");

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);

            var customer = new PaymentCustomer("Sarah", "Kamal", "sarah@example.com", "+201011111111");
            var request = new PaymentRequest(Money.FromDecimal(200.00m, "EGP"), customer, "ORD-LEGACY-1");

            var result = await gateway.InitiatePaymentAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Equal(PaymentStatus.Pending, result.Status);
            Assert.Equal("pay_token_333", result.ClientSecret);
            Assert.Equal("777666", result.OrderId);
            Assert.Contains("/api/acceptance/iframes/9999?payment_token=pay_token_333", result.CheckoutUrl);

            Assert.Equal(3, _httpHandler.SentRequests.Count);
            Assert.Contains("/api/auth/tokens", _httpHandler.SentRequests[0].RequestUri!.ToString());
            Assert.Contains("/api/ecommerce/orders", _httpHandler.SentRequests[1].RequestUri!.ToString());
            Assert.Contains("/api/acceptance/payment_keys", _httpHandler.SentRequests[2].RequestUri!.ToString());
        }

        [Fact]
        public async Task InitiatePaymentAsync_DisabledInConfig_SuppressesExecution()
        {
            var settings = new PaymentSettings
            {
                Enabled = false,
                Paymob = new PaymobSettings
                {
                    SecretKey = "sec_test"
                }
            };

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);
            var customer = new PaymentCustomer("Ahmed", "Ali", "ahmed@example.com", "+201000000000");
            var request = new PaymentRequest(Money.FromDecimal(50.00m, "EGP"), customer, "ORD-DISABLED");

            var result = await gateway.InitiatePaymentAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Equal("disabled-sink", result.ClientSecret);
            Assert.Empty(_httpHandler.SentRequests);
        }

        [Fact]
        public async Task InitiatePaymentAsync_AuthFailure_ClassifiedAsPermanent()
        {
            var settings = new PaymentSettings
            {
                Paymob = new PaymobSettings
                {
                    SecretKey = "invalid_secret"
                }
            };

            _httpHandler.EnqueueResponse(HttpStatusCode.Unauthorized, @"{ ""detail"": ""Authentication credentials were not provided."" }");

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);
            var customer = new PaymentCustomer("Ahmed", "Ali", "ahmed@example.com", "+201000000000");
            var request = new PaymentRequest(Money.FromDecimal(10.00m, "EGP"), customer);

            var result = await gateway.InitiatePaymentAsync(request);

            Assert.False(result.IsSuccess);
            Assert.Equal(PaymentErrorClassification.Permanent, result.ErrorClassification);
            Assert.Contains("401", result.ErrorMessage);
        }

        [Fact]
        public async Task InitiatePaymentAsync_Gateway500Error_ClassifiedAsTransient()
        {
            var settings = new PaymentSettings
            {
                Paymob = new PaymobSettings
                {
                    SecretKey = "sec_key"
                }
            };

            _httpHandler.EnqueueResponse(HttpStatusCode.InternalServerError, @"{ ""error"": ""Server error"" }");

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);
            var customer = new PaymentCustomer("Ahmed", "Ali", "ahmed@example.com", "+201000000000");
            var request = new PaymentRequest(Money.FromDecimal(10.00m, "EGP"), customer);

            var result = await gateway.InitiatePaymentAsync(request);

            Assert.False(result.IsSuccess);
            Assert.Equal(PaymentErrorClassification.Transient, result.ErrorClassification);
        }

        [Fact]
        public async Task InitiatePaymentAsync_Timeout_ClassifiedAsTransient()
        {
            var settings = new PaymentSettings
            {
                Paymob = new PaymobSettings
                {
                    SecretKey = "sec_key"
                }
            };

            _httpHandler.EnqueueException(new HttpRequestException("Connection timed out after 30 seconds"));

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);
            var customer = new PaymentCustomer("Ahmed", "Ali", "ahmed@example.com", "+201000000000");
            var request = new PaymentRequest(Money.FromDecimal(10.00m, "EGP"), customer);

            var result = await gateway.InitiatePaymentAsync(request);

            Assert.False(result.IsSuccess);
            Assert.Equal(PaymentErrorClassification.Transient, result.ErrorClassification);
            Assert.Contains("timed out", result.ErrorMessage);
        }

        [Fact]
        public async Task InitiatePaymentAsync_Cancellation_ReturnsTransientFailure()
        {
            var settings = new PaymentSettings
            {
                Paymob = new PaymobSettings
                {
                    SecretKey = "sec_key"
                }
            };

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);
            var customer = new PaymentCustomer("Ahmed", "Ali", "ahmed@example.com", "+201000000000");
            var request = new PaymentRequest(Money.FromDecimal(10.00m, "EGP"), customer);

            var result = await gateway.InitiatePaymentAsync(request, cts.Token);

            Assert.False(result.IsSuccess);
            Assert.Equal(PaymentErrorClassification.Transient, result.ErrorClassification);
            Assert.Contains("cancelled", result.ErrorMessage);
        }

        [Fact]
        public async Task VerifyPaymentAsync_SuccessfulTransaction_MapsToSuccessfulStatus()
        {
            var settings = new PaymentSettings
            {
                Paymob = new PaymobSettings
                {
                    SecretKey = "sec_key"
                }
            };

            string txJson = @"{
                ""id"": 12345,
                ""success"": true,
                ""pending"": false,
                ""is_refunded"": false,
                ""is_voided"": false,
                ""amount_cents"": 50000,
                ""currency"": ""EGP"",
                ""order"": { ""id"": 999111 },
                ""source_data"": {
                    ""pan"": ""411111xxxxxx1111"",
                    ""type"": ""card"",
                    ""sub_type"": ""Visa""
                }
            }";

            _httpHandler.EnqueueResponse(HttpStatusCode.OK, txJson);

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);

            var result = await gateway.VerifyPaymentAsync("12345");

            Assert.True(result.IsSuccess);
            Assert.Equal(PaymentStatus.Successful, result.Status);
            Assert.Equal("12345", result.TransactionId);
            Assert.Equal("999111", result.OrderId);
            Assert.Equal(500.00m, result.Amount.Amount);
            Assert.Equal("EGP", result.Amount.Currency);
            Assert.Equal("card", result.PaymentMethod);
            Assert.Equal("411111xxxxxx1111", result.MaskedPan);
        }

        [Fact]
        public async Task VerifyPaymentAsync_PendingTransaction_MapsToPendingStatus()
        {
            var settings = new PaymentSettings { Paymob = new PaymobSettings { SecretKey = "sec_key" } };
            string txJson = @"{ ""id"": 123, ""success"": false, ""pending"": true, ""amount_cents"": 1000, ""currency"": ""EGP"" }";
            _httpHandler.EnqueueResponse(HttpStatusCode.OK, txJson);

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);
            var result = await gateway.VerifyPaymentAsync("123");

            Assert.False(result.IsSuccess);
            Assert.Equal(PaymentStatus.Pending, result.Status);
        }

        [Fact]
        public async Task VerifyPaymentAsync_DeclinedTransaction_MapsToFailedStatus()
        {
            var settings = new PaymentSettings { Paymob = new PaymobSettings { SecretKey = "sec_key" } };
            string txJson = @"{ ""id"": 123, ""success"": false, ""pending"": false, ""amount_cents"": 1000, ""currency"": ""EGP"" }";
            _httpHandler.EnqueueResponse(HttpStatusCode.OK, txJson);

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);
            var result = await gateway.VerifyPaymentAsync("123");

            Assert.False(result.IsSuccess);
            Assert.Equal(PaymentStatus.Failed, result.Status);
        }

        [Fact]
        public async Task VerifyPaymentAsync_RefundedTransaction_MapsToRefundedStatus()
        {
            var settings = new PaymentSettings { Paymob = new PaymobSettings { SecretKey = "sec_key" } };
            string txJson = @"{ ""id"": 123, ""success"": true, ""is_refunded"": true, ""amount_cents"": 1000, ""currency"": ""EGP"" }";
            _httpHandler.EnqueueResponse(HttpStatusCode.OK, txJson);

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);
            var result = await gateway.VerifyPaymentAsync("123");

            Assert.False(result.IsSuccess);
            Assert.Equal(PaymentStatus.Refunded, result.Status);
        }

        [Fact]
        public async Task VerifyPaymentAsync_VoidedTransaction_MapsToCancelledStatus()
        {
            var settings = new PaymentSettings { Paymob = new PaymobSettings { SecretKey = "sec_key" } };
            string txJson = @"{ ""id"": 123, ""success"": false, ""is_voided"": true, ""amount_cents"": 1000, ""currency"": ""EGP"" }";
            _httpHandler.EnqueueResponse(HttpStatusCode.OK, txJson);

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);
            var result = await gateway.VerifyPaymentAsync("123");

            Assert.False(result.IsSuccess);
            Assert.Equal(PaymentStatus.Cancelled, result.Status);
        }

        [Fact]
        public async Task VerifyPaymentAsync_GatewayHttpError_MapsToUnknownStatus()
        {
            var settings = new PaymentSettings { Paymob = new PaymobSettings { SecretKey = "sec_key" } };
            _httpHandler.EnqueueResponse(HttpStatusCode.BadGateway, "Bad Gateway");

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);
            var result = await gateway.VerifyPaymentAsync("123");

            Assert.False(result.IsSuccess);
            Assert.Equal(PaymentStatus.Unknown, result.Status);
            Assert.Contains("502", result.ErrorMessage);
        }

        [Fact]
        public async Task RefundAsync_SuccessfulRefund_ReturnsSuccess()
        {
            var settings = new PaymentSettings { Paymob = new PaymobSettings { SecretKey = "sec_key" } };
            string refundResponse = @"{ ""success"": true, ""id"": 998877 }";
            _httpHandler.EnqueueResponse(HttpStatusCode.OK, refundResponse);

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);
            var request = new RefundRequest("TX-12345", Money.FromDecimal(50.00m, "EGP"));

            var result = await gateway.RefundAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Equal("998877", result.RefundTransactionId);
            Assert.Equal("TX-12345", result.OriginalTransactionId);
            Assert.Equal(50.00m, result.RefundedAmount!.Value.Amount);
            Assert.Equal(PaymentStatus.Refunded, result.Status);
        }

        [Fact]
        public async Task RefundAsync_DeclinedRefund_ReturnsFailure()
        {
            var settings = new PaymentSettings { Paymob = new PaymobSettings { SecretKey = "sec_key" } };
            string refundResponse = @"{ ""success"": false }";
            _httpHandler.EnqueueResponse(HttpStatusCode.OK, refundResponse);

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);
            var request = new RefundRequest("TX-12345", Money.FromDecimal(50.00m, "EGP"));

            var result = await gateway.RefundAsync(request);

            Assert.False(result.IsSuccess);
            Assert.Equal(PaymentStatus.Failed, result.Status);
            Assert.Contains("declined", result.ErrorMessage);
        }

        [Fact]
        public async Task ProcessWebhookAsync_ValidHmac_ParsesEvent()
        {
            string hmacSecret = "test_hmac_secret";
            var settings = new PaymentSettings
            {
                Paymob = new PaymobSettings
                {
                    HmacSecret = hmacSecret,
                    DefaultCurrency = "EGP"
                }
            };

            string json = @"{
                ""type"": ""TRANSACTION"",
                ""obj"": {
                    ""id"": 7654321,
                    ""pending"": false,
                    ""amount_cents"": 25000,
                    ""success"": true,
                    ""is_auth"": false,
                    ""is_capture"": false,
                    ""is_standalone_payment"": true,
                    ""is_voided"": false,
                    ""is_refunded"": false,
                    ""is_3d_secure"": true,
                    ""integration_id"": 1122,
                    ""order"": { ""id"": 9988 },
                    ""created_at"": ""2026-11-17T12:00:00.000000"",
                    ""currency"": ""EGP"",
                    ""source_data"": { ""pan"": ""9999"", ""sub_type"": ""Visa"", ""type"": ""card"" },
                    ""has_parent_transaction"": false,
                    ""owner"": ""1234"",
                    ""error_occured"": false
                }
            }";

            using var doc = JsonDocument.Parse(json);
            string concatenated = PaymobHmacValidator.BuildConcatenatedString(doc.RootElement.GetProperty("obj"));
            string validSignature = PaymobHmacValidator.ComputeHmacSha512(concatenated, hmacSecret);

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);

            var webhookEvent = await gateway.ProcessWebhookAsync(json, validSignature);

            Assert.NotNull(webhookEvent);
            Assert.Equal("TRANSACTION", webhookEvent!.EventType);
            Assert.Equal("7654321", webhookEvent.TransactionId);
            Assert.Equal("9988", webhookEvent.OrderId);
            Assert.Equal(PaymentStatus.Successful, webhookEvent.Status);
            Assert.Equal(250.00m, webhookEvent.Amount.Amount);
            Assert.Equal("9999", webhookEvent.MaskedPan);
            Assert.Equal("Visa", webhookEvent.PaymentMethod);
        }

        [Fact]
        public async Task ProcessWebhookAsync_InvalidHmac_ReturnsNull()
        {
            var settings = new PaymentSettings
            {
                Paymob = new PaymobSettings
                {
                    HmacSecret = "secret",
                    DefaultCurrency = "EGP"
                }
            };

            string json = @"{ ""obj"": { ""id"": 123 } }";
            string badSignature = "invalid_hmac_hex";

            using var gateway = new PaymobPaymentGateway(settings, _httpClient);

            var webhookEvent = await gateway.ProcessWebhookAsync(json, badSignature);

            Assert.Null(webhookEvent);
        }
    }
}
