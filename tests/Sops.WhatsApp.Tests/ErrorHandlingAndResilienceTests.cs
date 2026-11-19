using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Sops.WhatsApp.Abstractions;
using Sops.WhatsApp.Configuration;
using Sops.WhatsApp.Models;
using Sops.WhatsApp.Providers.MetaCloud;
using Xunit;

namespace Sops.WhatsApp.Tests
{
    public class ErrorHandlingAndResilienceTests
    {
        private readonly WhatsAppSettings _settings = new WhatsAppSettings
        {
            Provider = "MetaCloud",
            Enabled = true,
            BaseUrl = "https://graph.facebook.com",
            GraphApiVersion = "v21.0",
            PhoneNumberId = "102553952458",
            AccessToken = "EAABtest_token_123"
        };

        [Fact]
        public async Task PostMessage_Meta400Error_ClassifiedAsPermanent()
        {
            var handler = new TestHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.BadRequest, @"{
                ""error"": {
                    ""message"": ""(#100) Invalid parameter to"",
                    ""type"": ""OAuthException"",
                    ""code"": 100
                }
            }");

            using var httpClient = new HttpClient(handler);
            using var client = new MetaCloudWhatsAppClient(_settings, httpClient);

            var request = new WhatsAppTextMessageRequest
            {
                To = "+201001234567",
                Body = "Test"
            };

            var result = await client.SendTextMessageAsync(request);

            Assert.False(result.IsSuccess);
            Assert.Equal(WhatsAppErrorClassification.Permanent, result.ErrorClassification);
            Assert.Equal("(#100) Invalid parameter to", result.ErrorMessage);
            Assert.Equal(100, result.ErrorCode);
            Assert.Equal(WhatsAppDeliveryStatus.Failed, result.Status);
        }

        [Fact]
        public async Task PostMessage_Meta429RateLimit_ClassifiedAsTransient()
        {
            var handler = new TestHttpMessageHandler();
            handler.EnqueueResponse((HttpStatusCode)429, @"{
                ""error"": {
                    ""message"": ""Rate limit hit"",
                    ""code"": 80007
                }
            }");

            using var httpClient = new HttpClient(handler);
            using var client = new MetaCloudWhatsAppClient(_settings, httpClient);

            var request = new WhatsAppTextMessageRequest
            {
                To = "+201001234567",
                Body = "Test rate limit"
            };

            var result = await client.SendTextMessageAsync(request);

            Assert.False(result.IsSuccess);
            Assert.Equal(WhatsAppErrorClassification.Transient, result.ErrorClassification);
            Assert.Equal(80007, result.ErrorCode);
        }

        [Fact]
        public async Task PostMessage_Meta500InternalServerError_ClassifiedAsTransient()
        {
            var handler = new TestHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.InternalServerError, @"{
                ""error"": {
                    ""message"": ""Temporary server failure"",
                    ""code"": 2
                }
            }");

            using var httpClient = new HttpClient(handler);
            using var client = new MetaCloudWhatsAppClient(_settings, httpClient);

            var request = new WhatsAppTextMessageRequest
            {
                To = "+201001234567",
                Body = "Test 500"
            };

            var result = await client.SendTextMessageAsync(request);

            Assert.False(result.IsSuccess);
            Assert.Equal(WhatsAppErrorClassification.Transient, result.ErrorClassification);
        }

        [Fact]
        public async Task PostMessage_NetworkTimeout_ClassifiedAsAmbiguous()
        {
            var handler = new TestHttpMessageHandler();
            // Simulate timeout where task was canceled internally by HttpClient timeout
            handler.EnqueueException(new TaskCanceledException("HttpClient timeout"));

            using var httpClient = new HttpClient(handler);
            using var client = new MetaCloudWhatsAppClient(_settings, httpClient);

            var request = new WhatsAppTextMessageRequest
            {
                To = "+201001234567",
                Body = "Test timeout"
            };

            var result = await client.SendTextMessageAsync(request, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(WhatsAppErrorClassification.Ambiguous, result.ErrorClassification);
            Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task PostMessage_CallerCancellation_PropagatesCancellation()
        {
            var handler = new TestHttpMessageHandler();
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-canceled token

            using var httpClient = new HttpClient(handler);
            using var client = new MetaCloudWhatsAppClient(_settings, httpClient);

            var request = new WhatsAppTextMessageRequest
            {
                To = "+201001234567",
                Body = "Test cancel"
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.SendTextMessageAsync(request, cts.Token));
        }
    }
}
