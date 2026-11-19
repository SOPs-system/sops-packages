using System;
using System.Collections.Generic;
using System.Text;
using Sops.DataLayer.Configuration;

namespace Sops.DataLayer.Endpoints
{
    /// <summary>
    /// Resolves endpoints to absolute URLs enforcing scheme security policies (HTTPS required for cloud),
    /// strict URL canonicalization, SSRF defenses, and host allowlist validation.
    /// </summary>
    public class EndpointResolver : IEndpointResolver
    {
        private readonly DataLayerOptions _options;

        public EndpointResolver(DataLayerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public Uri ResolveUri(EndpointDefinition endpoint, IDictionary<string, string>? queryParameters = null)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));

            Uri baseUri = endpoint.Target switch
            {
                BackendTarget.LocalServer => _options.LocalServerAddress,
                BackendTarget.GlobalApi => _options.GlobalApiAddress,
                _ => _options.LocalServerAddress
            };

            // Security check 1: UserInfo credentials forbidden in base URI (prevents credential forwarding/leakage)
            if (!string.IsNullOrEmpty(baseUri.UserInfo))
            {
                throw new InvalidOperationException($"Security violation: Base URI '{baseUri}' must not contain user credentials (UserInfo).");
            }

            // Security check 2: Global API must be HTTPS (or loopback in local dev/testing)
            if (endpoint.Target == BackendTarget.GlobalApi &&
                !baseUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) &&
                !baseUri.IsLoopback)
            {
                throw new InvalidOperationException($"Global cloud API endpoint '{endpoint.Id}' must use HTTPS. Configured address was: {baseUri}");
            }

            // Security check 3: Local server must be HTTP or HTTPS
            if (endpoint.Target == BackendTarget.LocalServer &&
                !baseUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                !baseUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Local server endpoint '{endpoint.Id}' must use HTTP or HTTPS. Configured address was: {baseUri}");
            }

            string relativeRoute = endpoint.Route.TrimStart('/');
            var remainingQueryParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (queryParameters != null)
            {
                foreach (var kvp in queryParameters)
                {
                    string placeholder = "{" + kvp.Key + "}";
                    if (relativeRoute.IndexOf(placeholder, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string rawValue = kvp.Value ?? string.Empty;
                        // Prevent path separator and directory traversal injection into route parameter
                        if (rawValue.Contains("..") || rawValue.Contains("/") || rawValue.Contains("\\") ||
                            rawValue.IndexOf("%2f", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            rawValue.IndexOf("%5c", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            rawValue.IndexOf("%2e", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            throw new ArgumentException($"Route parameter '{kvp.Key}' contains prohibited path traversal sequences.");
                        }

                        string safeValue = Uri.EscapeDataString(rawValue);
                        if (safeValue.Contains("%2F") || safeValue.Contains("%2f") || safeValue.Contains("%5C") || safeValue.Contains("%5c"))
                        {
                            throw new ArgumentException($"Route parameter '{kvp.Key}' contains prohibited path traversal sequences.");
                        }
                        relativeRoute = ReplaceCaseInsensitive(relativeRoute, placeholder, safeValue);
                    }
                    else
                    {
                        remainingQueryParams[kvp.Key] = kvp.Value ?? string.Empty;
                    }
                }
            }

            string baseString = baseUri.ToString().TrimEnd('/') + "/" + relativeRoute;

            if (remainingQueryParams.Count > 0)
            {
                var sb = new StringBuilder(baseString);
                sb.Append(baseString.Contains("?") ? "&" : "?");

                bool first = true;
                foreach (var kvp in remainingQueryParams)
                {
                    if (string.IsNullOrEmpty(kvp.Key)) continue;

                    if (!first) sb.Append("&");
                    sb.Append(Uri.EscapeDataString(kvp.Key));
                    sb.Append("=");
                    sb.Append(Uri.EscapeDataString(kvp.Value ?? string.Empty));
                    first = false;
                }

                baseString = sb.ToString();
            }

            if (!Uri.TryCreate(baseString, UriKind.Absolute, out var finalUri))
            {
                throw new InvalidOperationException($"Failed to construct valid absolute URI from route '{baseString}'.");
            }

            // Security check 4: Final URI must not contain UserInfo
            if (!string.IsNullOrEmpty(finalUri.UserInfo))
            {
                throw new InvalidOperationException($"Security violation: Resolved URI '{finalUri}' must not contain user credentials (UserInfo).");
            }

            // SSRF Defense 1: Host and Port must match the intended base URL (prevents @evil.com or //evil.com injection)
            if (!string.Equals(finalUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase) || finalUri.Port != baseUri.Port)
            {
                throw new InvalidOperationException($"SSRF attack detected: Resolved URI host '{finalUri.Host}:{finalUri.Port}' does not match expected target host '{baseUri.Host}:{baseUri.Port}'.");
            }

            // SSRF Defense 2: Host must be present in approved AllowedHosts or be an authorized loopback / private LAN IP
            bool isHostAllowed = false;
            if (_options.AllowedHosts != null && _options.AllowedHosts.Contains(finalUri.Host))
            {
                isHostAllowed = true;
            }
            else if (IsAuthorizedPrivateLanOrLoopback(finalUri.Host))
            {
                isHostAllowed = true;
            }

            if (!isHostAllowed)
            {
                throw new InvalidOperationException($"Destination host '{finalUri.Host}' is not in the approved AllowedHosts allowlist and is not an authorized local endpoint.");
            }

            // SSRF Defense 3: Canonical path must not contain directory traversal
            if (finalUri.AbsolutePath.Contains("..") || finalUri.AbsolutePath.Contains("\\") ||
                finalUri.OriginalString.IndexOf("%2e%2e", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException("SSRF attack detected: Canonical URI path contains directory traversal sequences.");
            }

            return finalUri;
        }

        private static bool IsAuthorizedPrivateLanOrLoopback(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;

            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                host.Equals("127.0.0.1", StringComparison.Ordinal) ||
                host.Equals("::1", StringComparison.Ordinal))
            {
                return true;
            }

            if (System.Net.IPAddress.TryParse(host, out var ip))
            {
                if (System.Net.IPAddress.IsLoopback(ip)) return true;

                byte[] bytes = ip.GetAddressBytes();
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    // 10.0.0.0/8
                    if (bytes[0] == 10) return true;
                    // 172.16.0.0/12
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                    // 192.168.0.0/16
                    if (bytes[0] == 192 && bytes[1] == 168) return true;
                    // 169.254.0.0/16 (link-local)
                    if (bytes[0] == 169 && bytes[1] == 254) return true;
                }
            }

            return false;
        }

        private static string ReplaceCaseInsensitive(string input, string search, string replacement)
        {
            int index = input.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return input;

            var sb = new StringBuilder();
            int previousIndex = 0;

            while (index >= 0)
            {
                sb.Append(input.Substring(previousIndex, index - previousIndex));
                sb.Append(replacement);
                previousIndex = index + search.Length;
                index = input.IndexOf(search, previousIndex, StringComparison.OrdinalIgnoreCase);
            }

            sb.Append(input.Substring(previousIndex));
            return sb.ToString();
        }
    }
}
