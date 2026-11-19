using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sops.Licensing.Abstractions;
using Sops.Licensing.Models;

namespace Sops.Licensing.Activation
{
    /// <summary>
    /// Client for communicating with the SOPs online licensing service and packaging offline activation requests.
    /// Uses secure HTTPS transport with strict timeouts and idempotency nonces.
    /// </summary>
    public class ActivationClient
    {
        private readonly HttpClient _httpClient;
        private readonly LicensingOptions _options;
        private readonly IDeviceIdentifier _deviceIdentifier;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivationClient"/> class.
        /// </summary>
        public ActivationClient(
            LicensingOptions options,
            IDeviceIdentifier deviceIdentifier,
            HttpClient? httpClient = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _deviceIdentifier = deviceIdentifier ?? throw new ArgumentNullException(nameof(deviceIdentifier));
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>
        /// Generates a structured offline activation request payload containing the activation code, installation ID,
        /// and privacy-preserving hardware fingerprint.
        /// </summary>
        public string CreateOfflineRequest(string activationCode)
        {
            if (string.IsNullOrWhiteSpace(activationCode))
                throw new ArgumentException("Activation code cannot be empty.", nameof(activationCode));

            var fingerprint = _deviceIdentifier.GetCurrentFingerprint();

            var req = new ActivationRequest
            {
                ProductId = _options.ProductId,
                ActivationCode = activationCode.Trim(),
                InstallationId = fingerprint.InstallationId,
                DeviceFingerprint = fingerprint.CombinedHash,
                Platform = Platform.PlatformDetector.CurrentOS.ToString(),
                ClientVersion = "1.0.0",
                TimestampUtc = DateTimeOffset.UtcNow,
                Nonce = Guid.NewGuid().ToString("N")
            };

            return JsonSerializer.Serialize(req, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        /// <summary>
        /// Executes online activation against the configured licensing server endpoint.
        /// </summary>
        public async Task<ActivationResponse> ActivateOnlineAsync(string activationCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ServerUrl))
            {
                return new ActivationResponse
                {
                    Success = false,
                    Message = "No licensing server URL configured. Use offline activation request.",
                    ErrorCode = "SERVER_NOT_CONFIGURED"
                };
            }

            string requestJson = CreateOfflineRequest(activationCode);
            string serverUrl = _options.ServerUrl!;
            string endpoint = $"{serverUrl.TrimEnd('/')}/api/v1/licenses/activate";

            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);

            string responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new ActivationResponse
                {
                    Success = false,
                    Message = $"Licensing server returned HTTP {response.StatusCode}: {responseJson}",
                    ErrorCode = response.StatusCode.ToString()
                };
            }

            var actResponse = JsonSerializer.Deserialize<ActivationResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return actResponse ?? new ActivationResponse
            {
                Success = false,
                Message = "Empty response received from licensing server.",
                ErrorCode = "EMPTY_RESPONSE"
            };
        }

        /// <summary>
        /// Executes online license refresh against the configured licensing server endpoint.
        /// </summary>
        public async Task<ActivationResponse> RefreshOnlineAsync(string licenseId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ServerUrl))
            {
                return new ActivationResponse
                {
                    Success = false,
                    Message = "No licensing server URL configured.",
                    ErrorCode = "SERVER_NOT_CONFIGURED"
                };
            }

            var fingerprint = _deviceIdentifier.GetCurrentFingerprint();
            var payload = new
            {
                product_id = _options.ProductId,
                license_id = licenseId,
                installation_id = fingerprint.InstallationId,
                device_fingerprint = fingerprint.CombinedHash,
                timestamp_utc = DateTimeOffset.UtcNow,
                nonce = Guid.NewGuid().ToString("N")
            };

            string serverUrl = _options.ServerUrl!;
            string endpoint = $"{serverUrl.TrimEnd('/')}/api/v1/licenses/refresh";
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);

            string responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new ActivationResponse
                {
                    Success = false,
                    Message = $"Refresh failed with HTTP {response.StatusCode}: {responseJson}",
                    ErrorCode = response.StatusCode.ToString()
                };
            }

            return JsonSerializer.Deserialize<ActivationResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new ActivationResponse { Success = false, Message = "Empty response." };
        }
    }
}
