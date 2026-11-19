using System;
using Sops.DataLayer.Endpoints;

namespace Sops.DataLayer.Offline
{
    /// <summary>
    /// Policies governing which operations are permitted during server or cloud network disconnection.
    /// </summary>
    public static class OfflineOperationPolicy
    {
        public static bool IsPermittedOffline(EndpointDefinition endpoint)
        {
            if (endpoint == null) return false;

            switch (endpoint.OfflinePolicy)
            {
                case EndpointOfflinePolicy.CacheFallback:
                case EndpointOfflinePolicy.QueueOfflineAllowed:
                    return true;
                case EndpointOfflinePolicy.OnlineOnly:
                default:
                    return false;
            }
        }

        public static bool CanServeStaleCache(EndpointDefinition endpoint)
        {
            if (endpoint == null) return false;

            return endpoint.CachePolicy != null && endpoint.CachePolicy.IsCacheable;
        }
    }
}
