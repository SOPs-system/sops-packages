using System.Threading;
using System.Threading.Tasks;
using Sops.Licensing.Models;

namespace Sops.Licensing.Abstractions
{
    /// <summary>
    /// Validates digital cryptographic signatures, date validity, device binding, and anti-tampering rules on license certificates.
    /// </summary>
    public interface ILicenseValidator
    {
        /// <summary>
        /// Validates a strongly-typed <see cref="LicenseCertificate"/> against local cryptographic trust and device identity.
        /// </summary>
        /// <param name="certificate">The certificate model to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A detailed validation outcome.</returns>
        Task<LicenseValidationResult> ValidateAsync(
            LicenseCertificate certificate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Parses and validates raw serialized certificate JSON text.
        /// </summary>
        /// <param name="rawCertificateJson">The raw certificate JSON string.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A detailed validation outcome.</returns>
        Task<LicenseValidationResult> ValidateRawCertificateAsync(
            string rawCertificateJson,
            CancellationToken cancellationToken = default);
    }
}
