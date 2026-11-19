using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Sops.Licensing.Models;

namespace Sops.Licensing.Cryptography
{
    /// <summary>
    /// Asymmetric cryptographic verifier for digitally signed license certificates.
    /// Uses trusted public keys only; never requires or stores private signing keys.
    /// </summary>
    public static class CertificateVerifier
    {
        /// <summary>
        /// Verifies the digital signature on a <see cref="LicenseCertificate"/> using the specified dictionary of trusted public keys.
        /// </summary>
        /// <param name="certificate">The license certificate to verify.</param>
        /// <param name="trustedPublicKeys">Dictionary mapping KeyId to trusted public key string (XML, PEM, or Base64).</param>
        /// <returns><c>true</c> if the signature is authentic and verified; otherwise, <c>false</c>.</returns>
        public static bool VerifySignature(
            LicenseCertificate certificate,
            IDictionary<string, string> trustedPublicKeys)
        {
            if (certificate == null) return false;
            if (string.IsNullOrWhiteSpace(certificate.Signature)) return false;
            if (string.IsNullOrWhiteSpace(certificate.SigningKeyId)) return false;
            if (trustedPublicKeys == null || trustedPublicKeys.Count == 0) return false;

            if (!trustedPublicKeys.TryGetValue(certificate.SigningKeyId, out string? publicKeyStr) ||
                string.IsNullOrWhiteSpace(publicKeyStr))
            {
                return false;
            }

            try
            {
                byte[] canonicalBytes = CanonicalSerializer.SerializeForSignature(certificate);
                byte[] signatureBytes = Convert.FromBase64String(certificate.Signature);

                return VerifyDataWithKey(canonicalBytes, signatureBytes, publicKeyStr!);
            }
            catch
            {
                return false;
            }
        }

