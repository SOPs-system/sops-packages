using System;
using System.Text.Json;
using Sops.WhatsApp.Abstractions;
using Sops.WhatsApp.Configuration;
using Sops.WhatsApp.Providers.MetaCloud;
using Xunit;

namespace Sops.WhatsApp.Tests
{
    public class WebhookProcessingTests
    {
        private readonly MetaCloudWhatsAppClient _client = new MetaCloudWhatsAppClient(new WhatsAppSettings());

        [Fact]
        public void ProcessWebhook_IncomingTextMessage_ParsesFieldsCorrectly()
        {
            string payload = @"{
              ""object"": ""whatsapp_business_account"",
              ""entry"": [
                {
                  ""id"": ""100000000000"",
                  ""changes"": [
                    {
                      ""field"": ""messages"",
                      ""value"": {
                        ""messaging_product"": ""whatsapp"",
                        ""metadata"": {
                          ""display_phone_number"": ""15550254583"",
                          ""phone_number_id"": ""102553952458""
                        },
                        ""contacts"": [
                          {
                            ""profile"": { ""name"": ""Youssef"" },
                            ""wa_id"": ""201001234567""
                          }
                        ],
                        ""messages"": [
                          {
                            ""from"": ""201001234567"",
                            ""id"": ""wamid.HBgLMjAxMDAxMjM0NTY3FQIAERgSRjQ1NjcwQkQyRDc0QzUzRjQA"",
                            ""timestamp"": ""1763412000"",
                            ""type"": ""text"",
                            ""text"": {
                              ""body"": ""Where is my package?""
                            }
                          }
                        ]
                      }
                    }
                  ]
                }
              ]
            }";

            var evt = _client.ProcessWebhook(payload);

            Assert.Equal("100000000000", evt.BusinessAccountId);
            Assert.Equal("102553952458", evt.PhoneNumberId);
            Assert.Equal("15550254583", evt.DisplayPhoneNumber);

            Assert.Single(evt.IncomingMessages);
            var msg = evt.IncomingMessages[0];
            Assert.Equal("wamid.HBgLMjAxMDAxMjM0NTY3FQIAERgSRjQ1NjcwQkQyRDc0QzUzRjQA", msg.Id);
            Assert.Equal("201001234567", msg.From);
            Assert.Equal("text", msg.Type);
            Assert.Equal("Where is my package?", msg.TextBody);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1763412000), msg.Timestamp);
        }

        [Fact]
        public void ProcessWebhook_StatusUpdate_ParsesDeliveredAndFailed()
        {
            string payload = @"{
              ""object"": ""whatsapp_business_account"",
              ""entry"": [
                {
                  ""id"": ""100000000000"",
                  ""changes"": [
                    {
                      ""field"": ""messages"",
                      ""value"": {
                        ""messaging_product"": ""whatsapp"",
                        ""metadata"": {
                          ""phone_number_id"": ""102553952458""
                        },
                        ""statuses"": [
                          {
                            ""id"": ""wamid.msg1"",
                            ""status"": ""delivered"",
                            ""timestamp"": ""1763412010"",
                            ""recipient_id"": ""201001234567""
                          },
                          {
                            ""id"": ""wamid.msg2"",
                            ""status"": ""failed"",
                            ""timestamp"": ""1763412020"",
                            ""recipient_id"": ""201009999999"",
                            ""errors"": [
                              {
                                ""code"": 131026,
                                ""title"": ""Message undeliverable"",
                                ""message"": ""Could not deliver message"",
                                ""error_data"": {
                                  ""details"": ""User phone is turned off or not on WhatsApp""
                                }
                              }
                            ]
                          }
                        ]
                      }
                    }
                  ]
                }
              ]
            }";

            var evt = _client.ProcessWebhook(payload);

            Assert.Equal(2, evt.StatusUpdates.Count);

            var delivered = evt.StatusUpdates[0];
            Assert.Equal("wamid.msg1", delivered.MessageId);
            Assert.Equal(WhatsAppDeliveryStatus.Delivered, delivered.Status);
            Assert.Equal("201001234567", delivered.RecipientId);
            Assert.Empty(delivered.Errors);

            var failed = evt.StatusUpdates[1];
            Assert.Equal("wamid.msg2", failed.MessageId);
            Assert.Equal(WhatsAppDeliveryStatus.Failed, failed.Status);
            Assert.Equal("201009999999", failed.RecipientId);
            Assert.Single(failed.Errors);

            var error = failed.Errors[0];
            Assert.Equal(131026, error.Code);
            Assert.Equal("Message undeliverable", error.Title);
            Assert.Equal("Could not deliver message", error.Message);
            Assert.Equal("User phone is turned off or not on WhatsApp", error.Details);
        }

        [Fact]
        public void ProcessWebhook_IncomingMediaMessage_ParsesMediaReference()
        {
            string payload = @"{
              ""object"": ""whatsapp_business_account"",
              ""entry"": [
                {
                  ""changes"": [
                    {
                      ""value"": {
                        ""messages"": [
                          {
                            ""from"": ""201001234567"",
                            ""id"": ""wamid.image.1"",
                            ""timestamp"": ""1763412000"",
                            ""type"": ""image"",
                            ""image"": {
                              ""id"": ""media_id_555"",
                              ""caption"": ""Damage Photo""
                            }
                          }
                        ]
                      }
                    }
                  ]
                }
              ]
            }";

            var evt = _client.ProcessWebhook(payload);

            Assert.Single(evt.IncomingMessages);
            var msg = evt.IncomingMessages[0];
            Assert.Equal("image", msg.Type);
            Assert.NotNull(msg.Media);
            Assert.Equal("media_id_555", msg.Media!.Id);
            Assert.Equal("Damage Photo", msg.Media.Caption);
        }

        [Fact]
        public void ProcessWebhook_EmptyOrInvalidJson_ThrowsException()
        {
            Assert.Throws<ArgumentException>(() => _client.ProcessWebhook(""));
            Assert.ThrowsAny<JsonException>(() => _client.ProcessWebhook("{ not-json }"));
        }
    }
}
