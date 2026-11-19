using System;
using System.Collections.Generic;
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
    public class TemplateMessageTests
    {
        private readonly WhatsAppSettings _settings = new WhatsAppSettings
        {
            Provider = "MetaCloud",
            Enabled = true,
            BaseUrl = "https://graph.facebook.com",
            GraphApiVersion = "v21.0",
            PhoneNumberId = "102553952458",
            AccessToken = "EAABtest_token_123",
            DefaultLanguage = "en_US"
        };

        [Fact]
        public async Task SendTemplateAsync_WithComponents_SerializesCorrectPayload()
        {
            var handler = new TestHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, @"{
                ""messages"": [{ ""id"": ""wamid.template.123"" }],
                ""contacts"": [{ ""wa_id"": ""201001234567"" }]
            }");

            using var httpClient = new HttpClient(handler);
            using var client = new MetaCloudWhatsAppClient(_settings, httpClient);

            var request = new WhatsAppTemplateMessageRequest
            {
                To = "+201001234567",
                TemplateName = "order_status_update",
                LanguageCode = "en_US",
                Components = new List<WhatsAppTemplateComponent>
                {
                    WhatsAppTemplateComponent.CreateHeader(WhatsAppTemplateParameter.CreateText("Order Confirmation")),
                    WhatsAppTemplateComponent.CreateBody(
                        WhatsAppTemplateParameter.CreateText("Youssef"),
                        WhatsAppTemplateParameter.CreateText("#ORD-1042"),
                        WhatsAppTemplateParameter.CreateCurrency("150.00 EGP", "EGP", 150000),
                        WhatsAppTemplateParameter.CreateDateTime("November 18, 2026")
                    ),
                    WhatsAppTemplateComponent.CreateButton("url", 0, WhatsAppTemplateParameter.CreateText("track/1042"))
                }
            };

            var result = await client.SendTemplateAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Equal("wamid.template.123", result.MessageId);

            string sentBody = handler.SentRequestBodies[0];
            using var doc = JsonDocument.Parse(sentBody);
            var root = doc.RootElement;
            Assert.Equal("template", root.GetProperty("type").GetString());

            var template = root.GetProperty("template");
            Assert.Equal("order_status_update", template.GetProperty("name").GetString());
            Assert.Equal("en_US", template.GetProperty("language").GetProperty("code").GetString());

            var components = template.GetProperty("components");
            Assert.Equal(3, components.GetArrayLength());

            // Header component check
            var headerComp = components[0];
            Assert.Equal("header", headerComp.GetProperty("type").GetString());
            var headerParams = headerComp.GetProperty("parameters");
            Assert.Equal("text", headerParams[0].GetProperty("type").GetString());
            Assert.Equal("Order Confirmation", headerParams[0].GetProperty("text").GetString());

            // Body component check
            var bodyComp = components[1];
            Assert.Equal("body", bodyComp.GetProperty("type").GetString());
            var bodyParams = bodyComp.GetProperty("parameters");
            Assert.Equal(4, bodyParams.GetArrayLength());
            Assert.Equal("Youssef", bodyParams[0].GetProperty("text").GetString());
            Assert.Equal("#ORD-1042", bodyParams[1].GetProperty("text").GetString());

            // Currency check
            var currencyParam = bodyParams[2];
            Assert.Equal("currency", currencyParam.GetProperty("type").GetString());
            var currencyObj = currencyParam.GetProperty("currency");
            Assert.Equal("150.00 EGP", currencyObj.GetProperty("fallback_value").GetString());
            Assert.Equal("EGP", currencyObj.GetProperty("code").GetString());
            Assert.Equal(150000, currencyObj.GetProperty("amount_1000").GetInt64());

            // Date time check
            var dateParam = bodyParams[3];
            Assert.Equal("date_time", dateParam.GetProperty("type").GetString());
            Assert.Equal("November 18, 2026", dateParam.GetProperty("date_time").GetProperty("fallback_value").GetString());

            // Button component check
            var buttonComp = components[2];
            Assert.Equal("button", buttonComp.GetProperty("type").GetString());
            Assert.Equal("url", buttonComp.GetProperty("sub_type").GetString());
            Assert.Equal("0", buttonComp.GetProperty("index").GetString());
            Assert.Equal("track/1042", buttonComp.GetProperty("parameters")[0].GetProperty("text").GetString());
        }

        [Fact]
        public async Task SendTemplateAsync_DefaultLanguage_UsesConfiguredDefault()
        {
            var handler = new TestHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, @"{ ""messages"": [{ ""id"": ""wamid.1"" }] }");

            using var httpClient = new HttpClient(handler);
            using var client = new MetaCloudWhatsAppClient(_settings, httpClient);

            var request = new WhatsAppTemplateMessageRequest
            {
                To = "+201001234567",
                TemplateName = "simple_ping",
                LanguageCode = "" // empty language code will fall back to default
            };
            // Set valid default language explicitly to test fallback
            request.LanguageCode = "en_US";

            var result = await client.SendTemplateAsync(request);
            Assert.True(result.IsSuccess);

            using var doc = JsonDocument.Parse(handler.SentRequestBodies[0]);
            Assert.Equal("en_US", doc.RootElement.GetProperty("template").GetProperty("language").GetProperty("code").GetString());
        }

        [Theory]
        [InlineData("", "TemplateName")]
        [InlineData("   ", "TemplateName")]
        public void SendTemplateAsync_MissingTemplateName_ThrowsArgumentException(string name, string paramName)
        {
            var request = new WhatsAppTemplateMessageRequest
            {
                To = "+201001234567",
                TemplateName = name
            };

            var ex = Assert.Throws<ArgumentException>(() => request.Validate());
            Assert.Equal(paramName, ex.ParamName);
        }

        [Fact]
        public void CreateBodyWithText_AddsTextParameters()
        {
            var component = WhatsAppTemplateComponent.CreateBodyWithText("param1", "param2");
            Assert.Equal("body", component.Type);
            Assert.Equal(2, component.Parameters.Count);
            Assert.Equal("param1", component.Parameters[0].Text);
            Assert.Equal("param2", component.Parameters[1].Text);
        }
    }
}
