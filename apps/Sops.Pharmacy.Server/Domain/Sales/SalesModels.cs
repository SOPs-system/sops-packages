using System;
using System.Collections.Generic;

namespace Sops.Pharmacy.Server.Domain.Sales
{
    public class SaleDraft
    {
        public string SaleId { get; set; } = Guid.NewGuid().ToString("N");
        public string? PatientId { get; set; }
        public string? Reason { get; set; }
        public List<SaleLineDraft> Lines { get; set; } = new List<SaleLineDraft>();
        public List<PaymentDraft> Payments { get; set; } = new List<PaymentDraft>();
        public long SubtotalMinor => Lines != null ? System.Linq.Enumerable.Sum(Lines, l => l.LineTotalMinor) : 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class SaleLineDraft
    {
        public string DrugId { get; set; } = string.Empty;
        public string DrugName { get; set; } = string.Empty;
        public decimal Quantity { get; set; } = 1;
        public string PackageTier { get; set; } = "Unit";
        public int UnitRatio { get; set; } = 1;
        public long UnitPriceMinor { get; set; }
        public long DiscountMinor { get; set; }
        public long LineTotalMinor => (long)(Quantity * UnitPriceMinor) - DiscountMinor;
    }

    public class PaymentDraft
    {
        public string Method { get; set; } = "Cash"; // Cash, Card, Insurance
        public long TenderedPiastres { get; set; }
        public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    }

    public class TransactionDto
    {
        public string Id { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public string PharmacyId { get; set; } = string.Empty;
        public string TerminalId { get; set; } = string.Empty;
        public string CashierUserId { get; set; } = string.Empty;
        public string ShiftId { get; set; } = string.Empty;
        public string? PatientId { get; set; }
        public string TransactionType { get; set; } = "Sale";
        public string TransactionStatus { get; set; } = "Completed";
        public long SubtotalMinor { get; set; }
        public long DiscountMinor { get; set; }
        public long TaxMinor { get; set; }
        public long TotalMinor { get; set; }
        public string Currency { get; set; } = "EGP";
        public string? IdempotencyKey { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
        public string? CompletedAt { get; set; }
        public bool IsSynced { get; set; }
        public List<TransactionItemDto> Items { get; set; } = new List<TransactionItemDto>();
    }

    public class TransactionItemDto
    {
        public string ItemId { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string DrugId { get; set; } = string.Empty;
        public string DrugName { get; set; } = string.Empty;
        public string PackageTier { get; set; } = "Unit";
        public decimal Quantity { get; set; }
        public string BaseUnit { get; set; } = "Box";
        public long UnitPriceMinor { get; set; }
        public long DiscountMinor { get; set; }
        public decimal TaxRate { get; set; }
        public long TotalLineMinor { get; set; }
        public string Currency { get; set; } = "EGP";
        public string? LotId { get; set; }
    }
}
