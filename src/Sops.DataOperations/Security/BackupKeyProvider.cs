using System;
using System.Security.Cryptography;
using System.Text;

namespace Sops.DataOperations.Security
{
    /// <summary>
    /// Cryptographic key material separating AES-256 encryption from HMAC-SHA256 authentication.
    /// </summary>
    public sealed class BackupKeyMaterial
    {
        public byte[] EncryptionKey { get; }
        public byte[] AuthenticationKey { get; }
        public string KeyIdentifier { get; }

        public BackupKeyMaterial(byte[] encryptionKey, byte[] authenticationKey, string keyIdentifier = "primary")
        {
            if (encryptionKey == null || encryptionKey.Length != 32)
                throw new ArgumentException("Encryption key must be exactly 32 bytes (256 bits).", nameof(encryptionKey));

            if (authenticationKey == null || authenticationKey.Length != 32)
                throw new ArgumentException("Authentication key must be exactly 32 bytes (256 bits).", nameof(authenticationKey));

            EncryptionKey = encryptionKey;
            AuthenticationKey = authenticationKey;
            KeyIdentifier = keyIdentifier ?? "primary";
        }
    }

    /// <summary>
    /// Derives independent, cryptographically isolated encryption and HMAC keys from a master secret.
    /// </summary>
    public static class BackupKeyProvider
    {
        private static readonly byte[] EncryptionSalt = Encoding.UTF8.GetBytes("Sops.DataOperations.Backup.Encryption.v1");
        private static readonly byte[] AuthSalt = Encoding.UTF8.GetBytes("Sops.DataOperations.Backup.Authentication.v1");

        /// <summary>
        /// Derives separate 256-bit encryption and authentication keys using PBKDF2 with 100,000 iterations.
        /// </summary>
        public static BackupKeyMaterial DeriveKeys(string masterSecret, string keyIdentifier = "primary")
        {
            if (string.IsNullOrWhiteSpace(masterSecret))
                throw new ArgumentException("Master secret cannot be null or empty.", nameof(masterSecret));

            string normalizedKeyId = string.IsNullOrWhiteSpace(keyIdentifier) ? "primary" : keyIdentifier;
            byte[] encSalt = Encoding.UTF8.GetBytes("Sops.DataOperations.Backup.Encryption.v1:" + normalizedKeyId);
            byte[] authSalt = Encoding.UTF8.GetBytes("Sops.DataOperations.Backup.Authentication.v1:" + normalizedKeyId);

#if NET8_0_OR_GREATER
            byte[] passwordBytes = Encoding.UTF8.GetBytes(masterSecret);
            byte[] encKey = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, encSalt, 100000, HashAlgorithmName.SHA256, 32);
            byte[] authKey = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, authSalt, 100000, HashAlgorithmName.SHA256, 32);
            return new BackupKeyMaterial(encKey, authKey, normalizedKeyId);
#else
            byte[] encKey;
            byte[] authKey;

            using (var kdfEnc = new Rfc2898DeriveBytes(masterSecret, encSalt, 100000))
            {
                encKey = kdfEnc.GetBytes(32);
            }

            using (var kdfAuth = new Rfc2898DeriveBytes(masterSecret, authSalt, 100000))
            {
                authKey = kdfAuth.GetBytes(32);
            }

            return new BackupKeyMaterial(encKey, authKey, normalizedKeyId);
#endif
        }

        /// <summary>
        /// Generates a cryptographically secure random 32-byte master passphrase.
        /// </summary>
        public static string GenerateRandomMasterSecret()
        {
            byte[] bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes);
        }
    }
}
