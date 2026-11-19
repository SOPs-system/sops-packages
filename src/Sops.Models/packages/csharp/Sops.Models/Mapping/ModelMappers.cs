namespace Sops.Models.Mapping;

using System;
using System.Collections.Generic;
using Sops.Models.Catalog;
using Sops.Models.Chain;
using Sops.Models.Common;
using Sops.Models.Inventory;
using Sops.Models.Patients;
using Sops.Models.Transactions;

/// <summary>
/// Sample entity abstractions simulating external persistence entities for mapping demonstrations.
/// </summary>
public static class SamplePersistenceEntities
{
    /// <summary>Sample Global PostgreSQL entity for Drug.</summary>
    public sealed class GlobalDrugEntity
    {
        /// <summary>Primary key ID.</summary>
        public Guid Id { get; set; }
        /// <summary>SKU code.</summary>
        public string Sku { get; set; } = string.Empty;
        /// <summary>Primary barcode.</summary>
        public string Barcode { get; set; } = string.Empty;
        /// <summary>English trade name.</summary>
        public string NameEn { get; set; } = string.Empty;
        /// <summary>Arabic trade name.</summary>
        public string NameAr { get; set; } = string.Empty;
        /// <summary>Generic scientific name.</summary>
        public string GenericName { get; set; } = string.Empty;
        /// <summary>Dosage form string.</summary>
        public string DosageForm { get; set; } = "Tablet";
        /// <summary>Official price in minor units.</summary>
        public long OfficialPriceMinor { get; set; }
        /// <summary>Currency code.</summary>
        public string Currency { get; set; } = "EGP";
        /// <summary>Price regulation flag.</summary>
        public bool IsPriceRegulated { get; set; } = true;
        /// <summary>Prescription requirement flag.</summary>
        public bool RequiresPrescription { get; set; }
        /// <summary>Storage type string.</summary>
        public string StorageType { get; set; } = "RoomTemperature";
        /// <summary>Status string.</summary>
        public string Status { get; set; } = "Active";
        /// <summary>Catalog revision sequence.</summary>
        public long CatalogRevision { get; set; } = 1;
    }

    /// <summary>Sample Local SQLCipher entity for LocalDrug.</summary>
    public sealed class LocalDrugEntity
    {
        /// <summary>Primary key ID.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        /// <summary>Foreign key to global drug.</summary>
        public string GlobalDrugId { get; set; } = Guid.NewGuid().ToString();
        /// <summary>Foreign key to local pharmacy.</summary>
        public string PharmacyId { get; set; } = Guid.NewGuid().ToString();
        /// <summary>Local cost price in minor units.</summary>
        public int LocalCostMinor { get; set; }
        /// <summary>Optional custom selling price in minor units.</summary>
        public int? CustomSellPriceMinor { get; set; }
        /// <summary>Currency code.</summary>
        public string Currency { get; set; } = "EGP";
        /// <summary>Min stock warning level.</summary>
        public int MinStock { get; set; } = 5;
        /// <summary>Max stock capacity.</summary>
        public int MaxStock { get; set; } = 100;
        /// <summary>Active status flag (1 or 0).</summary>
        public int IsActiveLocally { get; set; } = 1;
    }

    /// <summary>Sample Local SQLCipher entity for POS Transaction.</summary>
    public sealed class LocalTransactionEntity
    {
        /// <summary>Primary key ID.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        /// <summary>Invoice number.</summary>
        public string InvoiceNumber { get; set; } = string.Empty;
        /// <summary>Pharmacy ID.</summary>
        public string PharmacyId { get; set; } = Guid.NewGuid().ToString();
        /// <summary>Terminal ID.</summary>
        public string TerminalId { get; set; } = Guid.NewGuid().ToString();
        /// <summary>Cashier user ID.</summary>
        public string CashierUserId { get; set; } = Guid.NewGuid().ToString();
        /// <summary>Shift ID.</summary>
        public string ShiftId { get; set; } = Guid.NewGuid().ToString();
        /// <summary>Optional patient ID.</summary>
        public string? PatientId { get; set; }
        /// <summary>Transaction type string.</summary>
        public string TransactionType { get; set; } = "Sale";
        /// <summary>Transaction status string.</summary>
        public string TransactionStatus { get; set; } = "Completed";
        /// <summary>Subtotal in minor units.</summary>
        public int SubtotalMinor { get; set; }
        /// <summary>Discount in minor units.</summary>
        public int DiscountMinor { get; set; }
        /// <summary>Tax in minor units.</summary>
        public int TaxMinor { get; set; }
        /// <summary>Total in minor units.</summary>
        public int TotalMinor { get; set; }
        /// <summary>Currency code.</summary>
        public string Currency { get; set; } = "EGP";
        /// <summary>Creation ISO timestamp.</summary>
        public string CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    }

