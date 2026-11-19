using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Sops.Licensing.Models;

namespace Sops.Licensing.Cryptography
{
    /// <summary>
    /// Deterministic canonical serializer for cryptographic signing and verification of license certificates.
    /// Guarantees that identical certificate contents produce bit-exact canonical byte representations
    /// regardless of platform, endianness, culture, or JSON property ordering.
    /// </summary>
    internal static class CanonicalSerializer
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// Serializes the signing payload of a <see cref="LicenseCertificate"/> (all fields except <see cref="LicenseCertificate.Signature"/>)
        /// into deterministic canonical UTF-8 bytes.
        /// </summary>
        /// <param name="certificate">The license certificate.</param>
        /// <returns>Deterministic UTF-8 byte array.</returns>
        public static byte[] SerializeForSignature(LicenseCertificate certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            using (var ms = new MemoryStream())
            using (var writer = new StreamWriter(ms, Utf8NoBom))
            {
                // Write deterministic canonical key-value pairs sorted alphabetically by field name
                writer.Write("cert_version:");
                writer.Write(certificate.CertificateVersion.ToString(CultureInfo.InvariantCulture));
                writer.Write("\n");

                writer.Write("device_binding:");
                writer.Write(certificate.DeviceBinding ?? string.Empty);
                writer.Write("\n");

                // Entitlements: sorted
                writer.Write("entitlements:");
                var sortedEntitlements = (certificate.Entitlements ?? new List<string>())
                    .OrderBy(e => e, StringComparer.Ordinal)
                    .ToList();
                writer.Write(string.Join(",", sortedEntitlements));
                writer.Write("\n");

                writer.Write("expires_at_utc:");
                writer.Write(certificate.ExpiresAtUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
                writer.Write("\n");

                writer.Write("issued_at_utc:");
                writer.Write(certificate.IssuedAtUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
                writer.Write("\n");

                writer.Write("license_id:");
                writer.Write(certificate.LicenseId);
                writer.Write("\n");

                // Limits: sorted by key
                writer.Write("limits:");
                if (certificate.Limits != null && certificate.Limits.Count > 0)
                {
                    var sortedLimits = certificate.Limits
                        .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                        .Select(kvp => $"{kvp.Key}={kvp.Value.ToString(CultureInfo.InvariantCulture)}");
                    writer.Write(string.Join(";", sortedLimits));
                }
                writer.Write("\n");

                // Metadata: sorted by key
                writer.Write("metadata:");
                if (certificate.Metadata != null && certificate.Metadata.Count > 0)
                {
                    var sortedMeta = certificate.Metadata
                        .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                        .Select(kvp => $"{kvp.Key}={kvp.Value}");
                    writer.Write(string.Join(";", sortedMeta));
                }
                writer.Write("\n");

                writer.Write("offline_valid_until_utc:");
                writer.Write(certificate.OfflineValidUntilUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
                writer.Write("\n");

                writer.Write("plan_id:");
                writer.Write(certificate.PlanId);
                writer.Write("\n");

                writer.Write("product_id:");
                writer.Write(certificate.ProductId);
                writer.Write("\n");

                writer.Write("signing_key_id:");
                writer.Write(certificate.SigningKeyId);
                writer.Write("\n");

                writer.Write("subject_id:");
                writer.Write(certificate.SubjectId);
                writer.Write("\n");

                writer.Write("valid_from_utc:");
                writer.Write(certificate.ValidFromUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));

                writer.Flush();
                return ms.ToArray();
            }
        }
    }
}
