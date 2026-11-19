using System;
using System.Security.Cryptography;
using System.Text;
using Sops.Device.Fingerprinting;

namespace Sops.Device.Management
{
    /// <summary>
    /// Represents server-issued cryptographic device credentials used for authenticating subsequent API requests.
    /// Strictly separated from the public, human-readable <c>DeviceCode</c>.
    /// </summary>
    public sealed class DeviceCredential
    {
        /// <summary>Authoritative server registration identifier.</summary>
        public string RegistrationId { get; }

        /// <summary>
        /// Cryptographic secret bearer or signature token issued by the server.
        /// Must be protected against unauthorized file disclosure and NEVER logged in plain text.
        /// </summary>
        public string DeviceToken { get; }

        /// <summary>
        /// Non-sensitive truncated SHA-256 thumbprint of the <see cref="DeviceToken"/>.
        /// Safe for diagnostics, system logs, and audit trails without revealing the secret credential.
        /// </summary>
        public string Thumbprint { get; }

        /// <summary>Timestamp in UTC when this credential was issued.</summary>
        public DateTimeOffset IssuedAtUtc { get; }

        /// <summary>Timestamp in UTC when this credential expires.</summary>
        public DateTimeOffset ExpiresAtUtc { get; }

        /// <summary>Credential format and schema version.</summary>
        public int TokenVersion { get; }

        /// <summary>Scoping boundary of the credential.</summary>
        public KeyScope Scope { get; }

        /// <summary>Documented trust and transport model governing this credential.</summary>
        public string TrustModel { get; }

        public DeviceCredential(
            string registrationId,
            string deviceToken,
            DateTimeOffset issuedAtUtc,
            DateTimeOffset expiresAtUtc,
            int tokenVersion = 1,
            KeyScope scope = KeyScope.Tenant,
            string? trustModel = null)
        {
            RegistrationId = !string.IsNullOrWhiteSpace(registrationId)
                ? registrationId.Trim()
                : throw new ArgumentException("RegistrationId is required.", nameof(registrationId));
            DeviceToken = !string.IsNullOrWhiteSpace(deviceToken)
                ? deviceToken.Trim()
                : throw new ArgumentException("DeviceToken is required.", nameof(deviceToken));
            IssuedAtUtc = issuedAtUtc;
            ExpiresAtUtc = expiresAtUtc;
            TokenVersion = tokenVersion;
            Scope = scope;
            TrustModel = trustModel ?? "ServerIssuedBearerToken:RequiresTls12Plus";
            Thumbprint = ComputeThumbprint(DeviceToken);
        }

        /// <summary>Whether the credential has passed its expiration time.</summary>
        public bool IsExpired(DateTimeOffset nowUtc) => nowUtc >= ExpiresAtUtc;

        /// <summary>Whether the credential is close to expiration and should be rotated.</summary>
        public bool IsExpiringSoon(DateTimeOffset nowUtc, TimeSpan threshold) =>
            nowUtc >= (ExpiresAtUtc - threshold);

        private static string ComputeThumbprint(string token)
        {
            if (string.IsNullOrEmpty(token)) return string.Empty;
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
                var sb = new StringBuilder(16);
                for (int i = 0; i < Math.Min(8, hash.Length); i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public override string ToString()
        {
            return $"[DeviceCredential: RegId={RegistrationId}, Thumbprint={Thumbprint}, Expires={ExpiresAtUtc:u}]";
        }
    }
}
