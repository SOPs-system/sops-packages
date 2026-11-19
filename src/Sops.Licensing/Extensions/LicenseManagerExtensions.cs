using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sops.Licensing.Abstractions;

namespace Sops.Licensing.Extensions
{
    /// <summary>
    /// Extension methods providing fluent syntax for <see cref="ILicenseManager"/>.
    /// </summary>
    public static class LicenseManagerExtensions
    {
        /// <summary>
        /// Enforces that the active license grants the specified entitlement without extra context.
        /// Throws <see cref="Models.EntitlementDeniedException"/> if unauthorized.
        /// </summary>
        /// <param name="manager">The license manager.</param>
        /// <param name="entitlementKey">The entitlement key (e.g. "database.raw.read").</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static Task RequireAsync(
            this ILicenseManager manager,
            string entitlementKey,
            CancellationToken cancellationToken = default)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            return manager.RequireAsync(entitlementKey, null, cancellationToken);
        }

        /// <summary>
        /// Checks whether the active license grants the specified entitlement without extra context.
        /// </summary>
        /// <param name="manager">The license manager.</param>
        /// <param name="entitlementKey">The entitlement key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if authorized; otherwise, <c>false</c>.</returns>
        public static Task<bool> IsEntitledAsync(
            this ILicenseManager manager,
            string entitlementKey,
            CancellationToken cancellationToken = default)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            return manager.IsEntitledAsync(entitlementKey, null, cancellationToken);
        }
    }
}
