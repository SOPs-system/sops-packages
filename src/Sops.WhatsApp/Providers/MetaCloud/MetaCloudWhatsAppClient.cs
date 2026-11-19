using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sops.WhatsApp.Abstractions;
using Sops.WhatsApp.Configuration;
using Sops.WhatsApp.Models;
using Sops.WhatsApp.Security;

namespace Sops.WhatsApp.Providers.MetaCloud
{
    /// <summary>
    /// Production-ready client for Meta's official WhatsApp Business Platform Cloud API.
    /// Handles text messages, pre-approved templates (including authentication/OTP), media messages,
    /// media uploads, metadata inspection, and cryptographic webhook verification.
    /// </summary>
    public class MetaCloudWhatsAppClient : IWhatsAppClient, IDisposable
    {
        private readonly WhatsAppSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly ILogger<MetaCloudWhatsAppClient>? _logger;
        private readonly bool _ownsHttpClient;
        private bool _disposed;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <inheritdoc />
        public string ProviderName => "MetaCloud";

        /// <summary>
        /// Initializes a new instance of the <see cref="MetaCloudWhatsAppClient"/> class using configured options.
        /// </summary>
        /// <param name="options">The configured WhatsApp settings.</param>
        /// <param name="httpClient">Optional external HttpClient instance.</param>
        /// <param name="logger">Optional logger instance.</param>
        public MetaCloudWhatsAppClient(
            IOptions<WhatsAppSettings> options,
            HttpClient? httpClient = null,
            ILogger<MetaCloudWhatsAppClient>? logger = null)
            : this(options?.Value ?? throw new ArgumentNullException(nameof(options)), httpClient, logger)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MetaCloudWhatsAppClient"/> class using direct settings.
        /// </summary>
        /// <param name="settings">The WhatsApp settings.</param>
        /// <param name="httpClient">Optional external HttpClient instance.</param>
        /// <param name="logger">Optional logger instance.</param>
        public MetaCloudWhatsAppClient(
            WhatsAppSettings settings,
            HttpClient? httpClient = null,
            ILogger<MetaCloudWhatsAppClient>? logger = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger;

            if (httpClient == null)
            {
                _httpClient = new HttpClient();
                _ownsHttpClient = true;
            }
            else
            {
                _httpClient = httpClient;
                _ownsHttpClient = false;
            }

            if (_settings.TimeoutSeconds > 0)
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
            }
        }

