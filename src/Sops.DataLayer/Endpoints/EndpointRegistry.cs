using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace Sops.DataLayer.Endpoints
{
    /// <summary>
    /// Thread-safe registry containing all verified SOPs client endpoints.
    /// Defends against SSRF vectors and validates against duplicate registrations.
    /// </summary>
    public sealed class EndpointRegistry : IEndpointRegistry
    {
        private readonly ConcurrentDictionary<string, EndpointDefinition> _endpoints =
            new ConcurrentDictionary<string, EndpointDefinition>(StringComparer.OrdinalIgnoreCase);

        public EndpointRegistry()
        {
            RegisterDefaults();
        }

        public void Register(EndpointDefinition endpoint)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            ValidateRoute(endpoint.Route);

            if (!_endpoints.TryAdd(endpoint.Id, endpoint))
            {
                throw new InvalidOperationException($"Endpoint with ID '{endpoint.Id}' is already registered.");
            }
        }

        public EndpointDefinition Get(string endpointId)
        {
            if (string.IsNullOrWhiteSpace(endpointId)) throw new ArgumentException("Endpoint ID cannot be empty.", nameof(endpointId));

            if (!_endpoints.TryGetValue(endpointId.Trim().ToLowerInvariant(), out var endpoint))
            {
                throw new KeyNotFoundException($"Endpoint '{endpointId}' was not found in the registry.");
            }

            return endpoint;
        }

        public bool TryGet(string endpointId, out EndpointDefinition? endpoint)
        {
            endpoint = null;
            if (string.IsNullOrWhiteSpace(endpointId)) return false;

            return _endpoints.TryGetValue(endpointId.Trim().ToLowerInvariant(), out endpoint);
        }

        public IReadOnlyCollection<EndpointDefinition> All() => _endpoints.Values.ToList();

        private static void ValidateRoute(string route)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                throw new ArgumentException("Route cannot be empty.");
            }

            if (!route.StartsWith("/"))
            {
                throw new ArgumentException($"Route '{route}' must start with a forward slash ('/').");
            }

            if (route.StartsWith("//") || route.IndexOf("://", StringComparison.Ordinal) >= 0)
            {
                throw new ArgumentException($"Route '{route}' must be relative and cannot contain URI schemes or protocol-relative prefixes (SSRF prevention).");
            }

            if (route.Contains("..") || route.Contains("\\") || route.Contains("%2e") || route.Contains("%2E") || route.Contains("%5c") || route.Contains("%5C"))
            {
                throw new ArgumentException($"Route '{route}' cannot contain directory traversal sequences or backslashes (SSRF prevention).");
            }
        }

        private void RegisterDefaults()
        {
            // System & Health
            Register(new EndpointDefinition("system.health", BackendTarget.LocalServer, TransportMode.HttpRest, "GET", "/health", isIdempotent: true, requiresAuthentication: false));
            Register(new EndpointDefinition("system.info", BackendTarget.LocalServer, TransportMode.HttpRest, "GET", "/rpc/info", isIdempotent: true, requiresAuthentication: false));
            Register(new EndpointDefinition("global.health", BackendTarget.GlobalApi, TransportMode.HttpRest, "GET", "/health", isIdempotent: true, requiresAuthentication: false));

            // Authentication & Authorization
            Register(new EndpointDefinition("auth.login", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/auth/login", rpcService: "Auth", rpcMethod: "Login", requiresAuthentication: false, sensitivity: SensitivityClassification.SecuritySensitive));
            Register(new EndpointDefinition("auth.logout", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/auth/logout", rpcService: "Auth", rpcMethod: "Logout"));
            Register(new EndpointDefinition("auth.refresh", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/auth/refresh", rpcService: "Auth", rpcMethod: "Refresh", requiresAuthentication: false, sensitivity: SensitivityClassification.SecuritySensitive));
            Register(new EndpointDefinition("auth.me", BackendTarget.LocalServer, TransportMode.HttpRest, "GET", "/api/auth/me", isIdempotent: true));

            // Drug Catalog (Reads allow cache fallback)
            Register(new EndpointDefinition("drugs.search", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/drugs/searchdrugs", rpcService: "Drugs", rpcMethod: "SearchDrugs", isIdempotent: true, cachePolicy: EndpointCachePolicy.Minutes(15), offlinePolicy: EndpointOfflinePolicy.CacheFallback));
            Register(new EndpointDefinition("drugs.getbyid", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/drugs/getdrugbyid", rpcService: "Drugs", rpcMethod: "GetDrugById", isIdempotent: true, cachePolicy: EndpointCachePolicy.Minutes(30), offlinePolicy: EndpointOfflinePolicy.CacheFallback));
            Register(new EndpointDefinition("drugs.getbybarcode", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/drugs/findbybarcode", rpcService: "Drugs", rpcMethod: "FindByBarcode", isIdempotent: true, cachePolicy: EndpointCachePolicy.Minutes(30), offlinePolicy: EndpointOfflinePolicy.CacheFallback));
            Register(new EndpointDefinition("drugs.getbysku", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/drugs/findbysku", rpcService: "Drugs", rpcMethod: "FindBySku", isIdempotent: true, cachePolicy: EndpointCachePolicy.Minutes(30), offlinePolicy: EndpointOfflinePolicy.CacheFallback));

            // Patients
            Register(new EndpointDefinition("patients.search", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/patients/searchpatients", rpcService: "Patients", rpcMethod: "SearchPatients", isIdempotent: true, sensitivity: SensitivityClassification.PersonallyIdentifiable));
            Register(new EndpointDefinition("patients.getbyid", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/patients/getpatientbyid", rpcService: "Patients", rpcMethod: "GetPatientById", isIdempotent: true, sensitivity: SensitivityClassification.PersonallyIdentifiable));
            Register(new EndpointDefinition("patients.save", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/patients/savepatient", rpcService: "Patients", rpcMethod: "SavePatient", sensitivity: SensitivityClassification.PersonallyIdentifiable));

            // Inventory & Stock
            Register(new EndpointDefinition("inventory.stock", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/inventory/getstocksummary", rpcService: "Inventory", rpcMethod: "GetStockSummary", isIdempotent: true, cachePolicy: EndpointCachePolicy.Minutes(5)));
            Register(new EndpointDefinition("inventory.lots", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/inventory/getlotsfordrug", rpcService: "Inventory", rpcMethod: "GetLotsForDrug", isIdempotent: true));
            Register(new EndpointDefinition("inventory.adjust", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/inventory/adjustlot", rpcService: "Inventory", rpcMethod: "AdjustLot", sensitivity: SensitivityClassification.Financial));

            // Sales & Transactions (Mutations require idempotency)
            Register(new EndpointDefinition("sales.draft", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/sales/createsaledraft", rpcService: "Sales", rpcMethod: "CreateSaleDraft"));
            Register(new EndpointDefinition("sales.addline", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/sales/addsaleline", rpcService: "Sales", rpcMethod: "AddSaleLine"));
            Register(new EndpointDefinition("sales.addpayment", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/sales/addpayment", rpcService: "Sales", rpcMethod: "AddPayment", sensitivity: SensitivityClassification.Financial));
            Register(new EndpointDefinition("sales.complete", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/sales/completetransaction", rpcService: "Sales", rpcMethod: "CompleteTransaction", sensitivity: SensitivityClassification.Financial));
            Register(new EndpointDefinition("sales.refund", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/sales/refundtransaction", rpcService: "Sales", rpcMethod: "RefundTransaction", sensitivity: SensitivityClassification.Financial));
            Register(new EndpointDefinition("sales.list", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/sales/listtransactions", rpcService: "Sales", rpcMethod: "ListTransactions", isIdempotent: true));

            // Shifts
            Register(new EndpointDefinition("shifts.open", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/shifts/openshift", rpcService: "Shifts", rpcMethod: "OpenShift", sensitivity: SensitivityClassification.Financial));
            Register(new EndpointDefinition("shifts.close", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/shifts/closeshift", rpcService: "Shifts", rpcMethod: "CloseShift", sensitivity: SensitivityClassification.Financial));
            Register(new EndpointDefinition("shifts.expectedcash", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/shifts/getexpectedcash", rpcService: "Shifts", rpcMethod: "GetExpectedCash", isIdempotent: true, sensitivity: SensitivityClassification.Financial));

            // Categories & Warehouses & Settings
            Register(new EndpointDefinition("categories.list", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/categories/listcategories", rpcService: "Categories", rpcMethod: "ListCategories", isIdempotent: true, cachePolicy: EndpointCachePolicy.Minutes(30), offlinePolicy: EndpointOfflinePolicy.CacheFallback));
            Register(new EndpointDefinition("warehouses.list", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/warehouses/listwarehouses", rpcService: "Warehouses", rpcMethod: "ListWarehouses", isIdempotent: true, cachePolicy: EndpointCachePolicy.Minutes(30)));
            Register(new EndpointDefinition("settings.get", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/settings/getsettings", rpcService: "Settings", rpcMethod: "GetSettings", isIdempotent: true, cachePolicy: EndpointCachePolicy.Minutes(30), offlinePolicy: EndpointOfflinePolicy.CacheFallback));

            // Live Feed (SSE over HTTP)
            Register(new EndpointDefinition("live.stream", BackendTarget.LocalServer, TransportMode.ServerSentEvents, "GET", "/api/live", timeout: TimeSpan.FromHours(24)));

            // Global Cloud Endpoints
            Register(new EndpointDefinition("licensing.status", BackendTarget.GlobalApi, TransportMode.HttpRest, "GET", "/api/v1/licenses/status", isIdempotent: true, cachePolicy: EndpointCachePolicy.Minutes(60), offlinePolicy: EndpointOfflinePolicy.CacheFallback));
            Register(new EndpointDefinition("licensing.activate", BackendTarget.GlobalApi, TransportMode.HttpRest, "POST", "/api/v1/licenses/activate", sensitivity: SensitivityClassification.SecuritySensitive));
            Register(new EndpointDefinition("sync.status", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/sync/status", rpcService: "Sync", rpcMethod: "GetStatus", isIdempotent: true));
            Register(new EndpointDefinition("sync.trigger", BackendTarget.LocalServer, TransportMode.Rpc, "POST", "/rpc/sync/triggersync", rpcService: "Sync", rpcMethod: "TriggerSync"));

            // File Transfers
            Register(new EndpointDefinition("files.upload", BackendTarget.LocalServer, TransportMode.HttpRest, "POST", "/api/files/upload", timeout: TimeSpan.FromMinutes(10), maxPayloadBytes: 524288000));
            Register(new EndpointDefinition("files.download", BackendTarget.LocalServer, TransportMode.HttpRest, "GET", "/api/files/download", timeout: TimeSpan.FromMinutes(10), isIdempotent: true));
        }
    }
}
