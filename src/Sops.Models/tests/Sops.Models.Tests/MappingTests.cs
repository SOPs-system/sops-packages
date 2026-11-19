namespace Sops.Models.Tests;

using System;
using System.Collections.Generic;
using Sops.Models.Catalog;
using Sops.Models.Mapping;
using Xunit;

public class MappingTests
{
    [Fact]
    public void GlobalDrugEntity_MapsToCanonicalDrugContract()
    {
        // Arrange
        var entity = new SamplePersistenceEntities.GlobalDrugEntity
        {
            Id = Guid.NewGuid(),
            Sku = "SKU-999",
            Barcode = "6221111222333",
            NameEn = "Cataflam 50mg",
            NameAr = "كاتافلام 50 مجم",
            GenericName = "Diclofenac Potassium",
            DosageForm = "Tablet",
            OfficialPriceMinor = 3300,
            Currency = "EGP",
            IsPriceRegulated = true,
            RequiresPrescription = false,
            StorageType = "RoomTemperature",
            Status = "Active",
            CatalogRevision = 3
        };

        var packages = new List<DrugPackage>
        {
            new("Box", 20, true)
        };

        // Act
        var contract = entity.ToContract(packages);

        // Assert
        Assert.Equal(entity.Id, contract.Id);
        Assert.Equal(entity.Sku, contract.Sku);
        Assert.Equal(entity.Barcode, contract.Barcode);
        Assert.Equal(DrugDosageForm.Tablet, contract.DosageForm);
        Assert.Equal(33.00m, contract.OfficialPrice.ToDecimal());
        Assert.Single(contract.Packages);
        Assert.Equal(20, contract.Packages[0].ConversionFactorToBase);
    }

    [Fact]
    public void LocalDrugEntity_MapsToLocalDrugContract()
    {
        // Arrange
        var entity = new SamplePersistenceEntities.LocalDrugEntity
        {
            Id = Guid.NewGuid().ToString(),
            GlobalDrugId = Guid.NewGuid().ToString(),
            PharmacyId = Guid.NewGuid().ToString(),
            LocalCostMinor = 2500,
            CustomSellPriceMinor = 3200,
            Currency = "EGP",
            MinStock = 10,
            MaxStock = 50,
            IsActiveLocally = 1
        };

        // Act
        var contract = entity.ToContract();

        // Assert
        Assert.Equal(Guid.Parse(entity.Id), contract.Id);
        Assert.Equal(25.00m, contract.LocalCostPrice.ToDecimal());
        Assert.NotNull(contract.CustomSellingPrice);
        Assert.Equal(32.00m, contract.CustomSellingPrice!.Value.ToDecimal());
        Assert.True(contract.IsActiveLocally);
    }

    [Fact]
    public void TenantBranchStockEntity_MapsToBranchStockSnapshotContract()
    {
        // Arrange
        var entity = new SamplePersistenceEntities.TenantBranchStockEntity
        {
            BranchId = Guid.NewGuid(),
            DrugId = Guid.NewGuid(),
            DrugSku = "SKU-001",
            QuantityInBaseUnits = 144,
            BaseUnitName = "Tablet",
            SnapshotRevision = 12,
            SnapshotTimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            LastSynchronizedUtc = DateTimeOffset.UtcNow
        };

        // Act
        var contract = entity.ToContract();

        // Assert
        Assert.Equal(entity.BranchId, contract.BranchId);
        Assert.Equal(entity.DrugId, contract.DrugId);
        Assert.Equal(144, contract.QuantityInBaseUnits);
        Assert.Equal("Tablet", contract.BaseUnitName);
        Assert.Equal(12, contract.SnapshotRevision);
    }

    [Fact]
    public void GlobalPatientEntity_MapsToNonSensitivePatientSummary()
    {
        // Arrange
        var entity = new SamplePersistenceEntities.GlobalPatientEntity
        {
            Id = Guid.NewGuid(),
            NationalIdHash = "a1b2c3d4e5f6",
            FullName = "Fatma Mahmoud",
            PhoneNumber = "+201200000000",
            DateOfBirth = "1990-05-15",
            Gender = "Female",
            HasChronicConditions = true,
            HasAllergies = false
        };

        // Act
        var summary = entity.ToSummary();

        // Assert
        Assert.Equal(entity.Id, summary.PatientId);
        Assert.Equal(entity.FullName, summary.FullName);
        Assert.Equal(entity.PhoneNumber, summary.PhoneNumber);
        Assert.True(summary.HasChronicConditions);
        Assert.False(summary.HasAllergies);
    }
}
