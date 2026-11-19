namespace Sops.Models.Transactions;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Sops.Models.Common;

/// <summary>
/// Point-of-sale transaction classification.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransactionType
{
    /// <summary>Customer retail sale.</summary>
    Sale,
    /// <summary>Customer item return.</summary>
    Return,
    /// <summary>Item exchange.</summary>
    Exchange,
    /// <summary>Voided transaction.</summary>
    Void
}

/// <summary>
/// Lifecycle state of a POS transaction.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransactionStatus
{
    /// <summary>Draft / in-progress cart.</summary>
    Draft,
    /// <summary>Completed and paid.</summary>
    Completed,
    /// <summary>Voided before or after completion.</summary>
    Voided,
    /// <summary>Suspended / parked cart.</summary>
    Suspended
}

/// <summary>
/// Retail customer tender method.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentMethod
{
    /// <summary>Physical paper currency / coins.</summary>
    Cash,
    /// <summary>Credit card terminal payment.</summary>
    CreditCard,
    /// <summary>Debit card terminal payment.</summary>
    DebitCard,
    /// <summary>Gift card or promotional voucher.</summary>
    Voucher,
    /// <summary>Insurance copay or third-party claim.</summary>
    InsuranceClaim,
    /// <summary>Split payment across multiple tenders.</summary>
    Split,
    /// <summary>Redeemed loyalty points.</summary>
    LoyaltyPoints
}

/// <summary>
/// Tender payment status.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentStatus
{
    /// <summary>Payment pending authorization.</summary>
    Pending,
    /// <summary>Payment successfully captured.</summary>
    Completed,
    /// <summary>Payment failed or rejected.</summary>
    Failed,
    /// <summary>Payment refunded back to customer.</summary>
    Refunded
}

/// <summary>
/// Retail point-of-sale customer tender payment.
/// Strictly separate from SaaS software BillingPayment.
/// </summary>
public sealed record Payment
{
    /// <summary>
    /// Gets the unique payment ID.
    /// </summary>
    [JsonPropertyName("payment_id")]
    public Guid PaymentId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the tender method.
    /// </summary>
    [JsonPropertyName("method")]
    public PaymentMethod Method { get; init; } = PaymentMethod.Cash;

    /// <summary>
    /// Gets the monetary amount settled.
    /// </summary>
    [JsonPropertyName("amount")]
    public Money Amount { get; init; }

    /// <summary>
    /// Gets the physical cash tendered by the customer (for cash change calculation).
    /// </summary>
    [JsonPropertyName("tendered_amount")]
    public Money? TenderedAmount { get; init; }

    /// <summary>
    /// Gets the change returned back to the customer.
    /// </summary>
    [JsonPropertyName("change_returned")]
    public Money? ChangeReturned { get; init; }

    /// <summary>
    /// Gets the payment status.
    /// </summary>
    [JsonPropertyName("status")]
    public PaymentStatus Status { get; init; } = PaymentStatus.Completed;

    /// <summary>
    /// Gets the masked card number or gateway authorization reference.
    /// </summary>
    [JsonPropertyName("card_reference")]
    public string? CardReference { get; init; }
}

/// <summary>
/// Individual line item within a sale or return transaction.
/// </summary>
public sealed record TransactionItem
{
    /// <summary>
    /// Gets the unique item line ID.
    /// </summary>
    [JsonPropertyName("item_id")]
    public Guid ItemId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the master catalogue drug ID.
    /// </summary>
    [JsonPropertyName("drug_id")]
    public Guid DrugId { get; init; }

    /// <summary>
    /// Gets the frozen snapshot of the drug name at the time of sale.
    /// </summary>
    [JsonPropertyName("drug_name")]
    public string DrugName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the package tier sold (e.g. "Box", "Strip", "Unit").
    /// </summary>
    [JsonPropertyName("package_tier")]
    public string PackageTier { get; init; } = "Unit";

    /// <summary>
    /// Gets the quantity sold in base units.
    /// </summary>
    [JsonPropertyName("quantity")]
    public Quantity Quantity { get; init; }

    /// <summary>
    /// Gets the authorized transaction-time selling price per unit.
    /// Preserves historical financial accuracy regardless of future price changes.
    /// </summary>
    [JsonPropertyName("unit_price")]
    public Money UnitPrice { get; init; }

