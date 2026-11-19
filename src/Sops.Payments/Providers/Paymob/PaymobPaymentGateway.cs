using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sops.Payments.Abstractions;
using Sops.Payments.Configuration;
using Sops.Payments.Models;

namespace Sops.Payments.Providers.Paymob
{
    /// <summary>
    /// Production-ready Paymob payment gateway implementation supporting the modern Unified Checkout (Intention API),
    /// legacy iframe integration, server-side transaction verification, refunds, and HMAC-SHA512 webhook validation.
    /// </summary>
    public class PaymobPaymentGateway : IPaymentGateway, IDisposable
    {
        private readonly PaymentSettings _settings;
        private readonly PaymobSettings _paymob;
        private readonly HttpClient _httpClient;
        private readonly ILogger<PaymobPaymentGateway>? _logger;
        private readonly bool _ownsHttpClient;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaymobPaymentGateway"/> class using configured options.
        /// </summary>
        public PaymobPaymentGateway(
            IOptions<PaymentSettings> options,
            HttpClient? httpClient = null,
            ILogger<PaymobPaymentGateway>? logger = null)
            : this(options?.Value ?? throw new ArgumentNullException(nameof(options)), httpClient, logger)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaymobPaymentGateway"/> class using direct settings.
        /// </summary>
        public PaymobPaymentGateway(
            PaymentSettings settings,
            HttpClient? httpClient = null,
            ILogger<PaymobPaymentGateway>? logger = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _paymob = settings.Paymob ?? new PaymobSettings();
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

            if (_httpClient.Timeout == TimeSpan.FromSeconds(100) && _paymob.TimeoutSeconds > 0)
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(_paymob.TimeoutSeconds);
            }
        }

        /// <summary>
        /// Creates a standalone instance of <see cref="PaymobPaymentGateway"/> without dependency injection.
        /// </summary>
        public static PaymobPaymentGateway Create(
            PaymentSettings settings,
            HttpClient? httpClient = null,
            ILogger<PaymobPaymentGateway>? logger = null)
        {
            return new PaymobPaymentGateway(settings, httpClient, logger);
        }

        /// <inheritdoc />
        public async Task<PaymentResult> InitiatePaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            try
            {
                request.Validate();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Payment request validation failed.");
                return PaymentResult.Failure(PaymentErrorClassification.Permanent, ex.Message);
            }

            if (!_settings.Enabled)
            {
                _logger?.LogInformation(
                    "Payments are disabled by configuration. Suppressing initiation for OrderId '{OrderId}'.",
                    request.OrderId);
                return PaymentResult.Pending(null, "disabled-sink", request.OrderId);
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Prefer modern Unified Checkout (Intention API) if SecretKey is provided
                if (!string.IsNullOrWhiteSpace(_paymob.SecretKey))
                {
                    return await InitiateIntentionAsync(request, stopwatch, cancellationToken).ConfigureAwait(false);
                }

                // Fall back to legacy 3-step auth/order/payment-key flow if ApiKey is provided
                if (!string.IsNullOrWhiteSpace(_paymob.ApiKey))
                {
                    return await InitiateLegacyFlowAsync(request, stopwatch, cancellationToken).ConfigureAwait(false);
                }

                return PaymentResult.Failure(
                    PaymentErrorClassification.Permanent,
                    "Paymob credentials missing: Neither SecretKey nor ApiKey was configured.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                _logger?.LogWarning("Payment initiation was cancelled by caller for OrderId '{OrderId}'.", request.OrderId);
                return PaymentResult.Failure(PaymentErrorClassification.Transient, "Payment initiation was cancelled.", null, PaymentStatus.Failed);
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                var classification = DetermineHttpErrorClassification(ex);
                _logger?.LogError(ex, "HTTP failure initiating payment with Paymob ({Classification}): {ErrorMessage}", classification, ex.Message);
                return PaymentResult.Failure(classification, $"Paymob communication error: {ex.Message}");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger?.LogError(ex, "Unexpected error initiating payment with Paymob.");
                return PaymentResult.Failure(PaymentErrorClassification.Permanent, $"Unexpected error: {ex.Message}");
            }
        }

