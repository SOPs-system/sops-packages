namespace Sops.Models.Inventory;

using System;
using System.Text.Json.Serialization;
using Sops.Models.Common;

/// <summary>
/// Locally authoritative physical inventory lot tracking batch number, expiration date, and base-unit count.
/// Supports FEFO (First-Expired, First-Out) dispensing.
/// </summary>
public sealed record StockLot
{
    /// <summary>
    /// Gets the unique lot identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the reference to the local drug profile.
    /// </summary>
    [JsonPropertyName("local_drug_id")]
    public Guid LocalDrugId { get; init; }

    /// <summary>
    /// Gets the reference to the master catalogue drug.
    /// </summary>
    [JsonPropertyName("global_drug_id")]
    public Guid GlobalDrugId { get; init; }

    /// <summary>
    /// Gets the manufacturer batch or lot number.
    /// </summary>
    [JsonPropertyName("batch_number")]
    public string BatchNumber { get; init; } = string.Empty;

    /// <summary>
    /// Gets the expiration date formatted as YYYY-MM-DD.
    /// </summary>
    [JsonPropertyName("expiry_date")]
    public string ExpiryDate { get; init; } = string.Empty;

    /// <summary>
    /// Gets the original total received quantity in base units.
    /// </summary>
    [JsonPropertyName("received_quantity")]
    public Quantity ReceivedQuantity { get; init; }

    /// <summary>
    /// Gets the remaining physical quantity in base units.
    /// </summary>
    [JsonPropertyName("remaining_quantity")]
    public Quantity RemainingQuantity { get; init; }

    /// <summary>
    /// Gets the quantity reserved for pending orders in base units.
    /// </summary>
    [JsonPropertyName("reserved_quantity")]
    public Quantity ReservedQuantity { get; init; }

    /// <summary>
    /// Gets the purchase invoice cost for this specific lot.
    /// </summary>
    [JsonPropertyName("purchase_price")]
    public Money PurchasePrice { get; init; }

    /// <summary>
    /// Gets the optional supplier distributor identifier.
    /// </summary>
    [JsonPropertyName("distributor_id")]
    public Guid? DistributorId { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the lot was received into the pharmacy.
    /// </summary>
    [JsonPropertyName("received_date_utc")]
    public DateTimeOffset ReceivedDateUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Inventory movement classification.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StockMovementType
{
    /// <summary>Stock received from supplier.</summary>
    Receipt,
    /// <summary>Stock deducted via POS customer sale.</summary>
    Sale,
    /// <summary>Customer returned item back into stock.</summary>
    CustomerReturn,
    /// <summary>Item returned back to distributor.</summary>
    SupplierReturn,
    /// <summary>Positive inventory count adjustment.</summary>
    AdjustmentPositive,
    /// <summary>Negative inventory count adjustment.</summary>
    AdjustmentNegative,
    /// <summary>Inter-branch transfer dispatched out.</summary>
    TransferOut,
    /// <summary>Inter-branch transfer received in.</summary>
    TransferIn,
    /// <summary>Damaged or expired stock written off.</summary>
    ExpiredWriteOff
}

/// <summary>
/// Immutable audit entry recording physical changes in inventory count.
/// </summary>
public sealed record StockMovement
{
    /// <summary>
    /// Gets the movement record identifier.
    /// </summary>
    [JsonPropertyName("movement_id")]
    public Guid MovementId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the associated lot ID.
    /// </summary>
    [JsonPropertyName("lot_id")]
    public Guid LotId { get; init; }

    /// <summary>
    /// Gets the associated drug ID.
    /// </summary>
    [JsonPropertyName("drug_id")]
    public Guid DrugId { get; init; }

    /// <summary>
    /// Gets the type of movement.
    /// </summary>
    [JsonPropertyName("movement_type")]
    public StockMovementType MovementType { get; init; }

    /// <summary>
    /// Gets the change in quantity (positive or negative).
    /// </summary>
    [JsonPropertyName("quantity_delta")]
    public Quantity QuantityDelta { get; init; }

    /// <summary>
    /// Gets the reference document (e.g. invoice number, receipt number, transfer number).
    /// </summary>
    [JsonPropertyName("reference_document_id")]
    public string? ReferenceDocumentId { get; init; }

    /// <summary>
    /// Gets the staff member who authorized the movement.
    /// </summary>
    [JsonPropertyName("user_id")]
    public Guid? UserId { get; init; }

    /// <summary>
    /// Gets the timestamp of the movement.
    /// </summary>
    [JsonPropertyName("timestamp_utc")]
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets an optional operational reason.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>
/// Calculated inventory availability projection for checkout and dispensing.
/// </summary>
public sealed record StockAvailability
{
    /// <summary>
    /// Gets the drug ID.
    /// </summary>
    [JsonPropertyName("drug_id")]
    public Guid DrugId { get; init; }

    /// <summary>
    /// Gets total physical stock in base units.
    /// </summary>
    [JsonPropertyName("total_physical_quantity")]
    public Quantity TotalPhysicalQuantity { get; init; }

    /// <summary>
    /// Gets total reserved stock in base units.
    /// </summary>
    [JsonPropertyName("total_reserved_quantity")]
    public Quantity TotalReservedQuantity { get; init; }

    /// <summary>
    /// Gets net available stock in base units.
    /// </summary>
    [JsonPropertyName("available_quantity")]
    public Quantity AvailableQuantity { get; init; }

    /// <summary>
    /// Gets the earliest expiration date among active lots (for FEFO warning).
    /// </summary>
    [JsonPropertyName("earliest_expiry_date")]
    public string? EarliestExpiryDate { get; init; }

    /// <summary>
    /// Gets the number of distinct lots currently in stock.
    /// </summary>
    [JsonPropertyName("lot_count")]
    public int LotCount { get; init; }
}
