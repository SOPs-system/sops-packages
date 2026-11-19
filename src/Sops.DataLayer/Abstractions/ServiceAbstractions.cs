using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sops.DataLayer.Models.Authentication;
using Sops.DataLayer.Models.Responses;
using Sops.Drugs.Models;

namespace Sops.DataLayer.Abstractions
{
    public interface IDrugDataService
    {
        Task<DataResult<IReadOnlyList<Drug>>> ListDrugsAsync(string? query = null, CancellationToken cancellationToken = default);
        Task<DataResult<DataPage<Drug>>> ListDrugsPageAsync(int offset, int limit, string? query = null, CancellationToken cancellationToken = default);
        Task<DataResult<Drug?>> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);
        Task<DataResult<Drug?>> FindBySkuAsync(string sku, CancellationToken cancellationToken = default);
    }

    public interface IPatientDataService
    {
        Task<DataResult<IReadOnlyList<object>>> ListPatientsAsync(string? query = null, CancellationToken cancellationToken = default);
        Task<DataResult<object?>> GetPatientAsync(string patientId, CancellationToken cancellationToken = default);
        Task<DataResult<object>> SavePatientAsync(object patient, CancellationToken cancellationToken = default);
    }

    public interface IInventoryDataService
    {
        Task<DataResult<IReadOnlyList<object>>> GetStockSummaryAsync(CancellationToken cancellationToken = default);
        Task<DataResult<IReadOnlyList<object>>> GetLotsForDrugAsync(string drugId, CancellationToken cancellationToken = default);
        Task<DataResult<object>> AdjustLotAsync(string lotId, int newQuantity, string reason, CancellationToken cancellationToken = default);
    }

    public interface ITransactionDataService
    {
        Task<DataResult<object>> CreateSaleDraftAsync(string? patientId = null, string? reason = null, CancellationToken cancellationToken = default);
        Task<DataResult<object>> AddSaleLineAsync(string saleId, string drugId, int quantity, int? unitRatio = null, CancellationToken cancellationToken = default);
        Task<DataResult<object>> AddPaymentAsync(string saleId, string method, long tenderedPiastres, CancellationToken cancellationToken = default);
        Task<DataResult<object>> CompleteTransactionAsync(string saleId, CancellationToken cancellationToken = default);
        Task<DataResult<object>> RefundTransactionAsync(string transactionId, string reason, CancellationToken cancellationToken = default);
        Task<DataResult<DataPage<object>>> ListTransactionsAsync(int offset, int limit, CancellationToken cancellationToken = default);
    }

    public interface IShiftDataService
    {
        Task<DataResult<object>> OpenShiftAsync(long floatPiastres, CancellationToken cancellationToken = default);
        Task<DataResult<object>> CloseShiftAsync(long countedPiastres, CancellationToken cancellationToken = default);
        Task<DataResult<long>> GetExpectedCashAsync(string shiftId, CancellationToken cancellationToken = default);
    }

    public interface IAuthDataService
    {
        Task<DataResult<LoginResponse>> LoginAsync(string username, string password, long? shiftFloatPiastres = null, CancellationToken cancellationToken = default);
        Task<DataResult> LogoutAsync(CancellationToken cancellationToken = default);
        Task<DataResult<SessionInfo>> GetSessionAsync(CancellationToken cancellationToken = default);
    }

    public interface ISettingsDataService
    {
        Task<DataResult<object>> GetSettingsAsync(CancellationToken cancellationToken = default);
    }

    public interface ICategoryDataService
    {
        Task<DataResult<IReadOnlyList<DrugCategory>>> ListCategoriesAsync(CancellationToken cancellationToken = default);
    }

    public interface IWarehouseDataService
    {
        Task<DataResult<IReadOnlyList<object>>> ListWarehousesAsync(CancellationToken cancellationToken = default);
    }

    public interface ILicensingDataService
    {
        Task<DataResult<object>> GetStatusAsync(CancellationToken cancellationToken = default);
        Task<DataResult<object>> ActivateAsync(string licenseKey, CancellationToken cancellationToken = default);
    }

    public interface ISyncDataService
    {
        Task<DataResult<object>> GetStatusAsync(CancellationToken cancellationToken = default);
        Task<DataResult<object>> TriggerSyncAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Unified main DataLayer facade for WPF and desktop client applications.
    /// </summary>
    public interface ISopsDataLayer
    {
        IDrugDataService Drugs { get; }
        IPatientDataService Patients { get; }
        IInventoryDataService Inventory { get; }
        ITransactionDataService Transactions { get; }
        IShiftDataService Shifts { get; }
        IAuthDataService Auth { get; }
        ISettingsDataService Settings { get; }
        ICategoryDataService Categories { get; }
        IWarehouseDataService Warehouses { get; }
        ILicensingDataService Licensing { get; }
        ISyncDataService Sync { get; }

        IConnectivityMonitor Connectivity { get; }
        IDataLayerEventBus Events { get; }
        ILivePollingClient Live { get; }
        IOfflineReadCoordinator Cache { get; }
        IFileTransferClient Files { get; }
        IAuthenticationService Authentication { get; }
        IAuthorizationStateProvider Authorization { get; }
        IDataLayerClient Client { get; }
    }
}