        private async Task<PaymentResult> InitiateIntentionAsync(
            PaymentRequest request,
            Stopwatch stopwatch,
            CancellationToken cancellationToken)
        {
            string url = $"{_paymob.BaseUrl.TrimEnd('/')}/v1/intention/";

            var billing = request.BillingData ?? PaymentBillingData.FromCustomer(request.Customer);

            var itemsList = new List<object>();
            foreach (var item in request.Items)
            {
                itemsList.Add(new
                {
                    name = item.Name,
                    amount = item.Amount.Cents,
                    quantity = item.Quantity,
                    description = item.Description ?? string.Empty
                });
            }

            var payload = new Dictionary<string, object?>
            {
                ["amount"] = request.Amount.Cents,
                ["currency"] = request.Amount.Currency,
                ["payment_methods"] = new[] { _paymob.IntegrationId },
                ["items"] = itemsList,
                ["billing_data"] = new Dictionary<string, object?>
                {
                    ["first_name"] = billing.FirstName,
                    ["last_name"] = billing.LastName,
                    ["email"] = billing.Email,
                    ["phone_number"] = billing.PhoneNumber,
                    ["apartment"] = billing.Apartment,
                    ["floor"] = billing.Floor,
                    ["street"] = billing.Street,
                    ["building"] = billing.Building,
                    ["city"] = billing.City,
                    ["state"] = billing.State,
                    ["country"] = billing.Country,
                    ["postal_code"] = billing.PostalCode
                },
                ["customer"] = new Dictionary<string, object?>
                {
                    ["first_name"] = request.Customer.FirstName,
                    ["last_name"] = request.Customer.LastName,
                    ["email"] = request.Customer.Email,
                    ["phone_number"] = request.Customer.PhoneNumber
                },
                ["special_reference"] = request.IdempotencyKey ?? request.OrderId ?? Guid.NewGuid().ToString("N"),
                ["notification_url"] = request.NotificationUrl ?? _paymob.NotificationUrl,
                ["redirection_url"] = request.RedirectionUrl ?? _paymob.RedirectionUrl
            };

            string json = JsonSerializer.Serialize(payload);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Token {_paymob.SecretKey}");

            _logger?.LogDebug(
                "Sending Paymob Intention request for OrderId '{OrderId}', Amount: {Amount} {Currency}...",
                request.OrderId,
                request.Amount.Amount,
                request.Amount.Currency);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                stopwatch.Stop();
                var classification = ((int)response.StatusCode >= 500)
                    ? PaymentErrorClassification.Transient
                    : PaymentErrorClassification.Permanent;

                _logger?.LogError(
                    "Paymob Intention API returned HTTP {StatusCode}: {ResponseBody}",
                    response.StatusCode,
                    SanitizeForLog(responseBody));

                return PaymentResult.Failure(
                    classification,
                    $"Paymob Intention API error ({(int)response.StatusCode}): {response.StatusCode}",
                    ((int)response.StatusCode).ToString());
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            string? clientSecret = null;
            if (root.TryGetProperty("cs", out var csProp))
            {
                clientSecret = csProp.GetString();
            }
            else if (root.TryGetProperty("client_secret", out var csProp2))
            {
                clientSecret = csProp2.GetString();
            }

            string? checkoutUrl = null;
            if (!string.IsNullOrWhiteSpace(_paymob.PublicKey) && !string.IsNullOrWhiteSpace(clientSecret))
            {
                checkoutUrl = $"{_paymob.BaseUrl.TrimEnd('/')}/unifiedcheckout/?publicKey={_paymob.PublicKey}&clientSecret={clientSecret}";
            }

            stopwatch.Stop();
            _logger?.LogInformation(
                "Paymob Intention created successfully in {ElapsedMs}ms for OrderId '{OrderId}'.",
                stopwatch.ElapsedMilliseconds,
                request.OrderId);

            return PaymentResult.Pending(checkoutUrl, clientSecret, request.OrderId);
        }