        /// <inheritdoc />
        public async Task<WhatsAppMessageResult> SendTextMessageAsync(
            WhatsAppTextMessageRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            request.Validate();

            if (!_settings.Enabled)
            {
                _logger?.LogWarning("WhatsApp messaging is disabled in configuration. Text message was not dispatched.");
                return WhatsAppMessageResult.Failure(
                    "WhatsApp messaging is disabled in configuration.",
                    WhatsAppErrorClassification.Permanent);
            }

            string normalizedTo = WhatsAppSecurityHelper.NormalizePhoneNumber(request.To);
            string maskedTo = WhatsAppSecurityHelper.MaskPhoneNumber(normalizedTo);
            _logger?.LogInformation("Dispatching WhatsApp text message to recipient {Recipient}", maskedTo);

            var payload = new Dictionary<string, object>
            {
                ["messaging_product"] = "whatsapp",
                ["recipient_type"] = "individual",
                ["to"] = normalizedTo,
                ["type"] = "text",
                ["text"] = new Dictionary<string, object>
                {
                    ["body"] = request.Body,
                    ["preview_url"] = request.PreviewUrl
                }
            };

            return await PostMessagePayloadAsync(payload, maskedTo, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<WhatsAppMessageResult> SendTemplateAsync(
            WhatsAppTemplateMessageRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            request.Validate();

            if (!_settings.Enabled)
            {
                _logger?.LogWarning("WhatsApp messaging is disabled in configuration. Template message was not dispatched.");
                return WhatsAppMessageResult.Failure(
                    "WhatsApp messaging is disabled in configuration.",
                    WhatsAppErrorClassification.Permanent);
            }

            string normalizedTo = WhatsAppSecurityHelper.NormalizePhoneNumber(request.To);
            string maskedTo = WhatsAppSecurityHelper.MaskPhoneNumber(normalizedTo);
            _logger?.LogInformation("Dispatching WhatsApp template '{Template}' to recipient {Recipient}", request.TemplateName, maskedTo);

            var templateObj = new Dictionary<string, object>
            {
                ["name"] = request.TemplateName,
                ["language"] = new Dictionary<string, string>
                {
                    ["code"] = string.IsNullOrWhiteSpace(request.LanguageCode) ? _settings.DefaultLanguage : request.LanguageCode
                }
            };

            if (request.Components != null && request.Components.Count > 0)
            {
                var componentList = new List<Dictionary<string, object>>();
                foreach (var comp in request.Components)
                {
                    var compDict = new Dictionary<string, object>
                    {
                        ["type"] = comp.Type
                    };

                    if (!string.IsNullOrWhiteSpace(comp.SubType))
                    {
                        compDict["sub_type"] = comp.SubType!;
                    }

                    if (!string.IsNullOrWhiteSpace(comp.Index))
                    {
                        compDict["index"] = comp.Index!;
                    }

                    if (comp.Parameters != null && comp.Parameters.Count > 0)
                    {
                        var paramList = new List<Dictionary<string, object>>();
                        foreach (var param in comp.Parameters)
                        {
                            var paramDict = BuildTemplateParameterDictionary(param);
                            paramList.Add(paramDict);
                        }
                        compDict["parameters"] = paramList;
                    }

                    componentList.Add(compDict);
                }

                templateObj["components"] = componentList;
            }

            var payload = new Dictionary<string, object>
            {
                ["messaging_product"] = "whatsapp",
                ["recipient_type"] = "individual",
                ["to"] = normalizedTo,
                ["type"] = "template",
                ["template"] = templateObj
            };

            return await PostMessagePayloadAsync(payload, maskedTo, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<WhatsAppMessageResult> SendMediaAsync(
            WhatsAppMediaMessageRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            request.Validate();

            if (!_settings.Enabled)
            {
                _logger?.LogWarning("WhatsApp messaging is disabled in configuration. Media message was not dispatched.");
                return WhatsAppMessageResult.Failure(
                    "WhatsApp messaging is disabled in configuration.",
                    WhatsAppErrorClassification.Permanent);
            }

            string normalizedTo = WhatsAppSecurityHelper.NormalizePhoneNumber(request.To);
            string maskedTo = WhatsAppSecurityHelper.MaskPhoneNumber(normalizedTo);
            string mediaTypeStr = request.MediaType.ToString().ToLowerInvariant();

            _logger?.LogInformation("Dispatching WhatsApp media message ({MediaType}) to recipient {Recipient}", mediaTypeStr, maskedTo);

            var mediaObj = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(request.MediaId))
            {
                mediaObj["id"] = request.MediaId!;
            }
            else if (!string.IsNullOrWhiteSpace(request.Link))
            {
                mediaObj["link"] = request.Link!;
            }

            if (!string.IsNullOrWhiteSpace(request.Caption) &&
                (request.MediaType == WhatsAppMediaType.Image ||
                 request.MediaType == WhatsAppMediaType.Document ||
                 request.MediaType == WhatsAppMediaType.Video))
            {
                mediaObj["caption"] = request.Caption!;
            }

            if (!string.IsNullOrWhiteSpace(request.Filename) && request.MediaType == WhatsAppMediaType.Document)
            {
                mediaObj["filename"] = request.Filename!;
            }

            var payload = new Dictionary<string, object>
            {
                ["messaging_product"] = "whatsapp",
                ["recipient_type"] = "individual",
                ["to"] = normalizedTo,
                ["type"] = mediaTypeStr,
                [mediaTypeStr] = mediaObj
            };

            return await PostMessagePayloadAsync(payload, maskedTo, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<WhatsAppMediaUploadResult> UploadMediaAsync(
            WhatsAppMediaUploadRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            request.Validate();

            if (!_settings.Enabled)
            {
                return WhatsAppMediaUploadResult.Failure("WhatsApp messaging is disabled in configuration.");
            }

            string endpoint = $"{_settings.BaseUrl.TrimEnd('/')}/{_settings.GraphApiVersion}/{_settings.PhoneNumberId}/media";
            _logger?.LogInformation("Uploading WhatsApp media file '{FileName}' ({MimeType})", request.FileName, request.MimeType);

            try
            {
                using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AccessToken);

                    using (var multipartContent = new MultipartFormDataContent())
                    {
                        multipartContent.Add(new StringContent("whatsapp"), "messaging_product");
                        multipartContent.Add(new StringContent(request.MimeType), "type");

                        HttpContent fileContent;
                        if (request.Content != null)
                        {
                            fileContent = new StreamContent(request.Content);
                        }
                        else
                        {
                            fileContent = new ByteArrayContent(request.ContentBytes!);
                        }

                        fileContent.Headers.ContentType = new MediaTypeHeaderValue(request.MimeType);
                        multipartContent.Add(fileContent, "file", request.FileName);

                        httpRequest.Content = multipartContent;

                        using (var response = await _httpClient.SendAsync(httpRequest, cancellationToken))
                        {
                            string responseBody = await response.Content.ReadAsStringAsync();

                            if (response.IsSuccessStatusCode)
                            {
                                using (var doc = JsonDocument.Parse(responseBody))
                                {
                                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                                    {
                                        string mediaId = idProp.GetString() ?? string.Empty;
                                        _logger?.LogInformation("WhatsApp media uploaded successfully with ID {MediaId}", mediaId);
                                        return WhatsAppMediaUploadResult.Success(mediaId);
                                    }
                                }

                                return WhatsAppMediaUploadResult.Failure("Upload response succeeded but media ID was missing.");
                            }

                            var errorInfo = ParseMetaError(responseBody, response.StatusCode);
                            _logger?.LogWarning("WhatsApp media upload failed. Status: {StatusCode}, Error: {Message}",
                                response.StatusCode, errorInfo.message);

                            return WhatsAppMediaUploadResult.Failure(errorInfo.message, errorInfo.classification);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning("WhatsApp media upload timed out.");
                return WhatsAppMediaUploadResult.Failure("Media upload timed out.", WhatsAppErrorClassification.Ambiguous);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                _logger?.LogError(ex, "Unexpected error during WhatsApp media upload: {Message}", ex.Message);
                return WhatsAppMediaUploadResult.Failure(ex.Message, WhatsAppErrorClassification.Transient);
            }
        }

        /// <inheritdoc />
        public async Task<WhatsAppMediaMetadata?> GetMediaMetadataAsync(
            string mediaId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(mediaId))
            {
                throw new ArgumentException("MediaId cannot be null or whitespace.", nameof(mediaId));
            }

            if (!_settings.Enabled)
            {
                return null;
            }

            string endpoint = $"{_settings.BaseUrl.TrimEnd('/')}/{_settings.GraphApiVersion}/{mediaId}";
            _logger?.LogInformation("Retrieving WhatsApp media metadata for {MediaId}", mediaId);

            try
            {
                using (var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint))
                {
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AccessToken);

                    using (var response = await _httpClient.SendAsync(httpRequest, cancellationToken))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger?.LogWarning("Failed to retrieve media metadata for {MediaId}. Status: {StatusCode}", mediaId, response.StatusCode);
                            return null;
                        }

                        string responseBody = await response.Content.ReadAsStringAsync();
                        using (var doc = JsonDocument.Parse(responseBody))
                        {
                            var root = doc.RootElement;
                            var metadata = new WhatsAppMediaMetadata
                            {
                                Id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? mediaId : mediaId,
                                Url = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null,
                                MimeType = root.TryGetProperty("mime_type", out var mimeProp) ? mimeProp.GetString() : null,
                                Sha256 = root.TryGetProperty("sha256", out var shaProp) ? shaProp.GetString() : null,
                                FileSize = root.TryGetProperty("file_size", out var sizeProp) && sizeProp.TryGetInt64(out long size) ? size : 0
                            };

                            return metadata;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error retrieving WhatsApp media metadata for {MediaId}: {Message}", mediaId, ex.Message);
                return null;
            }
        }

        /// <inheritdoc />
        public bool VerifyWebhookChallenge(string? mode, string? token, string? challenge, out string? response)
        {
            return WhatsAppSecurityHelper.TryVerifyWebhookChallenge(mode, token, challenge, _settings.WebhookVerifyToken, out response);
        }

        /// <inheritdoc />
        public bool ValidateWebhookSignature(string? signatureHeader, string rawPayload)
        {
            return WhatsAppSecurityHelper.VerifyWebhookSignature(signatureHeader, rawPayload, _settings.AppSecret);
        }

        /// <inheritdoc />
        public WhatsAppWebhookEvent ProcessWebhook(string rawPayload)
        {
            if (string.IsNullOrWhiteSpace(rawPayload))
            {
                throw new ArgumentException("Webhook payload cannot be null or whitespace.", nameof(rawPayload));
            }

            var evt = new WhatsAppWebhookEvent();

            try
            {
                using (var doc = JsonDocument.Parse(rawPayload))
                {
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("entry", out var entryArray) || entryArray.ValueKind != JsonValueKind.Array)
                    {
                        return evt;
                    }

                    foreach (var entry in entryArray.EnumerateArray())
                    {
                        if (entry.TryGetProperty("id", out var wabaIdProp))
                        {
                            evt.BusinessAccountId = wabaIdProp.GetString();
                        }

                        if (!entry.TryGetProperty("changes", out var changesArray) || changesArray.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }

                        foreach (var change in changesArray.EnumerateArray())
                        {
                            if (!change.TryGetProperty("value", out var valueObj))
                            {
                                continue;
                            }

                            if (valueObj.TryGetProperty("metadata", out var metaObj))
                            {
                                if (metaObj.TryGetProperty("phone_number_id", out var phoneIdProp))
                                {
                                    evt.PhoneNumberId = phoneIdProp.GetString();
                                }
                                if (metaObj.TryGetProperty("display_phone_number", out var dispProp))
                                {
                                    evt.DisplayPhoneNumber = dispProp.GetString();
                                }
                            }

                            // Extract incoming messages
                            if (valueObj.TryGetProperty("messages", out var msgArray) && msgArray.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var msg in msgArray.EnumerateArray())
                                {
                                    var incoming = new WhatsAppIncomingMessage();
                                    if (msg.TryGetProperty("id", out var idProp))
                                    {
                                        incoming.Id = idProp.GetString() ?? string.Empty;
                                    }
                                    if (msg.TryGetProperty("from", out var fromProp))
                                    {
                                        incoming.From = fromProp.GetString() ?? string.Empty;
                                    }
                                    if (msg.TryGetProperty("type", out var typeProp))
                                    {
                                        incoming.Type = typeProp.GetString() ?? "text";
                                    }
                                    if (msg.TryGetProperty("timestamp", out var tsProp) &&
                                        long.TryParse(tsProp.GetString(), out long epochSeconds))
                                    {
                                        incoming.Timestamp = DateTimeOffset.FromUnixTimeSeconds(epochSeconds);
                                    }

                                    if (string.Equals(incoming.Type, "text", StringComparison.OrdinalIgnoreCase) &&
                                        msg.TryGetProperty("text", out var textObj) &&
                                        textObj.TryGetProperty("body", out var bodyProp))
                                    {
                                        incoming.TextBody = bodyProp.GetString();
                                    }
                                    else if (msg.TryGetProperty(incoming.Type, out var mediaObj))
                                    {
                                        var mediaRef = new WhatsAppMediaReference();
                                        if (mediaObj.TryGetProperty("id", out var mediaIdProp))
                                        {
                                            mediaRef.Id = mediaIdProp.GetString();
                                        }
                                        if (mediaObj.TryGetProperty("caption", out var captionProp))
                                        {
                                            mediaRef.Caption = captionProp.GetString();
                                        }
                                        if (mediaObj.TryGetProperty("filename", out var fnProp))
                                        {
                                            mediaRef.Filename = fnProp.GetString();
                                        }
                                        incoming.Media = mediaRef;
                                    }

                                    evt.IncomingMessages.Add(incoming);
                                }
                            }

                            // Extract status updates
                            if (valueObj.TryGetProperty("statuses", out var statusArray) && statusArray.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var st in statusArray.EnumerateArray())
                                {
                                    var statusUpdate = new WhatsAppStatusUpdate();
                                    if (st.TryGetProperty("id", out var idProp))
                                    {
                                        statusUpdate.MessageId = idProp.GetString() ?? string.Empty;
                                    }
                                    if (st.TryGetProperty("recipient_id", out var recipProp))
                                    {
                                        statusUpdate.RecipientId = recipProp.GetString() ?? string.Empty;
                                    }
                                    if (st.TryGetProperty("timestamp", out var tsProp) &&
                                        long.TryParse(tsProp.GetString(), out long epochSeconds))
                                    {
                                        statusUpdate.Timestamp = DateTimeOffset.FromUnixTimeSeconds(epochSeconds);
                                    }
                                    if (st.TryGetProperty("status", out var statusProp))
                                    {
                                        string statusStr = statusProp.GetString() ?? string.Empty;
                                        statusUpdate.Status = ParseDeliveryStatus(statusStr);
                                    }

                                    if (st.TryGetProperty("errors", out var errorsArray) && errorsArray.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var err in errorsArray.EnumerateArray())
                                        {
                                            var webhookError = new WhatsAppWebhookError();
                                            if (err.TryGetProperty("code", out var codeProp) && codeProp.TryGetInt32(out int code))
                                            {
                                                webhookError.Code = code;
                                            }
                                            if (err.TryGetProperty("title", out var titleProp))
                                            {
                                                webhookError.Title = titleProp.GetString() ?? string.Empty;
                                            }
                                            if (err.TryGetProperty("message", out var errMsgProp))
                                            {
                                                webhookError.Message = errMsgProp.GetString() ?? string.Empty;
                                            }
                                            if (err.TryGetProperty("error_data", out var dataObj) &&
                                                dataObj.TryGetProperty("details", out var detailsProp))
                                            {
                                                webhookError.Details = detailsProp.GetString();
                                            }

                                            statusUpdate.Errors.Add(webhookError);
                                        }
                                    }

                                    evt.StatusUpdates.Add(statusUpdate);
                                }
                            }
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger?.LogError(ex, "Failed to parse WhatsApp webhook JSON payload: {Message}", ex.Message);
                throw;
            }

            return evt;
        }

        private async Task<WhatsAppMessageResult> PostMessagePayloadAsync(
            Dictionary<string, object> payload,
            string maskedRecipient,
            CancellationToken cancellationToken)
        {
            string endpoint = $"{_settings.BaseUrl.TrimEnd('/')}/{_settings.GraphApiVersion}/{_settings.PhoneNumberId}/messages";
            string jsonBody = JsonSerializer.Serialize(payload, JsonOptions);

            try
            {
                using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AccessToken);
                    httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    using (var response = await _httpClient.SendAsync(httpRequest, cancellationToken))
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            using (var doc = JsonDocument.Parse(responseBody))
                            {
                                var root = doc.RootElement;
                                string? messageId = null;
                                string? waId = null;

                                if (root.TryGetProperty("messages", out var msgArray) &&
                                    msgArray.ValueKind == JsonValueKind.Array &&
                                    msgArray.GetArrayLength() > 0)
                                {
                                    var firstMsg = msgArray[0];
                                    if (firstMsg.TryGetProperty("id", out var idProp))
                                    {
                                        messageId = idProp.GetString();
                                    }
                                }

                                if (root.TryGetProperty("contacts", out var contactsArray) &&
                                    contactsArray.ValueKind == JsonValueKind.Array &&
                                    contactsArray.GetArrayLength() > 0)
                                {
                                    var firstContact = contactsArray[0];
                                    if (firstContact.TryGetProperty("wa_id", out var waIdProp))
                                    {
                                        waId = waIdProp.GetString();
                                    }
                                }

                                _logger?.LogInformation(
                                    "WhatsApp message dispatched successfully to {Recipient}. MessageId: {MessageId}",
                                    maskedRecipient,
                                    messageId);

                                return WhatsAppMessageResult.Success(messageId ?? string.Empty, waId);
                            }
                        }

                        var errorInfo = ParseMetaError(responseBody, response.StatusCode);
                        _logger?.LogWarning(
                            "WhatsApp message dispatch failed for {Recipient}. Status: {StatusCode}, ErrorCode: {ErrorCode}, Message: {Message}",
                            maskedRecipient,
                            response.StatusCode,
                            errorInfo.code,
                            errorInfo.message);

                        return WhatsAppMessageResult.Failure(errorInfo.message, errorInfo.classification, errorInfo.code);
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger?.LogWarning(
                    "WhatsApp message dispatch timed out while waiting for response for recipient {Recipient}. State is ambiguous.",
                    maskedRecipient);

                return WhatsAppMessageResult.Failure(
                    "WhatsApp message dispatch timed out while waiting for Meta server response.",
                    WhatsAppErrorClassification.Ambiguous);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                _logger?.LogError(ex, "Unexpected exception during WhatsApp message dispatch to {Recipient}: {Message}", maskedRecipient, ex.Message);
                return WhatsAppMessageResult.Failure(ex.Message, WhatsAppErrorClassification.Transient);
            }
        }

        private static (string message, WhatsAppErrorClassification classification, int? code) ParseMetaError(
            string responseBody,
            HttpStatusCode statusCode)
        {
            string message = $"Meta API error ({statusCode})";
            int? code = null;

            try
            {
                using (var doc = JsonDocument.Parse(responseBody))
                {
                    if (doc.RootElement.TryGetProperty("error", out var errorObj))
                    {
                        if (errorObj.TryGetProperty("message", out var msgProp))
                        {
                            message = msgProp.GetString() ?? message;
                        }
                        if (errorObj.TryGetProperty("code", out var codeProp) && codeProp.TryGetInt32(out int c))
                        {
                            code = c;
                        }
                    }
                }
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    message = responseBody;
                }
            }

            WhatsAppErrorClassification classification;
            int statusInt = (int)statusCode;

            if (statusInt == 429 || statusInt >= 500)
            {
                classification = WhatsAppErrorClassification.Transient;
            }
            else
            {
                classification = WhatsAppErrorClassification.Permanent;
            }

            return (message, classification, code);
        }

