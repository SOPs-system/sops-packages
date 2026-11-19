using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sops.Licensing.Abstractions;
using Sops.Licensing.Device;
using Sops.Licensing.Entitlements;
using Sops.Licensing.Models;
using Sops.Licensing.Storage;
using Sops.Licensing.Validation;

namespace Sops.Licensing.Activation
{
    /// <summary>
    /// Production implementation of <see cref="ILicenseManager"/> providing complete lifecycle management
    /// for software licenses: activation, validation, feature entitlement enforcement, and secure storage.
    /// Supports both Dependency Injection and standalone instantiation.
    /// </summary>
    public class LicenseManager : ILicenseManager
    {
        private const string ActiveCertificateStorageKey = "license.active.certificate";

        private readonly LicensingOptions _options;
        private readonly ISecureStorage _secureStorage;
        private readonly ILicenseValidator _validator;
        private readonly IEntitlementEvaluator _evaluator;
        private readonly IDeviceIdentifier _deviceIdentifier;
        private readonly ActivationClient _activationClient;
        private readonly ILogger<LicenseManager> _logger;

        private LicenseCertificate? _cachedCertificate;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="LicenseManager"/> class with explicit dependencies or defaults.
        /// </summary>
        public LicenseManager(
            LicensingOptions? options = null,
            ISecureStorage? secureStorage = null,
            ILicenseValidator? validator = null,
            IEntitlementEvaluator? evaluator = null,
            IDeviceIdentifier? deviceIdentifier = null,
            ILogger<LicenseManager>? logger = null)
        {
            _options = options ?? new LicensingOptions();
            _secureStorage = secureStorage ?? SecureStorageFactory.CreateStorage(_options);
            _deviceIdentifier = deviceIdentifier ?? new DeviceIdentifier(_secureStorage);
            _validator = validator ?? new LicenseValidator(_options, _deviceIdentifier, _secureStorage);
            _evaluator = evaluator ?? new EntitlementEvaluator();
            _activationClient = new ActivationClient(_options, _deviceIdentifier);
            _logger = logger ?? NullLogger<LicenseManager>.Instance;
        }

        /// <inheritdoc />
        public async Task<LicenseActivationResult> ActivateAsync(string activationCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(activationCode))
            {
                return LicenseActivationResult.Failed("Activation code cannot be empty.");
            }

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string trimmed = activationCode.Trim();

                // If the activation code is actually a direct raw signed certificate JSON:
                if (trimmed.StartsWith("{") && trimmed.Contains("license_id"))
                {
                    return await InstallAndValidateCertificateRawAsync(trimmed, cancellationToken).ConfigureAwait(false);
                }

                // Execute online activation
                _logger.LogInformation("Attempting online activation for product: {ProductId}", _options.ProductId);
                var response = await _activationClient.ActivateOnlineAsync(trimmed, cancellationToken).ConfigureAwait(false);

                if (!response.Success || string.IsNullOrWhiteSpace(response.CertificateRaw))
                {
                    return LicenseActivationResult.Failed(
                        $"Activation failed: {response.Message ?? "Unknown error"}" +
                        (!string.IsNullOrEmpty(response.ErrorCode) ? $" ({response.ErrorCode})" : string.Empty));
                }

                return await InstallAndValidateCertificateRawAsync(response.CertificateRaw!, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<LicenseValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
        {
            var cert = await GetActiveLicenseAsync(cancellationToken).ConfigureAwait(false);
            if (cert == null)
            {
                return LicenseValidationResult.Failure(LicenseStatus.Missing, "No license certificate installed.");
            }

            return await _validator.ValidateAsync(cert, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task RequireAsync(
            string entitlementKey,
            IDictionary<string, object>? context = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(entitlementKey))
            {
                throw new ArgumentException("Entitlement key cannot be empty.", nameof(entitlementKey));
            }

            var validation = await ValidateAsync(cancellationToken).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                throw new EntitlementDeniedException(
                    entitlementKey,
                    $"Operation denied: License is not valid ({validation.Status}: {validation.Message}).");
            }

            var evaluation = _evaluator.Evaluate(validation.Certificate, entitlementKey, context);
            if (!evaluation.IsAllowed)
            {
                throw new EntitlementDeniedException(entitlementKey, evaluation.Reason);
            }
        }

        /// <inheritdoc />
        public async Task<bool> IsEntitledAsync(
            string entitlementKey,
            IDictionary<string, object>? context = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(entitlementKey)) return false;

            var validation = await ValidateAsync(cancellationToken).ConfigureAwait(false);
            if (!validation.IsValid) return false;

            var evaluation = _evaluator.Evaluate(validation.Certificate, entitlementKey, context);
            return evaluation.IsAllowed;
        }

