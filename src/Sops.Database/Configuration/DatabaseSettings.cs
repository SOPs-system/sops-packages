using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Sops.Configuration.Models;

namespace Sops.Database.Configuration
{
    /// <summary>
    /// Configuration options for database providers, connections, pooling, and resilience.
    /// </summary>
    public class DatabaseSettings : IValidatableObject
    {
        /// <summary>
        /// Gets or sets the database provider identifier (e.g., "PostgreSql", "SqlServer", "Sqlite", "MongoDb").
        /// </summary>
        [Required(ErrorMessage = "Database provider is required.")]
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the database connection string or provider-specific endpoint.
        /// Marked as sensitive to prevent exposure in logs and error messages.
        /// </summary>
        [Sensitive]
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the connection opening timeout in seconds.
        /// </summary>
        [Range(1, 3600, ErrorMessage = "Connection timeout must be between 1 and 3600 seconds.")]
        public int ConnectionTimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// Gets or sets the command execution timeout in seconds.
        /// </summary>
        [Range(1, 3600, ErrorMessage = "Command timeout must be between 1 and 3600 seconds.")]
        public int CommandTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets or sets a value indicating whether the database connection should be opened in read-only mode.
        /// </summary>
        public bool ReadOnly { get; set; } = false;

        /// <summary>
        /// Gets or sets an optional tenant or environment identifier for multi-tenant configurations.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Gets or sets an optional database or catalog name.
        /// </summary>
        public string? DatabaseName { get; set; }

        /// <summary>
        /// Gets or sets connection pooling settings.
        /// </summary>
        public DatabasePoolingSettings Pooling { get; set; } = new DatabasePoolingSettings();

        /// <summary>
        /// Gets or sets transient error retry settings.
        /// </summary>
        public DatabaseRetrySettings Retry { get; set; } = new DatabaseRetrySettings();

        /// <summary>
        /// Validates cross-property consistency rules for database settings.
        /// </summary>
        /// <param name="validationContext">The validation context.</param>
        /// <returns>A collection of validation errors if validation fails.</returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Provider))
            {
                yield return new ValidationResult(
                    "Database Provider cannot be null, empty, or whitespace.",
                    new[] { nameof(Provider) });
            }

            if (Pooling != null && Pooling.MinPoolSize > Pooling.MaxPoolSize)
            {
                yield return new ValidationResult(
                    $"MinPoolSize ({Pooling.MinPoolSize}) cannot be greater than MaxPoolSize ({Pooling.MaxPoolSize}).",
                    new[] { nameof(Pooling) });
            }

            if (Retry != null && Retry.InitialDelayMilliseconds > Retry.MaxDelayMilliseconds)
            {
                yield return new ValidationResult(
                    $"InitialDelayMilliseconds ({Retry.InitialDelayMilliseconds}) cannot exceed MaxDelayMilliseconds ({Retry.MaxDelayMilliseconds}).",
                    new[] { nameof(Retry) });
            }
        }

        /// <summary>
        /// Produces a safe, sanitized diagnostic summary of database settings without revealing secrets or connection strings.
        /// </summary>
        /// <returns>A string summary with sensitive information masked.</returns>
        public string ToSafeSummary()
        {
            string maskedConn = string.IsNullOrEmpty(ConnectionString) ? "[Not Configured]" : "[REDACTED]";
            string tenant = string.IsNullOrWhiteSpace(TenantId) ? "[Default]" : TenantId!;
            string db = string.IsNullOrWhiteSpace(DatabaseName) ? "[Default]" : DatabaseName!;

            return $"Provider='{Provider}', Database='{db}', Tenant='{tenant}', ReadOnly={ReadOnly}, " +
                   $"ConnTimeout={ConnectionTimeoutSeconds}s, CmdTimeout={CommandTimeoutSeconds}s, " +
                   $"Pooling={Pooling?.Enabled ?? false}, Retry={Retry?.Enabled ?? false}, ConnectionString={maskedConn}";
        }
    }
}
