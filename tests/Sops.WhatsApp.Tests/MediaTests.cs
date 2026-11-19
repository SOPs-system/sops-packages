using System;
using System.IO;
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
    public class MediaTests
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
        public async Task SendMediaAsync_ImageById_SerializesCorrectPayload()
        {
            var handler = new TestHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, @"{
                ""messages"": [{ ""id"": ""wamid.media.1"" }]
            }");

            using var httpClient = new HttpClient(handler);
            using var client = new MetaCloudWhatsAppClient(_settings, httpClient);

            var request = new WhatsAppMediaMessageRequest
            {
                To = "+201001234567",
                MediaType = WhatsAppMediaType.Image,
                MediaId = "meta_media_999",
                Caption = "Receipt Photo"
            };

            var result = await client.SendMediaAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Equal("wamid.media.1", result.MessageId);

            string body = handler.SentRequestBodies[0];
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            Assert.Equal("image", root.GetProperty("type").GetString());
            var imageObj = root.GetProperty("image");
            Assert.Equal("meta_media_999", imageObj.GetProperty("id").GetString());
            Assert.Equal("Receipt Photo", imageObj.GetProperty("caption").GetString());
        }

        [Fact]
        public async Task SendMediaAsync_DocumentByLink_SerializesCorrectPayload()
        {
            var handler = new TestHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, @"{
                ""messages"": [{ ""id"": ""wamid.doc.1"" }]
            }");

            using var httpClient = new HttpClient(handler);
            using var client = new MetaCloudWhatsAppClient(_settings, httpClient);

            var request = new WhatsAppMediaMessageRequest
            {
                To = "+201001234567",
                MediaType = WhatsAppMediaType.Document,
                Link = "https://cdn.example.com/docs/order_1001.pdf",
                Filename = "Order_1001.pdf",
                Caption = "Order Confirmation Document"
            };

            var result = await client.SendMediaAsync(request);

            Assert.True(result.IsSuccess);

            string body = handler.SentRequestBodies[0];
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            Assert.Equal("document", root.GetProperty("type").GetString());
            var docObj = root.GetProperty("document");
            Assert.Equal("https://cdn.example.com/docs/order_1001.pdf", docObj.GetProperty("link").GetString());
            Assert.Equal("Order_1001.pdf", docObj.GetProperty("filename").GetString());
            Assert.Equal("Order Confirmation Document", docObj.GetProperty("caption").GetString());
        }

        [Fact]
        public void SendMediaAsync_MissingMediaIdAndLink_ThrowsArgumentException()
        {
            var request = new WhatsAppMediaMessageRequest
            {
                To = "+201001234567",
                MediaType = WhatsAppMediaType.Image,
                MediaId = null,
                Link = null
            };

            var ex = Assert.Throws<ArgumentException>(() => request.Validate());
            Assert.Equal("MediaId", ex.ParamName);
        }

        [Fact]
        public async Task UploadMediaAsync_ValidBytes_SendsMultipartAndReturnsMediaId()
        {
            var handler = new TestHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, @"{
                ""id"": ""uploaded_media_id_777""
            }");

            using var httpClient = new HttpClient(handler);
            using var client = new MetaCloudWhatsAppClient(_settings, httpClient);

            byte[] dummyPdf = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
            var request = new WhatsAppMediaUploadRequest
            {
                FileName = "report.pdf",
                MimeType = "application/pdf",
                ContentBytes = dummyPdf
            };

            var result = await client.UploadMediaAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Equal("uploaded_media_id_777", result.MediaId);
            Assert.Equal(WhatsAppErrorClassification.None, result.ErrorClassification);

            Assert.Single(handler.SentRequests);
            var sentRequest = handler.SentRequests[0];
            Assert.Equal(HttpMethod.Post, sentRequest.Method);
            Assert.Equal("https://graph.facebook.com/v21.0/102553952458/media", sentRequest.RequestUri?.ToString());
        }

        [Fact]
        public async Task UploadMediaAsync_ErrorFromMeta_ReturnsFailure()
        {
            var handler = new TestHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.BadRequest, @"{
                ""error"": {
                    ""message"": ""Unsupported mime type"",
                    ""code"": 100
                }
            }");

            using var httpClient = new HttpClient(handler);
            using var client = new MetaCloudWhatsAppClient(_settings, httpClient);

            var request = new WhatsAppMediaUploadRequest
            {
                FileName = "test.xyz",
                MimeType = "application/unknown",
                ContentBytes = new byte[] { 1, 2, 3 }
            };

            var result = await client.UploadMediaAsync(request);

            Assert.False(result.IsSuccess);
            Assert.Equal("Unsupported mime type", result.ErrorMessage);
            Assert.Equal(WhatsAppErrorClassification.Permanent, result.ErrorClassification);
        }

        [Fact]
        public async Task GetMediaMetadataAsync_Found_ReturnsMetadata()
        {
            var handler = new TestHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.OK, @"{
                ""messaging_product"": ""whatsapp"",
                ""url"": ""https://lookaside.fbsbx.com/v/t24/file.pdf"",
                ""mime_type"": ""application/pdf"",
                ""sha256"": ""e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"",
                ""file_size"": 1048576,
                ""id"": ""meta_media_777""
            }");

            using var httpClient = new HttpClient(handler);
            using var client = new MetaCloudWhatsAppClient(_settings, httpClient);

            var metadata = await client.GetMediaMetadataAsync("meta_media_777");

            Assert.NotNull(metadata);
            Assert.Equal("meta_media_777", metadata!.Id);
            Assert.Equal("application/pdf", metadata.MimeType);
            Assert.Equal(1048576, metadata.FileSize);
            Assert.Equal("https://lookaside.fbsbx.com/v/t24/file.pdf", metadata.Url);
        }

        [Fact]
        public async Task GetMediaMetadataAsync_NotFound_ReturnsNull()
        {
            var handler = new TestHttpMessageHandler();
            handler.EnqueueResponse(HttpStatusCode.NotFound, "{}");

            using var httpClient = new HttpClient(handler);
            using var client = new MetaCloudWhatsAppClient(_settings, httpClient);

            var metadata = await client.GetMediaMetadataAsync("nonexistent_id");
            Assert.Null(metadata);
        }
    }
}