    /// <summary>
    /// Gets any line-item discount applied.
    /// </summary>
    [JsonPropertyName("discount_amount")]
    public Money DiscountAmount { get; init; }

    /// <summary>
    /// Gets the sales tax or VAT rate applied (e.g. 0.14 for 14%).
    /// </summary>
    [JsonPropertyName("tax_rate")]
    public decimal TaxRate { get; init; }

    /// <summary>
    /// Gets the final line total after discount and tax.
    /// </summary>
    [JsonPropertyName("total_line_amount")]
    public Money TotalLineAmount { get; init; }

    /// <summary>
    /// Gets the allocated stock lot ID for FEFO tracking.
    /// </summary>
    [JsonPropertyName("lot_id")]
    public Guid? LotId { get; init; }
}

/// <summary>
/// Point-of-sale retail transaction.
/// Operates and commits locally in SQLCipher without requiring an active internet connection.
/// </summary>
public sealed record Transaction
{
    /// <summary>
    /// Gets the unique transaction ID.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the sequential human-readable invoice or receipt number.
    /// </summary>
    [JsonPropertyName("invoice_number")]
    public string InvoiceNumber { get; init; } = string.Empty;

    /// <summary>
    /// Gets the dispensing pharmacy branch ID.
    /// </summary>
    [JsonPropertyName("pharmacy_id")]
    public Guid PharmacyId { get; init; }

    /// <summary>
    /// Gets the workstation POS terminal ID.
    /// </summary>
    [JsonPropertyName("terminal_id")]
    public Guid TerminalId { get; init; }

    /// <summary>
    /// Gets the cashier staff member ID.
    /// </summary>
    [JsonPropertyName("cashier_user_id")]
    public Guid CashierUserId { get; init; }

    /// <summary>
    /// Gets the associated shift ID.
    /// </summary>
    [JsonPropertyName("shift_id")]
    public Guid ShiftId { get; init; }

    /// <summary>
    /// Gets the optional patient reference.
    /// Nullable for anonymous walk-in sales.
    /// </summary>
    [JsonPropertyName("patient_id")]
    public Guid? PatientId { get; init; }

    /// <summary>
    /// Gets the transaction classification.
    /// </summary>
    [JsonPropertyName("type")]
    public TransactionType Type { get; init; } = TransactionType.Sale;

    /// <summary>
    /// Gets the transaction status.
    /// </summary>
    [JsonPropertyName("status")]
    public TransactionStatus Status { get; init; } = TransactionStatus.Completed;

    /// <summary>
    /// Gets the pre-discount, pre-tax subtotal.
    /// </summary>
    [JsonPropertyName("subtotal")]
    public Money Subtotal { get; init; }

    /// <summary>
    /// Gets total promotional or customer discounts.
    /// </summary>
    [JsonPropertyName("discount_amount")]
    public Money DiscountAmount { get; init; }

    /// <summary>
    /// Gets total tax / VAT amount.
    /// </summary>
    [JsonPropertyName("tax_amount")]
    public Money TaxAmount { get; init; }

    /// <summary>
    /// Gets the final customer charge.
    /// </summary>
    [JsonPropertyName("total_amount")]
    public Money TotalAmount { get; init; }

    /// <summary>
    /// Gets the line items sold.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<TransactionItem> Items { get; init; } = Array.Empty<TransactionItem>();

    /// <summary>
    /// Gets the customer payments tendered.
    /// </summary>
    [JsonPropertyName("payments")]
    public IReadOnlyList<Payment> Payments { get; init; } = Array.Empty<Payment>();

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    [JsonPropertyName("created_at_utc")]
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the completion timestamp.
    /// </summary>
    [JsonPropertyName("completed_at_utc")]
    public DateTimeOffset? CompletedAtUtc { get; init; }

    /// <summary>
    /// Validates transaction invariants.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(InvoiceNumber))
        {
            throw new InvalidOperationException("Invoice number cannot be null or empty.");
        }

        if (Items == null || Items.Count == 0)
        {
            throw new InvalidOperationException("A transaction must contain at least one line item.");
        }

        if (TotalAmount.AmountMinorUnits < 0 && Type == TransactionType.Sale)
        {
            throw new InvalidOperationException("Sale total amount cannot be negative.");
        }
    }
}

