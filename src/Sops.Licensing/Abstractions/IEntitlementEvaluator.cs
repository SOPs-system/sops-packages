using System.Collections.Generic;
using Sops.Licensing.Models;

namespace Sops.Licensing.Abstractions
{
    /// <summary>
    /// Evaluates feature, module, and operational entitlements against an active license certificate.
    /// Operates under a strict default-deny model.
    /// </summary>
    public interface IEntitlementEvaluator
    {
        /// <summary>
        /// Evaluates whether an entitlement key is authorized for the given license certificate.
        /// Supports exact keys ("database.raw.read"), wildcards ("database.*"), explicit denials ("!database.export"),
        /// and numeric limits.
        /// </summary>
        /// <param name="certificate">The verified active license certificate.</param>
        /// <param name="entitlementKey">The requested permission or feature string.</param>
        /// <param name="context">Optional context attributes for limit or quota verification.</param>
        /// <returns>A detailed evaluation result explaining approval or denial.</returns>
        EntitlementEvaluationResult Evaluate(
            LicenseCertificate? certificate,
            string entitlementKey,
            IDictionary<string, object>? context = null);

        /// <summary>
        /// Convenience method returning a boolean flag indicating if the entitlement is permitted.
        /// </summary>
        /// <param name="certificate">The verified active license certificate.</param>
        /// <param name="entitlementKey">The requested feature key.</param>
        /// <param name="context">Optional context attributes.</param>
        /// <returns><c>true</c> if entitled; otherwise, <c>false</c>.</returns>
        bool IsEntitled(
            LicenseCertificate? certificate,
            string entitlementKey,
            IDictionary<string, object>? context = null);
    }
}
