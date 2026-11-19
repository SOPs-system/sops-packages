using System.Threading;
using System.Threading.Tasks;
using Sops.Database.Abstractions;

namespace Sops.Database.Documents
{
    /// <summary>
    /// Contract for document-oriented (NoSQL) database providers.
    /// Provides access to document collections and optional multi-document transactions.
    /// </summary>
    public interface IDocumentDatabaseProvider : IDatabaseProvider
    {
        /// <summary>
        /// Retrieves a document collection with string identifiers.
        /// </summary>
        /// <typeparam name="TDocument">The document type.</typeparam>
        /// <param name="collectionName">The collection name.</param>
        /// <returns>An instance of <see cref="IDocumentCollection{TDocument}"/>.</returns>
        IDocumentCollection<TDocument> GetCollection<TDocument>(string collectionName);

        /// <summary>
        /// Retrieves a document collection with strongly typed identifiers.
        /// </summary>
        /// <typeparam name="TDocument">The document type.</typeparam>
        /// <typeparam name="TId">The document identifier type.</typeparam>
        /// <param name="collectionName">The collection name.</param>
        /// <returns>An instance of <see cref="IDocumentCollection{TDocument, TId}"/>.</returns>
        IDocumentCollection<TDocument, TId> GetCollection<TDocument, TId>(string collectionName)
            where TId : notnull;
    }
}
