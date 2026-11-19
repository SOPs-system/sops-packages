using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sops.Database.Documents
{
    /// <summary>
    /// Contract for strongly typed document collection operations with generic document and key types.
    /// </summary>
    /// <typeparam name="TDocument">The type of document stored in the collection.</typeparam>
    /// <typeparam name="TId">The type of unique identifier for the document.</typeparam>
    public interface IDocumentCollection<TDocument, in TId>
        where TId : notnull
    {
        /// <summary>
        /// Gets the name of the document collection.
        /// </summary>
        string CollectionName { get; }

        /// <summary>
        /// Asynchronously retrieves a document by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The document instance if found; otherwise, <c>null</c>.</returns>
        Task<TDocument?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously inserts a new document with the specified identifier.
        /// Throws an <see cref="InvalidOperationException"/> if a document with the same identifier already exists.
        /// </summary>
        /// <param name="id">The unique identifier.</param>
        /// <param name="document">The document to insert.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task representing the operation.</returns>
        Task InsertAsync(TId id, TDocument document, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously inserts or updates an existing document with the specified identifier.
        /// </summary>
        /// <param name="id">The unique identifier.</param>
        /// <param name="document">The document to upsert.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task representing the operation.</returns>
        Task UpsertAsync(TId id, TDocument document, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously deletes a document with the specified identifier.
        /// </summary>
        /// <param name="id">The unique identifier.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns><c>true</c> if a document was deleted; otherwise, <c>false</c>.</returns>
        Task<bool> DeleteAsync(TId id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously searches documents in the collection matching a client predicate.
        /// </summary>
        /// <param name="predicate">A filter predicate.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A read-only list of matching documents.</returns>
        Task<IReadOnlyList<TDocument>> FindAsync(Func<TDocument, bool> predicate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously counts the total number of documents in the collection.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The total document count.</returns>
        Task<long> CountAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Contract for string-keyed document collections.
    /// </summary>
    /// <typeparam name="TDocument">The type of document stored in the collection.</typeparam>
    public interface IDocumentCollection<TDocument> : IDocumentCollection<TDocument, string>
    {
    }
}
