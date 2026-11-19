namespace Sops.Models.Tests;

using System;
using System.Collections.Generic;
using Sops.Models.Billing;
using Sops.Models.Catalog;
using Sops.Models.Common;
using Sops.Models.Device;
using Sops.Models.Patients;
using Sops.Models.Transactions;
using Xunit;

public class ContractTests
{
    [Fact]
    public void Drug_Validate_ThrowsWhenSkuOrBarcodeIsMissing()
    {
        var drugWithoutSku = new Drug
        {
            Sku = "",
            Barcode = "123456",
            Packages = new List<DrugPackage> { new("Unit", 1) },
            OfficialPrice = Money.FromMinorUnits(100, "EGP")
        };

        Assert.Throws<InvalidOperationException>(() => drugWithoutSku.Validate());

        var drugWithoutBarcode = new Drug
        {
            Sku = "SKU-1",
            Barcode = "",
            Packages = new List<DrugPackage> { new("Unit", 1) },
            OfficialPrice = Money.FromMinorUnits(100, "EGP")
        };

        Assert.Throws<InvalidOperationException>(() => drugWithoutBarcode.Validate());
    }

    [Fact]
    public void Drug_Validate_ThrowsWhenOfficialPriceIsNegative()
    {
        var negativePriceDrug = new Drug
        {
            Sku = "SKU-1",
            Barcode = "123456",
            Packages = new List<DrugPackage> { new("Unit", 1) },
            OfficialPrice = Money.FromMinorUnits(-500, "EGP")
        };

        Assert.Throws<InvalidOperationException>(() => negativePriceDrug.Validate());
    }

    [Fact]
    public void DrugPackage_ThrowsWhenConversionFactorLessThanOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DrugPackage("InvalidTier", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DrugPackage("InvalidTier", -5));
    }

    [Fact]
    public void PaymentModels_RetailPOSAndSaaS_RemainStrictlySeparateTypes()
    {
        var retailPayment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            Method = PaymentMethod.Cash,
            Amount = Money.FromMinorUnits(1000, "EGP"),
            Status = PaymentStatus.Completed
        };

        var saasPayment = new BillingPayment
        {
            BillingPaymentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            InvoiceNumber = "SUB-2026-001",
            Amount = Money.FromMinorUnits(150000, "EGP"),
            Status = BillingPaymentStatus.Captured,
            GatewayProvider = "Paymob"
        };

        // Assert strictly different type hierarchies and namespaces
        Assert.NotEqual(retailPayment.GetType(), saasPayment.GetType());
        Assert.Equal("Sops.Models.Transactions", retailPayment.GetType().Namespace);
        Assert.Equal("Sops.Models.Billing", saasPayment.GetType().Namespace);
    }

    [Fact]
    public void PatientSummary_RedactsSensitiveClinicalDetails()
    {
        var summary = new PatientSummary
        {
            PatientId = Guid.NewGuid(),
            FullName = "Ahmed Mohamed",
            PhoneNumber = "+201012345678",
            HasChronicConditions = true,
            HasAllergies = true
        };

        // Summary only exposes booleans, never specific diagnostic strings or plaintext national IDs
        var properties = summary.GetType().GetProperties();
        var propNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in properties) propNames.Add(p.Name);

        Assert.DoesNotContain("NationalId", propNames);
        Assert.DoesNotContain("ChronicConditions", propNames);
        Assert.DoesNotContain("Allergies", propNames);
    }

    [Fact]
    public void DeviceIdentitySummary_DoesNotExposeRawHardwareSignals()
    {
        var summary = new DeviceIdentitySummary
        {
            DeviceId = Guid.NewGuid(),
            DeviceCode = "D7KX-92MA-88P",
            ChainId = Guid.NewGuid(),
            PharmacyId = Guid.NewGuid(),
            Platform = DevicePlatform.Windows,
            DeviceName = "POS-TERMINAL-01",
            Status = DeviceStatus.Active
        };

        var propNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in summary.GetType().GetProperties()) propNames.Add(p.Name);

        Assert.DoesNotContain("CpuId", propNames);
        Assert.DoesNotContain("MotherboardSerial", propNames);
        Assert.DoesNotContain("MacAddress", propNames);
        Assert.DoesNotContain("DiskSerial", propNames);
    }
}
