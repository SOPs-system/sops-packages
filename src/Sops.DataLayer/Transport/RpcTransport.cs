using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Authentication;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Transport
{
    /// <summary>
    /// Transport for local SOPs JSON-RPC services supporting both standard JSON-RPC 2.0 envelopes (POST /rpc)
    /// and route-based RPC endpoints (POST /rpc/{service}/{method}).
    /// </summary>
    public sealed class RpcTransport : ITransport
    {
        private readonly HttpClient _httpClient;
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<RpcTransport>? _logger;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public RpcTransport(HttpClient httpClient, ISessionManager sessionManager, ILogger<RpcTransport>? logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _logger = logger;
        }

        public async Task<DataResult<T>> SendAsync<T>(EndpointDefinition endpoint, DataRequest request, Uri resolvedUri, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            var origin = endpoint.Target == BackendTarget.LocalServer ? DataSourceOrigin.LocalServer : DataSourceOrigin.GlobalApi;

            if (string.Equals(resolvedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                Security.TlsConfiguration.EnsureTls12Supported();
            }

            try
            {
                using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, resolvedUri))
                {
                    if (!string.IsNullOrWhiteSpace(request.Context.CorrelationId))
                    {
                        httpRequest.Headers.TryAddWithoutValidation(RequestHeaders.CorrelationId, request.Context.CorrelationId);
                    }

                    if (!string.IsNullOrWhiteSpace(request.Context.IdempotencyKey))
                    {
                        httpRequest.Headers.TryAddWithoutValidation(RequestHeaders.IdempotencyKey, request.Context.IdempotencyKey);
                    }

                    if (!string.IsNullOrWhiteSpace(request.Context.TerminalId))
                    {
                        httpRequest.Headers.TryAddWithoutValidation(RequestHeaders.TerminalId, request.Context.TerminalId);
                    }

                    if (!string.IsNullOrWhiteSpace(request.Context.PharmacyId))
                    {
                        httpRequest.Headers.TryAddWithoutValidation(RequestHeaders.PharmacyId, request.Context.PharmacyId);
                    }

                    if (!string.IsNullOrWhiteSpace(request.Context.BranchId))
                    {
                        httpRequest.Headers.TryAddWithoutValidation(RequestHeaders.BranchId, request.Context.BranchId);
                    }

                    if (endpoint.RequiresAuthentication)
                    {
                        string? token = endpoint.Target == BackendTarget.GlobalApi
                            ? _sessionManager.CloudAccessToken
                            : (_sessionManager.IsAuthenticated ? _sessionManager.LocalAccessToken : null);

                        if (!string.IsNullOrEmpty(token))
                        {
                            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        }
                    }

                    // Format body: if endpoint route is root "/rpc", wrap into JSON-RPC 2.0 envelope
                    string json;
                    bool isSingleRpcRoute = endpoint.Route.TrimEnd('/').Equals("/rpc", StringComparison.OrdinalIgnoreCase);

                    if (isSingleRpcRoute)
                    {
                        string rpcMethodName = !string.IsNullOrWhiteSpace(endpoint.RpcMethod)
                            ? (!string.IsNullOrWhiteSpace(endpoint.RpcService) ? $"{endpoint.RpcService}.{endpoint.RpcMethod}" : endpoint.RpcMethod!)
                            : endpoint.Id;

                        var envelope = new
                        {
                            jsonrpc = "2.0",
                            method = rpcMethodName,
                            @params = request.Body ?? new { },
                            id = request.Context.RequestId
                        };
                        json = JsonSerializer.Serialize(envelope, JsonOptions);
                    }
                    else if (request.Body != null)
                    {
                        json = JsonSerializer.Serialize(request.Body, JsonOptions);
                    }
                    else
                    {
                        json = "{}";
                    }

                    httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    using (var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false))
                    {
                        sw.Stop();
                        string? etag = response.Headers.ETag?.Tag;
                        TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;

                        var metadata = ResponseMetadata.Live(origin, sw.Elapsed, (int)response.StatusCode, request.Context.CorrelationId, serverVersionOrETag: etag, retryAfter: retryAfter);

                        string responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            var outcome = MapStatusCode(response.StatusCode);
                            var error = TryParseError(responseJson, (int)response.StatusCode, request.Context.CorrelationId);
                            return DataResult<T>.Failure(outcome, error, metadata);
                        }

                        if (string.IsNullOrWhiteSpace(responseJson))
                        {
                            return DataResult<T>.Success(default!, metadata);
                        }

                        var (data, success, err) = TryExtractRpcPayload<T>(responseJson);
                        if (!success)
                        {
                            return DataResult<T>.Failure(DataOutcome.Failure, err ?? DataError.Create("RPC_ERROR", "RPC call returned failure"), metadata);
                        }

                        return DataResult<T>.Success(data!, metadata);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                sw.Stop();
                return DataResult<T>.Cancelled(request.Context.CorrelationId);
            }
            catch (OperationCanceledException ex)
            {
                sw.Stop();
                var metadata = ResponseMetadata.Live(origin, sw.Elapsed, 408, request.Context.CorrelationId);

                // Financial writes must never assume failure on timeout; transaction status is unknown until reconciled
                if (endpoint.IsFinancialMutation || endpoint.RequiresIdempotencyKey)
                {
                    return DataResult<T>.UnknownOutcome($"Financial mutation timed out in-flight. The transaction state on the server is unknown: {ex.Message}", request.Context.CorrelationId);
                }

                var error = DataError.Timeout("Local RPC request timed out", request.Context.CorrelationId);
                return DataResult<T>.Failure(DataOutcome.Timeout, error, metadata);
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                var metadata = ResponseMetadata.Live(origin, sw.Elapsed, 503, request.Context.CorrelationId);

                // Non-idempotent financial writes that suffered connection drops in-flight have unknown outcomes
                if (endpoint.IsFinancialMutation || endpoint.RequiresIdempotencyKey)
                {
                    return DataResult<T>.UnknownOutcome($"Financial mutation connection lost in-flight. The transaction state on the server is unknown: {ex.Message}", request.Context.CorrelationId);
                }

                var error = DataError.Unavailable("Cannot connect to local RPC service: " + ex.Message, request.Context.CorrelationId);
                return DataResult<T>.Failure(DataOutcome.ServerUnavailable, error, metadata);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger?.LogError(ex, "Unexpected error executing RPC request to {Uri}", resolvedUri);
                var metadata = ResponseMetadata.Live(origin, sw.Elapsed, 500, request.Context.CorrelationId);

                if (endpoint.IsFinancialMutation || endpoint.RequiresIdempotencyKey)
                {
                    return DataResult<T>.UnknownOutcome($"Financial mutation encountered unexpected transport error in-flight: {ex.Message}", request.Context.CorrelationId);
                }

                var error = DataError.Create("INTERNAL_CLIENT_ERROR", ex.Message, request.Context.CorrelationId);
                return DataResult<T>.Failure(DataOutcome.Failure, error, metadata);
            }
        }

        private static (T? data, bool success, DataError? error) TryExtractRpcPayload<T>(string json)
        {
            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        // Check JSON-RPC 2.0 error object: { "error": { "code": -32600, "message": "..." } }
                        if (root.TryGetProperty("error", out var errorProp))
                        {
                            string errMsg = "RPC failed";
                            string errCode = "RPC_ERROR";

                            if (errorProp.ValueKind == JsonValueKind.Object)
                            {
                                if (errorProp.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String)
                                {
                                    errMsg = msgEl.GetString() ?? errMsg;
                                }
                                if (errorProp.TryGetProperty("code", out var codeEl))
                                {
                                    errCode = codeEl.ToString();
                                }
                                return (default, false, DataError.Create(errCode, errMsg));
                            }
                            else if (errorProp.ValueKind == JsonValueKind.String)
                            {
                                return (default, false, DataError.Create(errCode, errorProp.GetString() ?? errMsg));
                            }
                        }

                        // Check legacy SOPs RPC envelope: { "success": true/false, "result": ... }
                        if (root.TryGetProperty("success", out var successProp))
                        {
                            bool isSuccess = successProp.ValueKind == JsonValueKind.True;
                            if (!isSuccess)
                            {
                                return (default, false, DataError.Create("RPC_ERROR", "RPC call indicated failure"));
                            }

                            if (root.TryGetProperty("result", out var resultProp))
                            {
                                var resultObj = JsonSerializer.Deserialize<T>(resultProp.GetRawText(), JsonOptions);
                                return (resultObj, true, null);
                            }
                        }

                        // Standard JSON-RPC 2.0 success: { "jsonrpc": "2.0", "result": ... }
                        if (root.TryGetProperty("result", out var standardResultProp))
                        {
                            var resultObj = JsonSerializer.Deserialize<T>(standardResultProp.GetRawText(), JsonOptions);
                            return (resultObj, true, null);
                        }
                    }

                    // Direct deserialization fallback
                    var directObj = JsonSerializer.Deserialize<T>(json, JsonOptions);
                    return (directObj, true, null);
                }
            }
            catch (Exception ex)
            {
                return (default, false, DataError.Create("SERIALIZATION_ERROR", "Failed to deserialize RPC response: " + ex.Message));
            }
        }

        private static DataOutcome MapStatusCode(System.Net.HttpStatusCode code)
        {
            switch (code)
            {
                case System.Net.HttpStatusCode.Unauthorized:
                    return DataOutcome.Unauthorized;
                case System.Net.HttpStatusCode.Forbidden:
                    return DataOutcome.Forbidden;
                case System.Net.HttpStatusCode.NotFound:
                    return DataOutcome.NotFound;
                case System.Net.HttpStatusCode.Conflict:
                    return DataOutcome.Conflict;
                case System.Net.HttpStatusCode.BadRequest:
                    return DataOutcome.ValidationFailed;
                case (System.Net.HttpStatusCode)429:
                    return DataOutcome.RateLimited;
                case System.Net.HttpStatusCode.BadGateway:
                case System.Net.HttpStatusCode.ServiceUnavailable:
                case System.Net.HttpStatusCode.GatewayTimeout:
                    return DataOutcome.ServerUnavailable;
                default:
                    return DataOutcome.Failure;
            }
        }

        private static DataError TryParseError(string content, int httpCode, string? correlationId)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return DataError.Create("HTTP_" + httpCode, "Server returned HTTP " + httpCode, correlationId, httpCode);
            }

            try
            {
                using (var doc = JsonDocument.Parse(content))
                {
                    var root = doc.RootElement;
                    string code = "HTTP_" + httpCode;
                    string message = "Request failed with HTTP " + httpCode;

                    if (root.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.String)
                        code = codeEl.GetString() ?? code;
                    else if (root.TryGetProperty("errorCode", out var errCodeEl) && errCodeEl.ValueKind == JsonValueKind.String)
                        code = errCodeEl.GetString() ?? code;
                    else if (root.TryGetProperty("error", out var errorObj) && errorObj.ValueKind == JsonValueKind.Object)
                    {
                        if (errorObj.TryGetProperty("code", out var innerCode))
                            code = innerCode.GetString() ?? innerCode.ToString();
                    }

                    if (root.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String)
                        message = msgEl.GetString() ?? message;
                    else if (root.TryGetProperty("error", out var errEl))
                    {
                        if (errEl.ValueKind == JsonValueKind.String)
                            message = errEl.GetString() ?? message;
                        else if (errEl.ValueKind == JsonValueKind.Object && errEl.TryGetProperty("message", out var innerMsg))
                            message = innerMsg.GetString() ?? message;
                    }

                    return DataError.Create(code, message, correlationId, httpCode);
                }
            }
            catch
            {
                return DataError.Create("HTTP_" + httpCode, content, correlationId, httpCode);
            }
        }
    }
}
