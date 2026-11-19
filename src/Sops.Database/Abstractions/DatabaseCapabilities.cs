using System;

namespace Sops.Database.Abstractions
{
    /// <summary>
    /// Describes the operational capabilities supported by a database provider.
    /// Used by callers and consumers to discover features without relying on provider type names.
    /// </summary>
    public sealed class DatabaseCapabilities : IEquatable<DatabaseCapabilities>
    {
        /// <summary>
        /// Gets a value indicating whether the provider supports explicit atomic transactions (commit/rollback).
        /// </summary>
        public bool SupportsTransactions { get; }

        /// <summary>
        /// Gets a value indicating whether the provider supports relational operations (DbConnection, DbCommand, SQL).
        /// </summary>
        public bool SupportsRelationalCommands { get; }

        /// <summary>
        /// Gets a value indicating whether the provider supports document-oriented collection operations.
        /// </summary>
        public bool SupportsDocumentCollections { get; }

        /// <summary>
        /// Gets a value indicating whether the provider supports active health checks.
        /// </summary>
        public bool SupportsHealthChecks { get; }

        /// <summary>
        /// Gets a value indicating whether the provider manages transient retries internally.
        /// </summary>
        public bool SupportsProviderRetries { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseCapabilities"/> class with explicit feature flags.
        /// </summary>
        /// <param name="supportsTransactions">Whether transactions are supported.</param>
        /// <param name="supportsRelationalCommands">Whether relational commands are supported.</param>
        /// <param name="supportsDocumentCollections">Whether document collections are supported.</param>
        /// <param name="supportsHealthChecks">Whether active health checks are supported.</param>
        /// <param name="supportsProviderRetries">Whether provider-managed retries are supported.</param>
        public DatabaseCapabilities(
            bool supportsTransactions = false,
            bool supportsRelationalCommands = false,
            bool supportsDocumentCollections = false,
            bool supportsHealthChecks = true,
            bool supportsProviderRetries = false)
        {
            SupportsTransactions = supportsTransactions;
            SupportsRelationalCommands = supportsRelationalCommands;
            SupportsDocumentCollections = supportsDocumentCollections;
            SupportsHealthChecks = supportsHealthChecks;
            SupportsProviderRetries = supportsProviderRetries;
        }

        /// <summary>
        /// Creates a capability preset typical for relational database providers (e.g., PostgreSQL, SQL Server, SQLite).
        /// </summary>
        /// <param name="supportsTransactions">Whether explicit transactions are supported. Defaults to true.</param>
        /// <param name="supportsHealthChecks">Whether health checks are supported. Defaults to true.</param>
        /// <param name="supportsProviderRetries">Whether provider-managed retries are supported. Defaults to false.</param>
        /// <returns>A configured <see cref="DatabaseCapabilities"/> instance.</returns>
        public static DatabaseCapabilities Relational(
            bool supportsTransactions = true,
            bool supportsHealthChecks = true,
            bool supportsProviderRetries = false) =>
            new DatabaseCapabilities(
                supportsTransactions: supportsTransactions,
                supportsRelationalCommands: true,
                supportsDocumentCollections: false,
                supportsHealthChecks: supportsHealthChecks,
                supportsProviderRetries: supportsProviderRetries);

        /// <summary>
        /// Creates a capability preset typical for document-oriented database providers (e.g., MongoDB, Cosmos DB).
        /// </summary>
        /// <param name="supportsTransactions">Whether multi-document transactions are supported. Defaults to false.</param>
        /// <param name="supportsHealthChecks">Whether health checks are supported. Defaults to true.</param>
        /// <param name="supportsProviderRetries">Whether provider-managed retries are supported. Defaults to false.</param>
        /// <returns>A configured <see cref="DatabaseCapabilities"/> instance.</returns>
        public static DatabaseCapabilities Document(
            bool supportsTransactions = false,
            bool supportsHealthChecks = true,
            bool supportsProviderRetries = false) =>
            new DatabaseCapabilities(
                supportsTransactions: supportsTransactions,
                supportsRelationalCommands: false,
                supportsDocumentCollections: true,
                supportsHealthChecks: supportsHealthChecks,
                supportsProviderRetries: supportsProviderRetries);

        /// <inheritdoc />
        public bool Equals(DatabaseCapabilities? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return SupportsTransactions == other.SupportsTransactions &&
                   SupportsRelationalCommands == other.SupportsRelationalCommands &&
                   SupportsDocumentCollections == other.SupportsDocumentCollections &&
                   SupportsHealthChecks == other.SupportsHealthChecks &&
                   SupportsProviderRetries == other.SupportsProviderRetries;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as DatabaseCapabilities);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + SupportsTransactions.GetHashCode();
                hash = hash * 31 + SupportsRelationalCommands.GetHashCode();
                hash = hash * 31 + SupportsDocumentCollections.GetHashCode();
                hash = hash * 31 + SupportsHealthChecks.GetHashCode();
                hash = hash * 31 + SupportsProviderRetries.GetHashCode();
                return hash;
            }
        }
    }
}