        private async Task<PaymentResult> InitiateLegacyFlowAsync(
            PaymentRequest request,
            Stopwatch stopwatch,
            CancellationToken cancellationToken)
        {
            // Step 1: Authentication token
            string authUrl = $"{_paymob.BaseUrl.TrimEnd('/')}/api/auth/tokens";
            string authPayload = JsonSerializer.Serialize(new { api_key = _paymob.ApiKey });

            using var authReq = new HttpRequestMessage(HttpMethod.Post, authUrl)
            {
                Content = new StringContent(authPayload, Encoding.UTF8, "application/json")
            };

            using var authRes = await _httpClient.SendAsync(authReq, cancellationToken).ConfigureAwait(false);
            string authBody = await authRes.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!authRes.IsSuccessStatusCode)
            {
                stopwatch.Stop();
                return PaymentResult.Failure(
                    PaymentErrorClassification.Permanent,
                    $"Paymob authentication failed ({(int)authRes.StatusCode}).");
            }

            using var authDoc = JsonDocument.Parse(authBody);
            string authToken = authDoc.RootElement.GetProperty("token").GetString()!;

            // Step 2: Order Registration
            string orderUrl = $"{_paymob.BaseUrl.TrimEnd('/')}/api/ecommerce/orders";
            var orderPayload = new
            {
                auth_token = authToken,
                delivery_needed = "false",
                amount_cents = request.Amount.Cents,
                currency = request.Amount.Currency,
                merchant_order_id = request.OrderId,
                items = Array.Empty<object>()
            };

