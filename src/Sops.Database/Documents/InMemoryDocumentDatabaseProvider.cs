using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.Database.Abstractions;
using Sops.Database.Configuration;

namespace Sops.Database.Documents
{
    /// <summary>
    /// In-memory implementation of <see cref="IDocumentDatabaseProvider"/> designed for fast unit testing and isolated environments.
    /// </summary>
    public sealed class InMemoryDocumentDatabaseProvider : IDocumentDatabaseProvider
    {
        private readonly ConcurrentDictionary<string, object> _collections = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger? _logger;
        private bool _disposed;

        /// <inheritdoc />
        public string ProviderName => "InMemoryDocument";

        /// <inheritdoc />
        public DatabaseCapabilities Capabilities { get; } = DatabaseCapabilities.Document(
            supportsTransactions: false,
            supportsHealthChecks: true,
            supportsProviderRetries: false);

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryDocumentDatabaseProvider"/> class.
        /// </summary>
        /// <param name="settings">Optional database settings.</param>
        /// <param name="logger">Optional logger.</param>
        public InMemoryDocumentDatabaseProvider(DatabaseSettings? settings = null, ILogger? logger = null)
        {
            _logger = logger;
            _logger?.LogInformation("Initialized InMemoryDocumentDatabaseProvider.");
        }

        /// <inheritdoc />
        public IDocumentCollection<TDocument> GetCollection<TDocument>(string collectionName)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                throw new ArgumentException("Collection name cannot be null or whitespace.", nameof(collectionName));
            }

            ThrowIfDisposed();

            return (IDocumentCollection<TDocument>)_collections.GetOrAdd(
                collectionName,
                name => new InMemoryDocumentCollection<TDocument>(name));
        }

        /// <inheritdoc />
        public IDocumentCollection<TDocument, TId> GetCollection<TDocument, TId>(string collectionName)
            where TId : notnull
        {
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                throw new ArgumentException("Collection name cannot be null or whitespace.", nameof(collectionName));
            }

            ThrowIfDisposed();

            return (IDocumentCollection<TDocument, TId>)_collections.GetOrAdd(
                $"{collectionName}::{typeof(TId).Name}",
                _ => new InMemoryDocumentCollection<TDocument, TId>(collectionName));
        }

        /// <inheritdoc />
        public Task<DatabaseHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var stopwatch = Stopwatch.StartNew();
            int count = _collections.Count;
            stopwatch.Stop();

            return Task.FromResult(DatabaseHealthResult.Healthy(
                stopwatch.Elapsed,
                $"InMemoryDocument provider is healthy ({count} active collections)."));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(InMemoryDocumentDatabaseProvider), "The document database provider has been disposed.");
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _collections.Clear();
            }
        }
    }
}