    /// <summary>Sample Tenant PostgreSQL entity for Branch Stock Snapshot.</summary>
    public sealed class TenantBranchStockEntity
    {
        /// <summary>Branch ID.</summary>
        public Guid BranchId { get; set; }
        /// <summary>Drug ID.</summary>
        public Guid DrugId { get; set; }
        /// <summary>Drug SKU.</summary>
        public string DrugSku { get; set; } = string.Empty;
        /// <summary>Count in base units.</summary>
        public long QuantityInBaseUnits { get; set; }
        /// <summary>Base unit name.</summary>
        public string BaseUnitName { get; set; } = "Tablet";
        /// <summary>Revision sequence number.</summary>
        public long SnapshotRevision { get; set; } = 1;
        /// <summary>Snapshot timestamp.</summary>
        public DateTimeOffset SnapshotTimestampUtc { get; set; }
        /// <summary>Cloud synchronization timestamp.</summary>
        public DateTimeOffset LastSynchronizedUtc { get; set; }
    }

    /// <summary>Sample Global PostgreSQL entity for Patient.</summary>
    public sealed class GlobalPatientEntity
    {
        /// <summary>Primary key ID.</summary>
        public Guid Id { get; set; }
        /// <summary>Blind hash of national identity.</summary>
        public string NationalIdHash { get; set; } = string.Empty;
        /// <summary>Full legal name.</summary>
        public string FullName { get; set; } = string.Empty;
        /// <summary>Phone number.</summary>
        public string PhoneNumber { get; set; } = string.Empty;
        /// <summary>Date of birth.</summary>
        public string? DateOfBirth { get; set; }
        /// <summary>Gender string.</summary>
        public string Gender { get; set; } = "Unspecified";
        /// <summary>Chronic condition flag.</summary>
        public bool HasChronicConditions { get; set; }
        /// <summary>Allergy flag.</summary>
        public bool HasAllergies { get; set; }
    }
}

/// <summary>
/// Mappers demonstrating conversion between persistence entities and Sops.Models contracts.
/// </summary>
public static class ModelMappers
{
    /// <summary>Maps a GlobalDrugEntity to a canonical Drug contract.</summary>
    public static Drug ToContract(this SamplePersistenceEntities.GlobalDrugEntity entity, IReadOnlyList<DrugPackage>? packages = null)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        var dosageForm = Enum.TryParse<DrugDosageForm>(entity.DosageForm, true, out var df) ? df : DrugDosageForm.Other;
        var storageType = Enum.TryParse<StorageType>(entity.StorageType, true, out var st) ? st : StorageType.RoomTemperature;
        var status = Enum.TryParse<DrugStatus>(entity.Status, true, out var s) ? s : DrugStatus.Active;

