using System;
using Sops.DataLayer.Endpoints;

namespace Sops.DataLayer.Resilience
{
    /// <summary>
    /// Policies governing mutation safety, duplicate suppression, and unknown outcome preservation.
    /// </summary>
    public static class IdempotencyPolicy
    {
        public static string GenerateKey(string prefix = "idemp")
        {
            return $"{prefix}_{Guid.NewGuid():N}";
        }

        public static bool IsMutation(EndpointDefinition endpoint)
        {
            if (endpoint == null) return false;

            if (endpoint.IsFinancialMutation || endpoint.RequiresIdempotencyKey || endpoint.Sensitivity == SensitivityClassification.Financial)
            {
                return true;
            }

            // In RPC over HTTP, queries (Search, Get, Find, List) use POST but are reads
            if (endpoint.Transport == TransportMode.Rpc)
            {
                if (!string.IsNullOrEmpty(endpoint.RpcMethod))
                {
                    var m = endpoint.RpcMethod!;
                    if (m.StartsWith("Get", StringComparison.OrdinalIgnoreCase) ||
                        m.StartsWith("Find", StringComparison.OrdinalIgnoreCase) ||
                        m.StartsWith("List", StringComparison.OrdinalIgnoreCase) ||
                        m.StartsWith("Search", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                var parts = endpoint.Route.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    var methodName = parts[parts.Length - 1];
                    if (methodName.StartsWith("Get", StringComparison.OrdinalIgnoreCase) ||
                        methodName.StartsWith("Find", StringComparison.OrdinalIgnoreCase) ||
                        methodName.StartsWith("List", StringComparison.OrdinalIgnoreCase) ||
                        methodName.StartsWith("Search", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (!string.IsNullOrEmpty(endpoint.HttpMethod))
            {
                var m = endpoint.HttpMethod.ToUpperInvariant();
                if (m == "POST" || m == "PUT" || m == "DELETE" || m == "PATCH")
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ShouldPreserveUnknownOutcome(EndpointDefinition endpoint, Models.Responses.DataOutcome outcome)
        {
            if (!IsMutation(endpoint)) return false;

            return outcome == Models.Responses.DataOutcome.Timeout || outcome == Models.Responses.DataOutcome.ServerUnavailable;
        }
    }
}
