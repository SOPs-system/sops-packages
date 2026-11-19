namespace Sops.Models.Billing;

using System;
using System.Text.Json.Serialization;
using Sops.Models.Common;

/// <summary>
/// Status of a SaaS billing payment.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BillingPaymentStatus
{
    /// <summary>Invoice awaiting payment.</summary>
    Pending,
    /// <summary>Pre-authorized on card or gateway.</summary>
    Authorized,
    /// <summary>Funds successfully captured.</summary>
    Captured,
    /// <summary>Payment attempt failed.</summary>
    Failed,
    /// <summary>Payment refunded back to tenant.</summary>
    Refunded
}

/// <summary>
/// SaaS software subscription invoice payment.
/// Strictly distinct from point-of-sale customer tender Payment.
/// </summary>
public sealed record BillingPayment
{
    /// <summary>
    /// Gets the unique billing payment ID.
    /// </summary>
    [JsonPropertyName("billing_payment_id")]
    public Guid BillingPaymentId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the customer tenant ID.
    /// </summary>
    [JsonPropertyName("tenant_id")]
    public Guid TenantId { get; init; }

    /// <summary>
    /// Gets the invoice reference number.
    /// </summary>
    [JsonPropertyName("invoice_number")]
    public string InvoiceNumber { get; init; } = string.Empty;

    /// <summary>
    /// Gets the monetary amount charged.
    /// </summary>
    [JsonPropertyName("amount")]
    public Money Amount { get; init; }

    /// <summary>
    /// Gets the payment status.
    /// </summary>
    [JsonPropertyName("status")]
    public BillingPaymentStatus Status { get; init; } = BillingPaymentStatus.Pending;

    /// <summary>
    /// Gets the gateway provider name (e.g. "Paymob", "Stripe", "Fawry", "BankTransfer").
    /// </summary>
    [JsonPropertyName("gateway_provider")]
    public string GatewayProvider { get; init; } = string.Empty;

    /// <summary>
    /// Gets the external transaction reference returned by the gateway.
    /// </summary>
    [JsonPropertyName("gateway_transaction_reference")]
    public string? GatewayTransactionReference { get; init; }

    /// <summary>
    /// Gets the invoice creation timestamp.
    /// </summary>
    [JsonPropertyName("created_at_utc")]
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the timestamp when payment was confirmed.
    /// </summary>
    [JsonPropertyName("paid_at_utc")]
    public DateTimeOffset? PaidAtUtc { get; init; }
}

/// <summary>
/// SaaS software tier and pricing package for pharmacy tenants.
/// </summary>
public sealed record SubscriptionPlan
{
    /// <summary>
    /// Gets the plan ID.
    /// </summary>
    [JsonPropertyName("plan_id")]
    public Guid PlanId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the plan name (e.g. "Professional", "Enterprise").
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the monthly subscription cost.
    /// </summary>
    [JsonPropertyName("price_per_month")]
    public Money PricePerMonth { get; init; }

    /// <summary>
    /// Gets the maximum branches allowed under this plan.
    /// </summary>
    [JsonPropertyName("max_branches")]
    public int MaxBranches { get; init; } = 1;

    /// <summary>
    /// Gets the maximum workstation seats allowed per branch.
    /// </summary>
    [JsonPropertyName("max_seats_per_branch")]
    public int MaxSeatsPerBranch { get; init; } = 3;

    /// <summary>
    /// Gets whether advanced chain analytics are included.
    /// </summary>
    [JsonPropertyName("has_analytics")]
    public bool HasAnalytics { get; init; }

    /// <summary>
    /// Gets whether WhatsApp customer notifications are included.
    /// </summary>
    [JsonPropertyName("has_whatsapp_notifications")]
    public bool HasWhatsAppNotifications { get; init; }
}

/// <summary>
/// Summary of an issued subscription invoice.
/// </summary>
public sealed record InvoiceSummary
{
    /// <summary>
    /// Gets the invoice ID.
    /// </summary>
    [JsonPropertyName("invoice_id")]
    public Guid InvoiceId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the human-readable invoice code.
    /// </summary>
    [JsonPropertyName("invoice_number")]
    public string InvoiceNumber { get; init; } = string.Empty;

    /// <summary>
    /// Gets the billed tenant ID.
    /// </summary>
    [JsonPropertyName("tenant_id")]
    public Guid TenantId { get; init; }

    /// <summary>
    /// Gets the total amount due.
    /// </summary>
    [JsonPropertyName("total_due")]
    public Money TotalDue { get; init; }

    /// <summary>
    /// Gets the issue date (YYYY-MM-DD).
    /// </summary>
    [JsonPropertyName("issue_date")]
    public string IssueDate { get; init; } = string.Empty;

    /// <summary>
    /// Gets the payment due date (YYYY-MM-DD).
    /// </summary>
    [JsonPropertyName("due_date")]
    public string DueDate { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether this invoice has been fully paid.
    /// </summary>
    [JsonPropertyName("is_settled")]
    public bool IsSettled { get; init; }
}
