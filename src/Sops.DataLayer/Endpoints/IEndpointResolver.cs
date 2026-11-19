using System;
using System.Collections.Generic;

namespace Sops.DataLayer.Endpoints
{
    /// <summary>
    /// Resolves an endpoint definition into an absolute, validated URI against configured base addresses.
    /// </summary>
    public interface IEndpointResolver
    {
        /// <summary>Resolves the target absolute URI for an endpoint with optional query parameters.</summary>
        Uri ResolveUri(EndpointDefinition endpoint, IDictionary<string, string>? queryParameters = null);
    }
}