        private static bool VerifyDataWithKey(byte[] data, byte[] signature, string publicKeyString)
        {
            string trimmed = publicKeyString.Trim();

            // Try RSA XML format
            if (trimmed.StartsWith("<RSAKeyValue", StringComparison.OrdinalIgnoreCase))
            {
                using (var rsa = RSA.Create())
                {
                    rsa.FromXmlString(trimmed);
                    return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }

            // Try PEM / DER SPKI format
            if (trimmed.Contains("BEGIN PUBLIC KEY"))
            {
                string base64 = trimmed
                    .Replace("-----BEGIN PUBLIC KEY-----", string.Empty)
                    .Replace("-----END PUBLIC KEY-----", string.Empty)
                    .Replace("\r", string.Empty)
                    .Replace("\n", string.Empty)
                    .Trim();

                byte[] der = Convert.FromBase64String(base64);
                return VerifyWithDerSpki(data, signature, der);
            }

            // Try raw Base64 DER
            try
            {
                byte[] der = Convert.FromBase64String(trimmed);
                return VerifyWithDerSpki(data, signature, der);
            }
            catch
            {
                // Not raw DER
            }

            return false;
        }

        private static bool VerifyWithDerSpki(byte[] data, byte[] signature, byte[] spkiDer)
        {
            // Parse SubjectPublicKeyInfo for RSA (OID 1.2.840.113549.1.1.1)
            var rsaParams = AsnKeyParser.ExtractRsaPublicKeyFromSpki(spkiDer);
            if (rsaParams.HasValue)
            {
                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(rsaParams.Value);
                    return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Utility for signing license certificates (used by license authorities, builder tools, and test suites).
    /// </summary>
    public static class CertificateSigner
    {
        /// <summary>
        /// Generates a new RSA 2048-bit key pair.
        /// </summary>
        /// <returns>A tuple of (privateKeyXml, publicKeyXml, publicKeyPem).</returns>
        public static (string privateKeyXml, string publicKeyXml, string publicKeyPem) GenerateRsaKeyPair(int keySize = 2048)
        {
            using (var rsa = RSA.Create())
            {
                rsa.KeySize = keySize;
                string privateXml = rsa.ToXmlString(true);
                string publicXml = rsa.ToXmlString(false);

                var rsaParams = rsa.ExportParameters(false);
                byte[] spki = AsnKeyParser.EncodeRsaPublicKeyToSpki(rsaParams);
                string pem = "-----BEGIN PUBLIC KEY-----\n" + Convert.ToBase64String(spki, Base64FormattingOptions.InsertLineBreaks) + "\n-----END PUBLIC KEY-----";

                return (privateXml, publicXml, pem);
            }
        }

        /// <summary>
        /// Signs a <see cref="LicenseCertificate"/> using an RSA private key XML string.
        /// </summary>
        /// <param name="certificate">The certificate to sign.</param>
        /// <param name="privateKeyXml">The RSA private key in XML format.</param>
        public static void SignCertificate(LicenseCertificate certificate, string privateKeyXml)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));
            if (string.IsNullOrWhiteSpace(privateKeyXml)) throw new ArgumentNullException(nameof(privateKeyXml));

            byte[] canonicalBytes = CanonicalSerializer.SerializeForSignature(certificate);

            using (var rsa = RSA.Create())
            {
                rsa.FromXmlString(privateKeyXml);
                byte[] signatureBytes = rsa.SignData(canonicalBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                certificate.Signature = Convert.ToBase64String(signatureBytes);
            }
        }
    }

    /// <summary>
    /// Lightweight ASN.1 DER parser and encoder for RSA SubjectPublicKeyInfo without external dependencies.
    /// Compatible with netstandard2.0 and .NET Framework 4.8.
    /// </summary>
    internal static class AsnKeyParser
    {
        public static RSAParameters? ExtractRsaPublicKeyFromSpki(byte[] spki)
        {
            try
            {
                // Simple DER parser for SubjectPublicKeyInfo
                // SEQUENCE { SEQUENCE { OID, NULL }, BIT STRING { SEQUENCE { INTEGER n, INTEGER e } } }
                int index = 0;
                if (spki[index++] != 0x30) return null; // SEQUENCE
                ReadLength(spki, ref index);

                if (spki[index++] != 0x30) return null; // AlgorithmIdentifier SEQUENCE
                int algLen = ReadLength(spki, ref index);
                index += algLen; // Skip AlgorithmIdentifier

                if (spki[index++] != 0x03) return null; // BIT STRING
                ReadLength(spki, ref index);
                index++; // Skip unused bits byte

                if (spki[index++] != 0x30) return null; // RSAPublicKey SEQUENCE
                ReadLength(spki, ref index);

                // Modulus INTEGER
                if (spki[index++] != 0x02) return null;
                int modLen = ReadLength(spki, ref index);
                byte[] mod = new byte[modLen];
                Buffer.BlockCopy(spki, index, mod, 0, modLen);
                index += modLen;

                // Strip leading 0x00 if present
                if (mod.Length > 0 && mod[0] == 0x00)
                {
                    byte[] strippedMod = new byte[mod.Length - 1];
                    Buffer.BlockCopy(mod, 1, strippedMod, 0, strippedMod.Length);
                    mod = strippedMod;
                }

                // Exponent INTEGER
                if (spki[index++] != 0x02) return null;
                int expLen = ReadLength(spki, ref index);
                byte[] exp = new byte[expLen];
                Buffer.BlockCopy(spki, index, exp, 0, expLen);

                return new RSAParameters
                {
                    Modulus = mod,
                    Exponent = exp
                };
            }
            catch
            {
                return null;
            }
        }

        public static byte[] EncodeRsaPublicKeyToSpki(RSAParameters rsaParams)
        {
            byte[] mod = rsaParams.Modulus ?? Array.Empty<byte>();
            byte[] exp = rsaParams.Exponent ?? Array.Empty<byte>();

            // If high bit of modulus is set, prepend 0x00
            byte[] encodedMod = (mod.Length > 0 && (mod[0] & 0x80) != 0)
                ? PrependByte(mod, 0x00)
                : mod;

            byte[] modInt = EncodeAsnInteger(encodedMod);
            byte[] expInt = EncodeAsnInteger(exp);

            byte[] rsaKeySeq = EncodeAsnSequence(Combine(modInt, expInt));
            byte[] bitString = EncodeAsnBitString(rsaKeySeq);

            // RSA OID: 1.2.840.113549.1.1.1 -> 06 09 2A 86 48 86 F7 0D 01 01 01, NULL: 05 00
            byte[] rsaOid = new byte[] { 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00 };
            byte[] algSeq = EncodeAsnSequence(rsaOid);

            return EncodeAsnSequence(Combine(algSeq, bitString));
        }

        private static int ReadLength(byte[] data, ref int index)
        {
            int b = data[index++];
            if ((b & 0x80) == 0) return b;

            int count = b & 0x7F;
            int length = 0;
            for (int i = 0; i < count; i++)
            {
                length = (length << 8) | data[index++];
            }
            return length;
        }

        private static byte[] EncodeAsnInteger(byte[] value)
        {
            return Combine(new byte[] { 0x02 }, EncodeLength(value.Length), value);
        }

        private static byte[] EncodeAsnSequence(byte[] content)
        {
            return Combine(new byte[] { 0x30 }, EncodeLength(content.Length), content);
        }

        private static byte[] EncodeAsnBitString(byte[] content)
        {
            return Combine(new byte[] { 0x03 }, EncodeLength(content.Length + 1), new byte[] { 0x00 }, content);
        }

        private static byte[] EncodeLength(int length)
        {
            if (length < 128) return new byte[] { (byte)length };

            if (length <= 0xFF) return new byte[] { 0x81, (byte)length };

            return new byte[] { 0x82, (byte)(length >> 8), (byte)(length & 0xFF) };
        }

        private static byte[] PrependByte(byte[] source, byte b)
        {
            byte[] res = new byte[source.Length + 1];
            res[0] = b;
            Buffer.BlockCopy(source, 0, res, 1, source.Length);
            return res;
        }

        private static byte[] Combine(params byte[][] arrays)
        {
            int total = 0;
            foreach (var a in arrays) total += a.Length;
            byte[] res = new byte[total];
            int offset = 0;
            foreach (var a in arrays)
            {
                Buffer.BlockCopy(a, 0, res, offset, a.Length);
                offset += a.Length;
            }
            return res;
        }
    }
}
