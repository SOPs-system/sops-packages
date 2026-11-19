using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Models.Authentication;
using Sops.DataLayer.Models.Requests;
using Sops.DataLayer.Models.Responses;
using Sops.Drugs.Models;

namespace Sops.DataLayer.Services
{
    public sealed class DrugDataService : IDrugDataService
    {
        private readonly IDataLayerClient _client;

        public DrugDataService(IDataLayerClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public Task<DataResult<IReadOnlyList<Drug>>> ListDrugsAsync(string? query = null, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("drugs.search", new { query });
            return _client.ExecuteAsync<IReadOnlyList<Drug>>(req, cancellationToken);
        }

        public Task<DataResult<DataPage<Drug>>> ListDrugsPageAsync(int offset, int limit, string? query = null, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("drugs.search", new { query, offset, limit });
            return _client.ExecuteAsync<DataPage<Drug>>(req, cancellationToken);
        }

        public Task<DataResult<Drug?>> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("drugs.getbybarcode", new { barcode });
            return _client.ExecuteAsync<Drug?>(req, cancellationToken);
        }

        public Task<DataResult<Drug?>> FindBySkuAsync(string sku, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("drugs.getbysku", new { sku });
            return _client.ExecuteAsync<Drug?>(req, cancellationToken);
        }
    }

    public sealed class PatientDataService : IPatientDataService
    {
        private readonly IDataLayerClient _client;

        public PatientDataService(IDataLayerClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public Task<DataResult<IReadOnlyList<object>>> ListPatientsAsync(string? query = null, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("patients.search", new { query });
            return _client.ExecuteAsync<IReadOnlyList<object>>(req, cancellationToken);
        }

        public Task<DataResult<object?>> GetPatientAsync(string patientId, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("patients.getbyid", new { patientId });
            return _client.ExecuteAsync<object?>(req, cancellationToken);
        }

        public Task<DataResult<object>> SavePatientAsync(object patient, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("patients.save", patient);
            return _client.ExecuteAsync<object>(req, cancellationToken);
        }
    }

    public sealed class InventoryDataService : IInventoryDataService
    {
        private readonly IDataLayerClient _client;

        public InventoryDataService(IDataLayerClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public Task<DataResult<IReadOnlyList<object>>> GetStockSummaryAsync(CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("inventory.stock");
            return _client.ExecuteAsync<IReadOnlyList<object>>(req, cancellationToken);
        }

        public Task<DataResult<IReadOnlyList<object>>> GetLotsForDrugAsync(string drugId, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("inventory.lots", new { drugId });
            return _client.ExecuteAsync<IReadOnlyList<object>>(req, cancellationToken);
        }

        public Task<DataResult<object>> AdjustLotAsync(string lotId, int newQuantity, string reason, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("inventory.adjust", new { lotId, newQuantity, reason });
            return _client.ExecuteAsync<object>(req, cancellationToken);
        }
    }

    public sealed class TransactionDataService : ITransactionDataService
    {
        private readonly IDataLayerClient _client;

        public TransactionDataService(IDataLayerClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public Task<DataResult<object>> CreateSaleDraftAsync(string? patientId = null, string? reason = null, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("sales.draft", new { patientId, reason });
            return _client.ExecuteAsync<object>(req, cancellationToken);
        }

        public Task<DataResult<object>> AddSaleLineAsync(string saleId, string drugId, int quantity, int? unitRatio = null, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("sales.addline", new { saleId, drugId, quantity, unitRatio });
            return _client.ExecuteAsync<object>(req, cancellationToken);
        }

        public Task<DataResult<object>> AddPaymentAsync(string saleId, string method, long tenderedPiastres, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("sales.addpayment", new { saleId, method, tenderedPiastres });
            return _client.ExecuteAsync<object>(req, cancellationToken);
        }

        public Task<DataResult<object>> CompleteTransactionAsync(string saleId, CancellationToken cancellationToken = default)
        {
            var options = new RequestOptions { IdempotencyKey = Guid.NewGuid().ToString("N") };
            var req = new DataRequest("sales.complete", new { saleId }, options);
            return _client.ExecuteAsync<object>(req, cancellationToken);
        }

        public Task<DataResult<object>> RefundTransactionAsync(string transactionId, string reason, CancellationToken cancellationToken = default)
        {
            var options = new RequestOptions { IdempotencyKey = Guid.NewGuid().ToString("N") };
            var req = new DataRequest("sales.refund", new { transactionId, reason }, options);
            return _client.ExecuteAsync<object>(req, cancellationToken);
        }

        public Task<DataResult<DataPage<object>>> ListTransactionsAsync(int offset, int limit, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("sales.list", new { offset, limit });
            return _client.ExecuteAsync<DataPage<object>>(req, cancellationToken);
        }
    }

