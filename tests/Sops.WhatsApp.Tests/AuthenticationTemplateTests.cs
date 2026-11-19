using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sops.WhatsApp.Configuration;
using Sops.WhatsApp.Models;
using Sops.WhatsApp.Providers.MetaCloud;
using Xunit;

namespace Sops.WhatsApp.Tests
{
    public class AuthenticationTemplateTests
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
        public async Task SendAuthenticationOtpTemplate_FormatsPayloadCorrectly()
        {
            var handler = new TestHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, @"{
                ""messages"": [{ ""id"": ""wamid.otp.123"" }]
            }");

            using var httpClient = new HttpClient(handler);
            var logger = Substitute.For<ILogger<MetaCloudWhatsAppClient>>();
            using var client = new MetaCloudWhatsAppClient(_settings, httpClient, logger);

            string secretOtpCode = "749201";

            var request = new WhatsAppTemplateMessageRequest
            {
                To = "+201001234567",
                TemplateName = "authentication_otp",
                LanguageCode = "en_US",
                Components = new List<WhatsAppTemplateComponent>
                {
                    WhatsAppTemplateComponent.CreateOtpBody(secretOtpCode),
                    WhatsAppTemplateComponent.CreateOtpButton(index: 0, otpCode: secretOtpCode)
                }
            };

            var result = await client.SendTemplateAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Equal("wamid.otp.123", result.MessageId);

            // Inspect serialized JSON
            string sentJson = handler.SentRequestBodies[0];
            using var doc = JsonDocument.Parse(sentJson);
            var components = doc.RootElement.GetProperty("template").GetProperty("components");

            // Verify body OTP parameter
            var bodyComp = components[0];
            Assert.Equal("body", bodyComp.GetProperty("type").GetString());
            Assert.Equal(secretOtpCode, bodyComp.GetProperty("parameters")[0].GetProperty("text").GetString());

            // Verify button OTP parameter
            var buttonComp = components[1];
            Assert.Equal("button", buttonComp.GetProperty("type").GetString());
            Assert.Equal("url", buttonComp.GetProperty("sub_type").GetString());
            Assert.Equal("0", buttonComp.GetProperty("index").GetString());
            Assert.Equal(secretOtpCode, buttonComp.GetProperty("parameters")[0].GetProperty("text").GetString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void CreateOtpBody_InvalidCode_ThrowsArgumentException(string? invalidCode)
        {
            Assert.Throws<ArgumentException>(() => WhatsAppTemplateComponent.CreateOtpBody(invalidCode!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void CreateOtpButton_InvalidCode_ThrowsArgumentException(string? invalidCode)
        {
            Assert.Throws<ArgumentException>(() => WhatsAppTemplateComponent.CreateOtpButton(0, invalidCode!));
        }
    }
}