            using var orderReq = new HttpRequestMessage(HttpMethod.Post, orderUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(orderPayload), Encoding.UTF8, "application/json")
            };

            using var orderRes = await _httpClient.SendAsync(orderReq, cancellationToken).ConfigureAwait(false);
            string orderBody = await orderRes.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!orderRes.IsSuccessStatusCode)
            {
                stopwatch.Stop();
                return PaymentResult.Failure(
                    PaymentErrorClassification.Permanent,
                    $"Paymob order registration failed ({(int)orderRes.StatusCode}).");
            }

            using var orderDoc = JsonDocument.Parse(orderBody);
            long orderId = orderDoc.RootElement.GetProperty("id").GetInt64();

            // Step 3: Payment Key
            string keyUrl = $"{_paymob.BaseUrl.TrimEnd('/')}/api/acceptance/payment_keys";
            var billing = request.BillingData ?? PaymentBillingData.FromCustomer(request.Customer);

            var keyPayload = new
            {
                auth_token = authToken,
                amount_cents = request.Amount.Cents,
                expiration = 3600,
                order_id = orderId,
                billing_data = new
                {
                    first_name = billing.FirstName,
                    last_name = billing.LastName,
                    email = billing.Email,
                    phone_number = billing.PhoneNumber,
                    apartment = billing.Apartment,
                    floor = billing.Floor,
                    street = billing.Street,
                    building = billing.Building,
                    city = billing.City,
                    state = billing.State,
                    country = billing.Country,
                    postal_code = billing.PostalCode
                },
                currency = request.Amount.Currency,
                integration_id = _paymob.IntegrationId
            };

            using var keyReq = new HttpRequestMessage(HttpMethod.Post, keyUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(keyPayload), Encoding.UTF8, "application/json")
            };

            using var keyRes = await _httpClient.SendAsync(keyReq, cancellationToken).ConfigureAwait(false);
            string keyBody = await keyRes.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!keyRes.IsSuccessStatusCode)
            {
                stopwatch.Stop();
                return PaymentResult.Failure(
                    PaymentErrorClassification.Permanent,
                    $"Paymob payment key generation failed ({(int)keyRes.StatusCode}).");
            }

            using var keyDoc = JsonDocument.Parse(keyBody);
            string paymentToken = keyDoc.RootElement.GetProperty("token").GetString()!;

            string? checkoutUrl = null;
            if (_paymob.IframeId.HasValue && _paymob.IframeId.Value > 0)
            {
                checkoutUrl = $"{_paymob.BaseUrl.TrimEnd('/')}/api/acceptance/iframes/{_paymob.IframeId.Value}?payment_token={paymentToken}";
            }

            stopwatch.Stop();
            _logger?.LogInformation(
                "Paymob legacy payment key generated in {ElapsedMs}ms for OrderId '{OrderId}'.",
                stopwatch.ElapsedMilliseconds,
                request.OrderId);

            return PaymentResult.Pending(checkoutUrl, paymentToken, orderId.ToString());
        }

        /// <inheritdoc />
        public async Task<PaymentVerificationResult> VerifyPaymentAsync(string transactionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                throw new ArgumentException("Transaction ID cannot be null or whitespace.", nameof(transactionId));
            }

            string url = $"{_paymob.BaseUrl.TrimEnd('/')}/api/acceptance/transactions/{transactionId.Trim()}";

            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrWhiteSpace(_paymob.SecretKey))
            {
                httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Token {_paymob.SecretKey}");
            }
            else if (!string.IsNullOrWhiteSpace(_paymob.ApiKey))
            {
                // Obtain token for query
                string authToken = await GetAuthTokenAsync(cancellationToken).ConfigureAwait(false);
                httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {authToken}");
            }

            _logger?.LogDebug("Verifying Paymob transaction '{TransactionId}'...", transactionId);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogError(
                    "Paymob transaction verification returned HTTP {StatusCode}: {ResponseBody}",
                    response.StatusCode,
                    SanitizeForLog(responseBody));

                return PaymentVerificationResult.NonSuccess(
                    PaymentStatus.Unknown,
                    transactionId,
                    null,
                    Money.FromCents(0, _paymob.DefaultCurrency),
                    $"Gateway verification error: HTTP {(int)response.StatusCode}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            bool success = root.TryGetProperty("success", out var sProp) && sProp.GetBoolean();
            bool pending = root.TryGetProperty("pending", out var pProp) && pProp.GetBoolean();
            bool isRefunded = root.TryGetProperty("is_refunded", out var rProp) && rProp.GetBoolean();
            bool isVoided = root.TryGetProperty("is_voided", out var vProp) && vProp.GetBoolean();

            long amountCents = root.TryGetProperty("amount_cents", out var aProp) ? aProp.GetInt64() : 0;
            string currency = root.TryGetProperty("currency", out var cProp) ? (cProp.GetString() ?? _paymob.DefaultCurrency) : _paymob.DefaultCurrency;
            var amount = Money.FromCents(amountCents, currency);

            string? orderId = null;
            if (root.TryGetProperty("order", out var orderProp))
            {
                if (orderProp.ValueKind == JsonValueKind.Object && orderProp.TryGetProperty("id", out var orderIdProp))
                {
                    orderId = orderIdProp.ToString();
                }
                else if (orderProp.ValueKind == JsonValueKind.Number || orderProp.ValueKind == JsonValueKind.String)
                {
                    orderId = orderProp.ToString();
                }
            }

            string? paymentMethod = null;
            string? maskedPan = null;
            if (root.TryGetProperty("source_data", out var srcProp) && srcProp.ValueKind == JsonValueKind.Object)
            {
                if (srcProp.TryGetProperty("type", out var typeProp)) paymentMethod = typeProp.GetString();
                if (srcProp.TryGetProperty("pan", out var panProp)) maskedPan = panProp.GetString();
            }

            PaymentStatus status;
            if (isRefunded) status = PaymentStatus.Refunded;
            else if (isVoided) status = PaymentStatus.Cancelled;
            else if (pending) status = PaymentStatus.Pending;
            else if (success) status = PaymentStatus.Successful;
            else status = PaymentStatus.Failed;

            _logger?.LogInformation(
                "Transaction '{TransactionId}' verified as {Status}. Amount: {Amount}",
                transactionId,
                status,
                amount);

            if (status == PaymentStatus.Successful)
            {
                return PaymentVerificationResult.Success(
                    transactionId,
                    orderId,
                    amount,
                    DateTimeOffset.UtcNow,
                    paymentMethod,
                    maskedPan);
            }

            return PaymentVerificationResult.NonSuccess(
                status,
                transactionId,
                orderId,
                amount,
                status == PaymentStatus.Failed ? "Transaction declined by gateway." : null,
                DateTimeOffset.UtcNow,
                paymentMethod,
                maskedPan);
        }

        /// <inheritdoc />
        public async Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            try
            {
                request.Validate();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Refund request validation failed.");
                return RefundResult.Failure(request.TransactionId, PaymentErrorClassification.Permanent, ex.Message);
            }

            string url = $"{_paymob.BaseUrl.TrimEnd('/')}/api/acceptance/void_refund/refund";

            string authToken;
            if (!string.IsNullOrWhiteSpace(_paymob.SecretKey))
            {
                authToken = _paymob.SecretKey!;
            }
            else
            {
                authToken = await GetAuthTokenAsync(cancellationToken).ConfigureAwait(false);
            }

            var payload = new
            {
                auth_token = authToken,
                transaction_id = request.TransactionId,
                amount_cents = request.Amount.Cents
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            _logger?.LogInformation(
                "Requesting refund for TransactionId '{TransactionId}', Amount: {Amount}...",
                request.TransactionId,
                request.Amount);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var classification = ((int)response.StatusCode >= 500)
                    ? PaymentErrorClassification.Transient
                    : PaymentErrorClassification.Permanent;

                _logger?.LogError(
                    "Paymob refund failed with HTTP {StatusCode}: {ResponseBody}",
                    response.StatusCode,
                    SanitizeForLog(responseBody));

                return RefundResult.Failure(
                    request.TransactionId,
                    classification,
                    $"Paymob refund error ({(int)response.StatusCode}).");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            bool success = root.TryGetProperty("success", out var sProp) && sProp.GetBoolean();
            string refundTxId = root.TryGetProperty("id", out var idProp) ? idProp.ToString() : Guid.NewGuid().ToString("N");

            if (success)
            {
                _logger?.LogInformation(
                    "Refund of {Amount} for TransactionId '{TransactionId}' completed successfully. RefundId: {RefundId}.",
                    request.Amount,
                    request.TransactionId,
                    refundTxId);

                return RefundResult.Success(refundTxId, request.TransactionId, request.Amount);
            }

            return RefundResult.Failure(
                request.TransactionId,
                PaymentErrorClassification.Permanent,
                "Gateway declined refund request.");
        }

        /// <inheritdoc />
        public Task<PaymentWebhookEvent?> ProcessWebhookAsync(
            string rawPayload,
            string? signature,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rawPayload))
            {
                _logger?.LogWarning("Received empty or null webhook payload.");
                return Task.FromResult<PaymentWebhookEvent?>(null);
            }

            // Verify HMAC signature
            bool isValid = PaymobHmacValidator.Validate(rawPayload, signature, _paymob.HmacSecret);
            if (!isValid)
            {
                _logger?.LogWarning("Paymob webhook HMAC validation failed.");
                return Task.FromResult<PaymentWebhookEvent?>(null);
            }

            try
            {
                using var doc = JsonDocument.Parse(rawPayload);
                var root = doc.RootElement;

                string eventType = root.TryGetProperty("type", out var typeProp) ? (typeProp.GetString() ?? "TRANSACTION") : "TRANSACTION";
                JsonElement objElement = root.TryGetProperty("obj", out var oProp) ? oProp : root;

                string transactionId = objElement.TryGetProperty("id", out var idProp) ? idProp.ToString() : string.Empty;
                bool success = objElement.TryGetProperty("success", out var sProp) && sProp.GetBoolean();
                bool pending = objElement.TryGetProperty("pending", out var pProp) && pProp.GetBoolean();
                bool isRefunded = objElement.TryGetProperty("is_refunded", out var rProp) && rProp.GetBoolean();
                bool isVoided = objElement.TryGetProperty("is_voided", out var vProp) && vProp.GetBoolean();

                long amountCents = objElement.TryGetProperty("amount_cents", out var aProp) ? aProp.GetInt64() : 0;
                string currency = objElement.TryGetProperty("currency", out var cProp) ? (cProp.GetString() ?? _paymob.DefaultCurrency) : _paymob.DefaultCurrency;
                var amount = Money.FromCents(amountCents, currency);

                string? orderId = null;
                if (objElement.TryGetProperty("order", out var orderProp) && orderProp.ValueKind == JsonValueKind.Object)
                {
                    if (orderProp.TryGetProperty("id", out var oIdProp)) orderId = oIdProp.ToString();
                }

                string? maskedPan = null;
                string? paymentMethod = null;
                if (objElement.TryGetProperty("source_data", out var srcProp) && srcProp.ValueKind == JsonValueKind.Object)
                {
                    if (srcProp.TryGetProperty("pan", out var panProp)) maskedPan = panProp.GetString();
                    if (srcProp.TryGetProperty("sub_type", out var subProp)) paymentMethod = subProp.GetString();
                    else if (srcProp.TryGetProperty("type", out var tProp)) paymentMethod = tProp.GetString();
                }

                PaymentStatus status;
                if (isRefunded) status = PaymentStatus.Refunded;
                else if (isVoided) status = PaymentStatus.Cancelled;
                else if (pending) status = PaymentStatus.Pending;
                else if (success) status = PaymentStatus.Successful;
                else status = PaymentStatus.Failed;

                var rawDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in root.EnumerateObject())
                {
                    rawDict[prop.Name] = prop.Value.ToString();
                }

                var webhookEvent = new PaymentWebhookEvent(
                    eventType: eventType,
                    transactionId: transactionId,
                    orderId: orderId,
                    status: status,
                    amount: amount,
                    isSuccess: success,
                    isPending: pending,
                    isRefunded: isRefunded,
                    isVoided: isVoided,
                    createdAt: DateTimeOffset.UtcNow,
                    maskedPan: maskedPan,
                    paymentMethod: paymentMethod,
                    rawPayload: rawDict);

                _logger?.LogInformation(
                    "Processed valid Paymob webhook for Transaction '{TransactionId}', Status: {Status}.",
                    transactionId,
                    status);

                return Task.FromResult<PaymentWebhookEvent?>(webhookEvent);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to parse Paymob webhook payload.");
                return Task.FromResult<PaymentWebhookEvent?>(null);
            }
        }

        private async Task<string> GetAuthTokenAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_paymob.ApiKey))
            {
                throw new InvalidOperationException("Paymob ApiKey is required for token authentication.");
            }

            string authUrl = $"{_paymob.BaseUrl.TrimEnd('/')}/api/auth/tokens";
            string authPayload = JsonSerializer.Serialize(new { api_key = _paymob.ApiKey });

            using var authReq = new HttpRequestMessage(HttpMethod.Post, authUrl)
            {
                Content = new StringContent(authPayload, Encoding.UTF8, "application/json")
            };

            using var authRes = await _httpClient.SendAsync(authReq, cancellationToken).ConfigureAwait(false);
            authRes.EnsureSuccessStatusCode();

            string authBody = await authRes.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var authDoc = JsonDocument.Parse(authBody);
            return authDoc.RootElement.GetProperty("token").GetString()!;
        }

        private static PaymentErrorClassification DetermineHttpErrorClassification(HttpRequestException ex)
        {
            string msg = ex.Message.ToLowerInvariant();
            if (msg.Contains("timed out") || msg.Contains("timeout"))
            {
                return PaymentErrorClassification.Transient;
            }

            return PaymentErrorClassification.Permanent;
        }

        private static string SanitizeForLog(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            if (input.Length > 500) return input.Substring(0, 500) + "...[truncated]";
            return input;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes allocated resources.
        /// </summary>
        /// <param name="disposing">True to release managed objects.</param>
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