        return new Drug
        {
            Id = entity.Id,
            Sku = entity.Sku,
            Barcode = entity.Barcode,
            NameEn = entity.NameEn,
            NameAr = entity.NameAr,
            GenericName = entity.GenericName,
            DosageForm = dosageForm,
            OfficialPrice = Money.FromMinorUnits(entity.OfficialPriceMinor, entity.Currency),
            IsPriceRegulated = entity.IsPriceRegulated,
            RequiresPrescription = entity.RequiresPrescription,
            StorageType = storageType,
            Status = status,
            CatalogRevision = entity.CatalogRevision,
            Packages = packages ?? new List<DrugPackage> { new("Unit", 1, true) }
        };
    }

    /// <summary>Maps a LocalDrugEntity to a LocalDrug contract.</summary>
    public static LocalDrug ToContract(this SamplePersistenceEntities.LocalDrugEntity entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        return new LocalDrug
        {
            Id = Guid.Parse(entity.Id),
            GlobalDrugId = Guid.Parse(entity.GlobalDrugId),
            PharmacyId = Guid.Parse(entity.PharmacyId),
            LocalCostPrice = Money.FromMinorUnits(entity.LocalCostMinor, entity.Currency),
            CustomSellingPrice = entity.CustomSellPriceMinor.HasValue ? Money.FromMinorUnits(entity.CustomSellPriceMinor.Value, entity.Currency) : null,
            MinimumStockThreshold = entity.MinStock,
            MaximumStockThreshold = entity.MaxStock,
            IsActiveLocally = entity.IsActiveLocally == 1
        };
    }

    /// <summary>Maps a LocalTransactionEntity to a Transaction contract.</summary>
    public static Transaction ToContract(this SamplePersistenceEntities.LocalTransactionEntity entity, IReadOnlyList<TransactionItem>? items = null, IReadOnlyList<Payment>? payments = null)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        var type = Enum.TryParse<TransactionType>(entity.TransactionType, true, out var t) ? t : TransactionType.Sale;
        var status = Enum.TryParse<TransactionStatus>(entity.TransactionStatus, true, out var s) ? s : TransactionStatus.Completed;

        return new Transaction
        {
            Id = Guid.Parse(entity.Id),
            InvoiceNumber = entity.InvoiceNumber,
            PharmacyId = Guid.Parse(entity.PharmacyId),
            TerminalId = Guid.Parse(entity.TerminalId),
            CashierUserId = Guid.Parse(entity.CashierUserId),
            ShiftId = Guid.Parse(entity.ShiftId),
            PatientId = !string.IsNullOrEmpty(entity.PatientId) ? Guid.Parse(entity.PatientId!) : null,
            Type = type,
            Status = status,
            Subtotal = Money.FromMinorUnits(entity.SubtotalMinor, entity.Currency),
            DiscountAmount = Money.FromMinorUnits(entity.DiscountMinor, entity.Currency),
            TaxAmount = Money.FromMinorUnits(entity.TaxMinor, entity.Currency),
            TotalAmount = Money.FromMinorUnits(entity.TotalMinor, entity.Currency),
            Items = items ?? Array.Empty<TransactionItem>(),
            Payments = payments ?? Array.Empty<Payment>(),
            CreatedAtUtc = DateTimeOffset.Parse(entity.CreatedAt)
        };
    }

    /// <summary>Maps a TenantBranchStockEntity to a BranchStockSnapshot contract.</summary>
    public static BranchStockSnapshot ToContract(this SamplePersistenceEntities.TenantBranchStockEntity entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        return new BranchStockSnapshot
        {
            BranchId = entity.BranchId,
            DrugId = entity.DrugId,
            DrugSku = entity.DrugSku,
            QuantityInBaseUnits = entity.QuantityInBaseUnits,
            BaseUnitName = entity.BaseUnitName,
            SnapshotRevision = entity.SnapshotRevision,
            SnapshotTimestampUtc = entity.SnapshotTimestampUtc,
            LastSynchronizedUtc = entity.LastSynchronizedUtc
        };
    }

    /// <summary>Maps a GlobalPatientEntity to a non-sensitive PatientSummary contract.</summary>
    public static PatientSummary ToSummary(this SamplePersistenceEntities.GlobalPatientEntity entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        return new PatientSummary
        {
            PatientId = entity.Id,
            FullName = entity.FullName,
            PhoneNumber = entity.PhoneNumber,
            HasChronicConditions = entity.HasChronicConditions,
            HasAllergies = entity.HasAllergies
        };
    }
}