/// <summary>
/// Status of a cashier shift.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShiftStatus
{
    /// <summary>Shift actively running.</summary>
    Open,
    /// <summary>Shift closed awaiting final count reconciliation.</summary>
    Closed,
    /// <summary>Shift fully reconciled and settled.</summary>
    Reconciled
}

/// <summary>
/// Cashier shift session tracking physical cash reconciliation.
/// </summary>
public sealed record Shift
{
    /// <summary>
    /// Gets the shift ID.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the sequential shift counter for the terminal.
    /// </summary>
    [JsonPropertyName("shift_number")]
    public int ShiftNumber { get; init; } = 1;

    /// <summary>
    /// Gets the pharmacy branch ID.
    /// </summary>
    [JsonPropertyName("pharmacy_id")]
    public Guid PharmacyId { get; init; }

    /// <summary>
    /// Gets the terminal ID.
    /// </summary>
    [JsonPropertyName("terminal_id")]
    public Guid TerminalId { get; init; }

    /// <summary>
    /// Gets the cashier user ID.
    /// </summary>
    [JsonPropertyName("cashier_user_id")]
    public Guid CashierUserId { get; init; }

    /// <summary>
    /// Gets the opening cash float in the drawer.
    /// </summary>
    [JsonPropertyName("opening_cash")]
    public Money OpeningCash { get; init; }

    /// <summary>
    /// Gets the physical closing cash counted at shift end.
    /// </summary>
    [JsonPropertyName("closing_cash")]
    public Money? ClosingCash { get; init; }

    /// <summary>
    /// Gets the system expected cash (Opening + Cash Sales - Cash Refunds).
    /// </summary>
    [JsonPropertyName("expected_cash")]
    public Money? ExpectedCash { get; init; }

    /// <summary>
    /// Gets the shortage or surplus cash difference.
    /// </summary>
    [JsonPropertyName("cash_difference")]
    public Money? CashDifference { get; init; }

    /// <summary>
    /// Gets the shift status.
    /// </summary>
    [JsonPropertyName("status")]
    public ShiftStatus Status { get; init; } = ShiftStatus.Open;

    /// <summary>
    /// Gets the shift opening timestamp.
    /// </summary>
    [JsonPropertyName("opened_at_utc")]
    public DateTimeOffset OpenedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the shift closing timestamp.
    /// </summary>
    [JsonPropertyName("closed_at_utc")]
    public DateTimeOffset? ClosedAtUtc { get; init; }
}

/// <summary>
/// Daily or shift accounting financial summary.
/// </summary>
public sealed record Settlement
{
    /// <summary>
    /// Gets the settlement ID.
    /// </summary>
    [JsonPropertyName("settlement_id")]
    public Guid SettlementId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the pharmacy branch ID.
    /// </summary>
    [JsonPropertyName("pharmacy_id")]
    public Guid PharmacyId { get; init; }

    /// <summary>
    /// Gets the business date string (YYYY-MM-DD).
    /// </summary>
    [JsonPropertyName("business_date")]
    public string BusinessDate { get; init; } = string.Empty;

    /// <summary>
    /// Gets gross sales total.
    /// </summary>
    [JsonPropertyName("total_sales")]
    public Money TotalSales { get; init; }

    /// <summary>
    /// Gets total returns refunded.
    /// </summary>
    [JsonPropertyName("total_returns")]
    public Money TotalReturns { get; init; }

    /// <summary>
    /// Gets net sales total (Sales - Returns).
    /// </summary>
    [JsonPropertyName("net_sales")]
    public Money NetSales { get; init; }

    /// <summary>
    /// Gets total cash tendered.
    /// </summary>
    [JsonPropertyName("cash_total")]
    public Money CashTotal { get; init; }

    /// <summary>
    /// Gets total credit/debit card tendered.
    /// </summary>
    [JsonPropertyName("card_total")]
    public Money CardTotal { get; init; }

    /// <summary>
    /// Gets the manager who authorized the settlement.
    /// </summary>
    [JsonPropertyName("settled_by_user_id")]
    public Guid? SettledByUserId { get; init; }

    /// <summary>
    /// Gets the settlement timestamp.
    /// </summary>
    [JsonPropertyName("settled_at_utc")]
    public DateTimeOffset SettledAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