    public sealed class ShiftDataService : IShiftDataService
    {
        private readonly IDataLayerClient _client;

        public ShiftDataService(IDataLayerClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public Task<DataResult<object>> OpenShiftAsync(long floatPiastres, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("shifts.open", new { floatPiastres });
            return _client.ExecuteAsync<object>(req, cancellationToken);
        }

        public Task<DataResult<object>> CloseShiftAsync(long countedPiastres, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("shifts.close", new { countedPiastres });
            return _client.ExecuteAsync<object>(req, cancellationToken);
        }

        public Task<DataResult<long>> GetExpectedCashAsync(string shiftId, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("shifts.expectedcash", new { shiftId });
            return _client.ExecuteAsync<long>(req, cancellationToken);
        }
    }

    public sealed class AuthDataService : IAuthDataService
    {
        private readonly IAuthenticationService _authService;

        public AuthDataService(IAuthenticationService authService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        public Task<DataResult<LoginResponse>> LoginAsync(string username, string password, long? shiftFloatPiastres = null, CancellationToken cancellationToken = default)
        {
            var req = new LoginRequest
            {
                Username = username,
                Password = password,
                ShiftFloatPiastres = shiftFloatPiastres
            };
            return _authService.LoginAsync(req, cancellationToken);
        }

        public Task<DataResult> LogoutAsync(CancellationToken cancellationToken = default)
        {
            return _authService.LogoutAsync(cancellationToken);
        }

        public Task<DataResult<SessionInfo>> GetSessionAsync(CancellationToken cancellationToken = default)
        {
            return _authService.RestoreSessionAsync(cancellationToken);
        }
    }

    public sealed class SettingsDataService : ISettingsDataService
    {
        private readonly IDataLayerClient _client;

        public SettingsDataService(IDataLayerClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public Task<DataResult<object>> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("settings.get");
            return _client.ExecuteAsync<object>(req, cancellationToken);
        }
    }

    public sealed class CategoryDataService : ICategoryDataService
    {
        private readonly IDataLayerClient _client;

        public CategoryDataService(IDataLayerClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public Task<DataResult<IReadOnlyList<DrugCategory>>> ListCategoriesAsync(CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("categories.list");
            return _client.ExecuteAsync<IReadOnlyList<DrugCategory>>(req, cancellationToken);
        }
    }

    public sealed class WarehouseDataService : IWarehouseDataService
    {
        private readonly IDataLayerClient _client;

        public WarehouseDataService(IDataLayerClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public Task<DataResult<IReadOnlyList<object>>> ListWarehousesAsync(CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("warehouses.list");
            return _client.ExecuteAsync<IReadOnlyList<object>>(req, cancellationToken);
        }
    }

    public sealed class LicensingDataService : ILicensingDataService
    {
        private readonly IDataLayerClient _client;

        public LicensingDataService(IDataLayerClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public Task<DataResult<object>> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("licensing.status");
            return _client.ExecuteAsync<object>(req, cancellationToken);
        }

        public Task<DataResult<object>> ActivateAsync(string licenseKey, CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("licensing.activate", new { licenseKey });
            return _client.ExecuteAsync<object>(req, cancellationToken);
        }
    }

    public sealed class SyncDataService : ISyncDataService
    {
        private readonly IDataLayerClient _client;

        public SyncDataService(IDataLayerClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public Task<DataResult<object>> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("sync.status");
            return _client.ExecuteAsync<object>(req, cancellationToken);
        }

        public Task<DataResult<object>> TriggerSyncAsync(CancellationToken cancellationToken = default)
        {
            var req = new DataRequest("sync.trigger");
            return _client.ExecuteAsync<object>(req, cancellationToken);
        }
    }
}
