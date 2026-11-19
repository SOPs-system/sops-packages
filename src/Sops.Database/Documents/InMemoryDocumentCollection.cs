using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sops.Database.Documents
{
    /// <summary>
    /// Thread-safe in-memory document collection implementation for testing and lightweight document storage.
    /// </summary>
    /// <typeparam name="TDocument">The type of document stored in the collection.</typeparam>
    /// <typeparam name="TId">The type of unique identifier for the document.</typeparam>
    public class InMemoryDocumentCollection<TDocument, TId> : IDocumentCollection<TDocument, TId>
        where TId : notnull
    {
        private readonly ConcurrentDictionary<TId, TDocument> _storage = new ConcurrentDictionary<TId, TDocument>();

        /// <inheritdoc />
        public string CollectionName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryDocumentCollection{TDocument, TId}"/> class.
        /// </summary>
        /// <param name="collectionName">The collection name.</param>
        public InMemoryDocumentCollection(string collectionName)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                throw new ArgumentException("Collection name cannot be null or whitespace.", nameof(collectionName));
            }

            CollectionName = collectionName;
        }

        /// <inheritdoc />
        public Task<TDocument?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            cancellationToken.ThrowIfCancellationRequested();

            _storage.TryGetValue(id, out TDocument? doc);
            return Task.FromResult<TDocument?>(doc);
        }

        /// <inheritdoc />
        public Task InsertAsync(TId id, TDocument document, CancellationToken cancellationToken = default)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (document == null) throw new ArgumentNullException(nameof(document));
            cancellationToken.ThrowIfCancellationRequested();

            if (!_storage.TryAdd(id, document))
            {
                throw new InvalidOperationException($"Document with key '{id}' already exists in collection '{CollectionName}'.");
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task UpsertAsync(TId id, TDocument document, CancellationToken cancellationToken = default)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (document == null) throw new ArgumentNullException(nameof(document));
            cancellationToken.ThrowIfCancellationRequested();

            _storage[id] = document;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<bool> DeleteAsync(TId id, CancellationToken cancellationToken = default)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            cancellationToken.ThrowIfCancellationRequested();

            bool removed = _storage.TryRemove(id, out _);
            return Task.FromResult(removed);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<TDocument>> FindAsync(Func<TDocument, bool> predicate, CancellationToken cancellationToken = default)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<TDocument> matches = _storage.Values.Where(predicate).ToList();
            return Task.FromResult(matches);
        }

        /// <inheritdoc />
        public Task<long> CountAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult((long)_storage.Count);
        }
    }

    /// <summary>
    /// Thread-safe in-memory document collection implementation with string identifiers.
    /// </summary>
    /// <typeparam name="TDocument">The type of document stored in the collection.</typeparam>
    public sealed class InMemoryDocumentCollection<TDocument> : InMemoryDocumentCollection<TDocument, string>, IDocumentCollection<TDocument>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryDocumentCollection{TDocument}"/> class.
        /// </summary>
        /// <param name="collectionName">The collection name.</param>
        public InMemoryDocumentCollection(string collectionName)
            : base(collectionName)
        {
        }
    }
}