        /// <inheritdoc />
        public async Task<LicenseCertificate?> GetActiveLicenseAsync(CancellationToken cancellationToken = default)
        {
            if (_cachedCertificate != null)
            {
                return _cachedCertificate;
            }

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_cachedCertificate != null)
                {
                    return _cachedCertificate;
                }

                string? rawCert = await _secureStorage.GetStringAsync(ActiveCertificateStorageKey, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(rawCert))
                {
                    return null;
                }

                _cachedCertificate = JsonSerializer.Deserialize<LicenseCertificate>(rawCert!, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return _cachedCertificate;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed loading active license certificate from secure storage.");
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeactivateAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _cachedCertificate = null;
                return await _secureStorage.DeleteAsync(ActiveCertificateStorageKey, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc />
        public Task<string> CreateOfflineActivationRequestAsync(string activationCode, CancellationToken cancellationToken = default)
        {
            string req = _activationClient.CreateOfflineRequest(activationCode);
            return Task.FromResult(req);
        }

        /// <inheritdoc />
        public async Task<LicenseActivationResult> ActivateOfflineResponseAsync(string signedResponseJson, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(signedResponseJson))
            {
                return LicenseActivationResult.Failed("Offline response payload cannot be empty.");
            }

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string trimmed = signedResponseJson.Trim();

                // If response is wrapped in ActivationResponse object
                if (trimmed.Contains("\"certificate_raw\""))
                {
                    var response = JsonSerializer.Deserialize<ActivationResponse>(trimmed, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (response == null || !response.Success || string.IsNullOrWhiteSpace(response.CertificateRaw))
                    {
                        return LicenseActivationResult.Failed(response?.Message ?? "Invalid offline activation response payload.");
                    }

                    return await InstallAndValidateCertificateRawAsync(response.CertificateRaw!, cancellationToken).ConfigureAwait(false);
                }

                // Direct LicenseCertificate JSON
                return await InstallAndValidateCertificateRawAsync(trimmed, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<LicenseRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
        {
            var cert = await GetActiveLicenseAsync(cancellationToken).ConfigureAwait(false);
            if (cert == null)
            {
                return new LicenseRefreshResult { Success = false, Message = "No active license to refresh." };
            }

            var response = await _activationClient.RefreshOnlineAsync(cert.LicenseId, cancellationToken).ConfigureAwait(false);
            if (!response.Success || string.IsNullOrWhiteSpace(response.CertificateRaw))
            {
                return new LicenseRefreshResult
                {
                    Success = false,
                    Message = $"Refresh failed: {response.Message}"
                };
            }

            var activation = await InstallAndValidateCertificateRawAsync(response.CertificateRaw!, cancellationToken).ConfigureAwait(false);
            return new LicenseRefreshResult
            {
                Success = activation.Success,
                Message = activation.Message,
                Certificate = activation.Certificate
            };
        }

        private async Task<LicenseActivationResult> InstallAndValidateCertificateRawAsync(string rawCertJson, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateRawCertificateAsync(rawCertJson, cancellationToken).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                return LicenseActivationResult.Failed($"License certificate is invalid ({validation.Status}: {validation.Message})", validation);
            }

            var cert = validation.Certificate!;

            // Store in secure storage
            bool exists = await _secureStorage.ExistsAsync(ActiveCertificateStorageKey, cancellationToken).ConfigureAwait(false);
            if (exists)
            {
                await _secureStorage.UpdateStringAsync(ActiveCertificateStorageKey, rawCertJson, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _secureStorage.SaveStringAsync(ActiveCertificateStorageKey, rawCertJson, cancellationToken).ConfigureAwait(false);
            }

            _cachedCertificate = cert;
            _logger.LogInformation("License activated successfully for license ID: {LicenseId}", cert.LicenseId);

            return LicenseActivationResult.Succeeded(cert, validation);
        }
    }
}