        private static Dictionary<string, object> BuildTemplateParameterDictionary(WhatsAppTemplateParameter param)
        {
            var dict = new Dictionary<string, object>
            {
                ["type"] = param.Type
            };

            if (string.Equals(param.Type, "text", StringComparison.OrdinalIgnoreCase))
            {
                dict["text"] = param.Text ?? string.Empty;
            }
            else if (string.Equals(param.Type, "currency", StringComparison.OrdinalIgnoreCase) && param.Currency != null)
            {
                dict["currency"] = new Dictionary<string, object>
                {
                    ["fallback_value"] = param.Currency.FallbackValue,
                    ["code"] = param.Currency.Code,
                    ["amount_1000"] = param.Currency.Amount1000
                };
            }
            else if (string.Equals(param.Type, "date_time", StringComparison.OrdinalIgnoreCase) && param.DateTime != null)
            {
                dict["date_time"] = new Dictionary<string, object>
                {
                    ["fallback_value"] = param.DateTime.FallbackValue
                };
            }
            else if (param.Media != null)
            {
                var mediaDict = new Dictionary<string, object>();
                if (!string.IsNullOrWhiteSpace(param.Media.Id))
                {
                    mediaDict["id"] = param.Media.Id!;
                }
                else if (!string.IsNullOrWhiteSpace(param.Media.Link))
                {
                    mediaDict["link"] = param.Media.Link!;
                }
                dict[param.Type] = mediaDict;
            }

            return dict;
        }

        private static WhatsAppDeliveryStatus ParseDeliveryStatus(string status)
        {
            switch (status?.ToLowerInvariant())
            {
                case "sent":
                    return WhatsAppDeliveryStatus.Sent;
                case "delivered":
                    return WhatsAppDeliveryStatus.Delivered;
                case "read":
                    return WhatsAppDeliveryStatus.Read;
                case "failed":
                    return WhatsAppDeliveryStatus.Failed;
                default:
                    return WhatsAppDeliveryStatus.Unknown;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing">True if releasing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && _ownsHttpClient)
                {
                    _httpClient.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
