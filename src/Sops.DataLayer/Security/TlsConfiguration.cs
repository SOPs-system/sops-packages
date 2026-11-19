using System;
using System.Net;

namespace Sops.DataLayer.Security
{
    /// <summary>
    /// Governs TLS transport security across .NET Framework 4.8 and modern .NET runtimes.
    /// Ensures TLS 1.2 support on Windows 7 SP1 while respecting system-default secure negotiation
    /// on Windows 10 and Windows 11 without enabling deprecated SSL/TLS protocols or bypassing certificate validation.
    /// </summary>
    public static class TlsConfiguration
    {
        // Numeric constants for SecurityProtocolType flags (compatible across all framework profiles)
        public const SecurityProtocolType Tls12 = (SecurityProtocolType)3072;
        public const SecurityProtocolType Tls13 = (SecurityProtocolType)12288;
        public const SecurityProtocolType Ssl3 = (SecurityProtocolType)48;
        public const SecurityProtocolType Tls10 = (SecurityProtocolType)192;
        public const SecurityProtocolType Tls11 = (SecurityProtocolType)768;

        private static bool _initialized;
        private static readonly object _initLock = new object();

        /// <summary>
        /// Ensures modern TLS 1.2 negotiation is active in the current process.
        /// Preserves SystemDefault on modern Windows while adding TLS 1.2 capability on Windows 7 SP1.
        /// </summary>
        public static void EnsureTls12Supported()
        {
            if (_initialized) return;

            lock (_initLock)
            {
                if (_initialized) return;

#if NETFRAMEWORK
                try
                {
                    var current = ServicePointManager.SecurityProtocol;

                    // Ensure TLS 1.2 is included in the process protocols
                    if ((current & Tls12) == 0)
                    {
                        ServicePointManager.SecurityProtocol |= Tls12;
                    }

                    // Explicitly strip deprecated/insecure protocols if present
                    if ((ServicePointManager.SecurityProtocol & Ssl3) != 0 ||
                        (ServicePointManager.SecurityProtocol & Tls10) != 0 ||
                        (ServicePointManager.SecurityProtocol & Tls11) != 0)
                    {
                        ServicePointManager.SecurityProtocol &= ~(Ssl3 | Tls10 | Tls11);
                    }
                }
                catch (NotSupportedException)
                {
                    // Running on unpatched Windows 7 SP1 missing KB3140245 (TLS 1.2 update);
                    // allow process to proceed while preserving OS SCHANNEL defaults.
                }
                catch (Exception)
                {
                    // Non-fatal fallback for restricted hosting environments
                }
#endif
                _initialized = true;
            }
        }

        /// <summary>
        /// Validates that an endpoint URI complies with transport encryption requirements.
        /// </summary>
        /// <param name="uri">Target endpoint URI.</param>
        /// <param name="requireHttps">Whether HTTPS is strictly mandatory (e.g. for cloud backend).</param>
        /// <exception cref="InvalidOperationException">Thrown if protocol requirements are violated.</exception>
        public static void ValidateTransportSecurity(Uri uri, bool requireHttps)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));

            if (requireHttps && !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) && !uri.IsLoopback)
            {
                throw new InvalidOperationException($"Transport security violation: Endpoint '{uri}' requires HTTPS encryption.");
            }

            // Forbid userinfo credentials in URI
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new InvalidOperationException($"Security violation: URI '{uri}' must not contain user credentials (UserInfo) in the authority component.");
            }
        }

        /// <summary>
        /// Checks whether TLS 1.2 is active in the current process security protocol configuration.
        /// </summary>
        public static bool IsTls12Active()
        {
#if NETFRAMEWORK
            return (ServicePointManager.SecurityProtocol & Tls12) != 0;
#else
            return true;
#endif
        }

        /// <summary>
        /// Checks whether any deprecated or insecure protocol (SSL 3.0, TLS 1.0, TLS 1.1) is enabled.
        /// </summary>
        public static bool HasInsecureProtocolsEnabled()
        {
#if NETFRAMEWORK
            var current = ServicePointManager.SecurityProtocol;
            return (current & (Ssl3 | Tls10 | Tls11)) != 0;
#else
            return false;
#endif
        }
    }
}
