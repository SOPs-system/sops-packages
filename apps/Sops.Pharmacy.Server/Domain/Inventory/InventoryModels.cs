using System;
using System.Collections.Generic;

namespace Sops.Pharmacy.Server.Domain.Inventory
{
    public class LocalDrugDto
    {
        public string Id { get; set; } = string.Empty;
        public string GlobalDrugId { get; set; } = string.Empty;
        public string TradeName { get; set; } = string.Empty;
        public string TradeNameAr { get; set; } = string.Empty;
        public string GenericName { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string DosageForm { get; set; } = string.Empty;
        public string Strength { get; set; } = string.Empty;
        public long PublicPriceMinor { get; set; }
        public long LocalCostMinor { get; set; }
        public long? CustomSellPriceMinor { get; set; }
        public int UnitsPerPack { get; set; } = 1;
        public decimal TotalStockQuantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal AvailableQuantity => TotalStockQuantity - ReservedQuantity;
        public string Currency { get; set; } = "EGP";
        public bool IsActiveLocally { get; set; } = true;
    }

    public class StockLotDto
    {
        public string Id { get; set; } = string.Empty;
        public string LocalDrugId { get; set; } = string.Empty;
        public string GlobalDrugId { get; set; } = string.Empty;
        public string BatchNumber { get; set; } = string.Empty;
        public string ExpiryDate { get; set; } = string.Empty;
        public decimal ReceivedQuantity { get; set; }
        public decimal RemainingQuantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal AvailableQuantity => RemainingQuantity - ReservedQuantity;
        public string BaseUnit { get; set; } = "Box";
        public long PurchasePriceMinor { get; set; }
        public string Currency { get; set; } = "EGP";
        public string? DistributorId { get; set; }
        public string ReceivedDate { get; set; } = string.Empty;
    }

    public class StockMovementDto
    {
        public string Id { get; set; } = string.Empty;
        public string LotId { get; set; } = string.Empty;
        public string DrugId { get; set; } = string.Empty;
        public string MovementType { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal PreviousQuantity { get; set; }
        public decimal NewQuantity { get; set; }
        public string? ReferenceId { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
