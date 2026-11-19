using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Sops.WhatsApp.Abstractions;
using Sops.WhatsApp.Configuration;
using Sops.WhatsApp.Models;
using Sops.WhatsApp.Providers.MetaCloud;
using Xunit;

namespace Sops.WhatsApp.Tests
{
    public class TextMessageTests
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
        public async Task SendTextMessageAsync_ValidRequest_SendsCorrectPayloadAndReturnsSuccess()
        {
            var handler = new TestHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, @"{
                ""messaging_product"": ""whatsapp"",
                ""contacts"": [{ ""input"": ""+201001234567"", ""wa_id"": ""201001234567"" }],
                ""messages"": [{ ""id"": ""wamid.HBgLMjAxMDAxMjM0NTY3FQIAERgSRjQ1NjcwQkQyRDc0QzUzRjQA"" }]
            }");

            using var httpClient = new HttpClient(handler);
            using var client = new MetaCloudWhatsAppClient(_settings, httpClient);

            var request = new WhatsAppTextMessageRequest
            {
                To = "+201001234567",
                Body = "Welcome to SOPs Inventory System!",
                PreviewUrl = true
            };

            var result = await client.SendTextMessageAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Equal("wamid.HBgLMjAxMDAxMjM0NTY3FQIAERgSRjQ1NjcwQkQyRDc0QzUzRjQA", result.MessageId);
            Assert.Equal("201001234567", result.RecipientWaId);
            Assert.Equal(WhatsAppDeliveryStatus.Sent, result.Status);
            Assert.Equal(WhatsAppErrorClassification.None, result.ErrorClassification);

            // Verify HTTP call details
            Assert.Single(handler.SentRequests);
            var sentRequest = handler.SentRequests[0];
            Assert.Equal(HttpMethod.Post, sentRequest.Method);
            Assert.Equal("https://graph.facebook.com/v21.0/102553952458/messages", sentRequest.RequestUri?.ToString());
            Assert.Equal("Bearer", sentRequest.Headers.Authorization?.Scheme);
            Assert.Equal("EAABtest_token_123", sentRequest.Headers.Authorization?.Parameter);

            // Verify JSON body
            string sentBody = handler.SentRequestBodies[0];
            using var doc = JsonDocument.Parse(sentBody);
            var root = doc.RootElement;
            Assert.Equal("whatsapp", root.GetProperty("messaging_product").GetString());
            Assert.Equal("individual", root.GetProperty("recipient_type").GetString());
            Assert.Equal("201001234567", root.GetProperty("to").GetString());
            Assert.Equal("text", root.GetProperty("type").GetString());

            var textObj = root.GetProperty("text");
            Assert.Equal("Welcome to SOPs Inventory System!", textObj.GetProperty("body").GetString());
            Assert.True(textObj.GetProperty("preview_url").GetBoolean());
        }

        [Fact]
        public async Task SendTextMessageAsync_NormalizesPhoneNumber()
        {
            var handler = new TestHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, @"{
                ""messages"": [{ ""id"": ""wamid.123"" }]
            }");

            using var httpClient = new HttpClient(handler);
            using var client = new MetaCloudWhatsAppClient(_settings, httpClient);

            var request = new WhatsAppTextMessageRequest
            {
                To = "+1 (555) 025-4583",
                Body = "Test normalization"
            };

            var result = await client.SendTextMessageAsync(request);
            Assert.True(result.IsSuccess);

            using var doc = JsonDocument.Parse(handler.SentRequestBodies[0]);
            Assert.Equal("15550254583", doc.RootElement.GetProperty("to").GetString());
        }

        [Fact]
        public async Task SendTextMessageAsync_WhenDisabled_ReturnsFailureWithoutHttpCall()
        {
            var handler = new TestHttpMessageHandler();
            using var httpClient = new HttpClient(handler);

            var disabledSettings = new WhatsAppSettings { Enabled = false };
            using var client = new MetaCloudWhatsAppClient(disabledSettings, httpClient);

            var request = new WhatsAppTextMessageRequest
            {
                To = "+201001234567",
                Body = "Disabled test"
            };

            var result = await client.SendTextMessageAsync(request);

            Assert.False(result.IsSuccess);
            Assert.Equal(WhatsAppDeliveryStatus.Failed, result.Status);
            Assert.Equal(WhatsAppErrorClassification.Permanent, result.ErrorClassification);
            Assert.Empty(handler.SentRequests);
        }

        [Theory]
        [InlineData("", "To")]
        [InlineData("   ", "To")]
        public void SendTextMessageAsync_InvalidTo_ThrowsArgumentException(string invalidTo, string expectedParam)
        {
            var request = new WhatsAppTextMessageRequest
            {
                To = invalidTo,
                Body = "Hello"
            };

            var ex = Assert.Throws<ArgumentException>(() => request.Validate());
            Assert.Equal(expectedParam, ex.ParamName);
        }

        [Theory]
        [InlineData("", "Body")]
        [InlineData("   ", "Body")]
        public void SendTextMessageAsync_InvalidBody_ThrowsArgumentException(string invalidBody, string expectedParam)
        {
            var request = new WhatsAppTextMessageRequest
            {
                To = "+201001234567",
                Body = invalidBody
            };

            var ex = Assert.Throws<ArgumentException>(() => request.Validate());
            Assert.Equal(expectedParam, ex.ParamName);
        }

        [Fact]
        public void SendTextMessageAsync_BodyExceeding4096Chars_ThrowsArgumentException()
        {
            string longBody = new string('A', 4097);
            var request = new WhatsAppTextMessageRequest
            {
                To = "+201001234567",
                Body = longBody
            };

            var ex = Assert.Throws<ArgumentException>(() => request.Validate());
            Assert.Equal("Body", ex.ParamName);
        }
    }
}
