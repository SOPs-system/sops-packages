using System.Collections.Generic;

namespace Sops.DataLayer.Endpoints
{
    /// <summary>
    /// Central registry of all endpoint definitions in the SOPs client.
    /// Immutable after initialization; validates against duplicates and SSRF vectors.
    /// </summary>
    public interface IEndpointRegistry
    {
        /// <summary>Retrieves an endpoint by logical operation ID, throwing if not found.</summary>
        EndpointDefinition Get(string endpointId);

        /// <summary>Tries to retrieve an endpoint by logical operation ID.</summary>
        bool TryGet(string endpointId, out EndpointDefinition? endpoint);

        /// <summary>Returns all registered endpoints.</summary>
        IReadOnlyCollection<EndpointDefinition> All();
    }
}
