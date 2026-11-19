using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sops.Licensing.Models;

namespace Sops.Licensing.Abstractions
{
    /// <summary>
    /// Central manager coordinating license activation, offline-first validation, entitlement enforcement,
    /// and secure license credential storage.
    /// </summary>
    public interface ILicenseManager
    {
        /// <summary>
        /// Activates the software online using an activation code, storing the signed certificate securely.
        /// </summary>
        /// <param name="activationCode">The customer activation key or token.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The activation outcome.</returns>
        Task<LicenseActivationResult> ActivateAsync(string activationCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates the currently installed license against cryptographic signatures, dates, clock rollback, and device bindings.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A detailed validation outcome.</returns>
        Task<LicenseValidationResult> ValidateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Enforces that the active license grants the specified entitlement.
        /// Throws <see cref="EntitlementDeniedException"/> if not authorized.
        /// </summary>
        /// <param name="entitlementKey">The requested permission or feature string (e.g. "database.raw.read").</param>
        /// <param name="context">Optional context attributes for quota/limit validation.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RequireAsync(
            string entitlementKey,
            IDictionary<string, object>? context = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks whether the active license grants the specified entitlement without throwing an exception.
        /// </summary>
        /// <param name="entitlementKey">The requested feature key.</param>
        /// <param name="context">Optional context attributes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if authorized; otherwise, <c>false</c>.</returns>
        Task<bool> IsEntitledAsync(
            string entitlementKey,
            IDictionary<string, object>? context = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the currently installed and verified <see cref="LicenseCertificate"/>, or <c>null</c> if none is active.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The active certificate or null.</returns>
        Task<LicenseCertificate?> GetActiveLicenseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Deactivates and removes the current license from local secure storage.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if a license was deactivated; otherwise, <c>false</c>.</returns>
        Task<bool> DeactivateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a signed or structured JSON offline activation request that can be transferred to an internet-connected machine.
        /// </summary>
        /// <param name="activationCode">The activation key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Serialized offline activation request string.</returns>
        Task<string> CreateOfflineActivationRequestAsync(string activationCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Installs and activates a signed offline activation response received from the licensing authority.
        /// </summary>
        /// <param name="signedResponseJson">The raw offline response JSON.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The activation outcome.</returns>
        Task<LicenseActivationResult> ActivateOfflineResponseAsync(string signedResponseJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes license status, extending offline validity dates if an online server is reachable.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The refresh outcome.</returns>
        Task<LicenseRefreshResult> RefreshAsync(CancellationToken cancellationToken = default);
    }
}
