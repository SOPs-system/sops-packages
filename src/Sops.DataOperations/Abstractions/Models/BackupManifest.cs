using System;
using System.Text.Json.Serialization;

namespace Sops.DataOperations.Abstractions.Models
{
    /// <summary>
    /// Authenticated metadata manifest embedded in each encrypted backup archive.
    /// </summary>
    public sealed class BackupManifest
    {
        [JsonPropertyName("backup_id")]
        public Guid BackupId { get; set; } = Guid.NewGuid();

        [JsonPropertyName("format_version")]
        public int FormatVersion { get; set; } = 1;

        [JsonPropertyName("pharmacy_id")]
        public Guid PharmacyId { get; set; }

        [JsonPropertyName("chain_id")]
        public Guid? ChainId { get; set; }

        [JsonPropertyName("installation_id")]
        public string InstallationId { get; set; } = string.Empty;

        [JsonPropertyName("created_at_utc")]
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        [JsonPropertyName("database_schema_version")]
        public string DatabaseSchemaVersion { get; set; } = "1.0.0";

        [JsonPropertyName("application_version")]
        public string ApplicationVersion { get; set; } = "1.0.0";

        [JsonPropertyName("recovery_generation")]
        public Guid RecoveryGeneration { get; set; } = Guid.NewGuid();

        [JsonPropertyName("snapshot_checkpoint")]
        public long SnapshotCheckpoint { get; set; }

        [JsonPropertyName("archive_size_bytes")]
        public long ArchiveSizeBytes { get; set; }

        [JsonPropertyName("source_database_size_bytes")]
        public long SourceDatabaseSizeBytes { get; set; }

        [JsonPropertyName("encryption_algorithm")]
        public string EncryptionAlgorithm { get; set; } = "AES-256-CBC+HMAC-SHA256";

        [JsonPropertyName("key_identifier")]
        public string KeyIdentifier { get; set; } = "primary";

        [JsonPropertyName("checksum_sha256")]
        public string ChecksumSha256 { get; set; } = string.Empty;

        [JsonPropertyName("integrity_mac")]
        public string IntegrityMac { get; set; } = string.Empty;

        [JsonPropertyName("is_sqlcipher")]
        public bool IsSqlCipher { get; set; } = true;
    }
}
