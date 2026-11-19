using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Transport
{
    /// <summary>
    /// HTTP REST transport implementation.
    /// </summary>
    public class HttpTransport : ITransport
    {
        private readonly HttpClient _httpClient;
        private readonly ISessionManager _sessionManager;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public HttpTransport(HttpClient httpClient, ISessionManager sessionManager)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        }

        public async Task<DataResult<TResponse>> SendAsync<TResponse>(
            EndpointDefinition endpoint,
            DataRequest request,
            Uri resolvedUri,
            CancellationToken cancellationToken)
        {
            if (string.Equals(resolvedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                Security.TlsConfiguration.EnsureTls12Supported();
            }

            var sw = Stopwatch.StartNew();
            using var httpRequest = new HttpRequestMessage(new HttpMethod(endpoint.HttpMethod), resolvedUri);

            // Attach correlation and context headers
            httpRequest.Headers.TryAddWithoutValidation(RequestHeaders.CorrelationId, request.Context.CorrelationId);
            httpRequest.Headers.TryAddWithoutValidation(RequestHeaders.RequestId, request.Context.RequestId);
            httpRequest.Headers.TryAddWithoutValidation(RequestHeaders.TerminalId, request.Context.TerminalId);
            httpRequest.Headers.TryAddWithoutValidation(RequestHeaders.ClientVersion, request.Context.ClientVersion);

            if (!string.IsNullOrWhiteSpace(request.Context.IdempotencyKey))
            {
                httpRequest.Headers.TryAddWithoutValidation(RequestHeaders.IdempotencyKey, request.Context.IdempotencyKey);
            }
            if (!string.IsNullOrWhiteSpace(request.Context.PharmacyId))
            {
                httpRequest.Headers.TryAddWithoutValidation(RequestHeaders.PharmacyId, request.Context.PharmacyId);
            }
            if (!string.IsNullOrWhiteSpace(request.Context.BranchId))
            {
                httpRequest.Headers.TryAddWithoutValidation(RequestHeaders.BranchId, request.Context.BranchId);
            }
            if (!string.IsNullOrWhiteSpace(request.Context.TenantId))
            {
                httpRequest.Headers.TryAddWithoutValidation(RequestHeaders.TenantId, request.Context.TenantId);
            }

            // Additional custom headers
            if (request.Options.AdditionalHeaders != null)
            {
                foreach (var kvp in request.Options.AdditionalHeaders)
                {
                    httpRequest.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                }
            }

            // Attach bearer token if authenticated with strict local vs cloud token separation
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

            // Serialize body if present and method allows it
            if (request.Body != null && httpRequest.Method != HttpMethod.Get && httpRequest.Method != HttpMethod.Head)
            {
                string jsonBody = JsonSerializer.Serialize(request.Body, JsonOptions);
                httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return DataResult<TResponse>.Cancelled(request.Context.CorrelationId);
            }
            catch (Exception ex)
            {
                var duration = sw.Elapsed;
                var origin = endpoint.Target == BackendTarget.LocalServer ? DataSourceOrigin.LocalServer : DataSourceOrigin.GlobalApi;
                var metadata = ResponseMetadata.Live(origin, duration, null, request.Context.CorrelationId);

                if (endpoint.IsIdempotent)
                {
                    return DataResult<TResponse>.Failure(DataOutcome.ServerUnavailable, DataError.Unavailable($"Transport communication failed: {ex.Message}", request.Context.CorrelationId), metadata);
                }

                return DataResult<TResponse>.UnknownOutcome($"Mutation request failed in-flight with network error: {ex.Message}", request.Context.CorrelationId);
            }

            sw.Stop();
            var resOrigin = endpoint.Target == BackendTarget.LocalServer ? DataSourceOrigin.LocalServer : DataSourceOrigin.GlobalApi;
            string? etag = response.Headers.ETag?.Tag;
            TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;

            var resMetadata = ResponseMetadata.Live(resOrigin, sw.Elapsed, (int)response.StatusCode, request.Context.CorrelationId, serverVersionOrETag: etag, retryAfter: retryAfter);

            return await ParseHttpResponseAsync<TResponse>(response, request.Context, resMetadata, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<DataResult<TResponse>> ParseHttpResponseAsync<TResponse>(
            HttpResponseMessage response,
            RequestContext context,
            ResponseMetadata metadata,
            CancellationToken cancellationToken)
        {
            int statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    return DataResult<TResponse>.Empty(metadata);
                }

                string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return DataResult<TResponse>.Empty(metadata);
                }

                try
                {
                    if (typeof(TResponse) == typeof(string))
                    {
                        return DataResult<TResponse>.Success((TResponse)(object)content, metadata);
                    }

                    var parsed = JsonSerializer.Deserialize<TResponse>(content, JsonOptions);
                    return DataResult<TResponse>.Success(parsed!, metadata);
                }
                catch (JsonException jex)
                {
                    return DataResult<TResponse>.Failure(
                        DataOutcome.Failure,
                        DataError.Create("json_parse_error", $"Failed parsing response JSON: {jex.Message}", context.CorrelationId, statusCode),
                        metadata);
                }
            }

            string errorBody = string.Empty;
            try
            {
                errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch { }

            DataOutcome outcome = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => DataOutcome.Unauthorized,
                HttpStatusCode.Forbidden => DataOutcome.Forbidden,
                HttpStatusCode.NotFound => DataOutcome.NotFound,
                HttpStatusCode.Conflict => DataOutcome.Conflict,
                HttpStatusCode.BadRequest => DataOutcome.ValidationFailed,
                (HttpStatusCode)429 => DataOutcome.RateLimited,
                HttpStatusCode.ServiceUnavailable => DataOutcome.ServerUnavailable,
                HttpStatusCode.GatewayTimeout => DataOutcome.Timeout,
                _ => DataOutcome.Failure
            };

            string safeMessage = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Authentication required or session expired.",
                HttpStatusCode.Forbidden => "You do not have permission to perform this action.",
                HttpStatusCode.NotFound => "The requested resource was not found.",
                HttpStatusCode.Conflict => "The action conflicts with existing state.",
                HttpStatusCode.BadRequest => "The request parameters are invalid.",
                (HttpStatusCode)429 => "Too many requests. Please try again shortly.",
                HttpStatusCode.ServiceUnavailable => "Service is temporarily unavailable.",
                _ => $"Server returned status {statusCode}"
            };

            return DataResult<TResponse>.Failure(
                outcome,
                DataError.Create(outcome.ToString().ToLowerInvariant(), safeMessage, context.CorrelationId, statusCode),
                metadata);
        }
    }
}
