namespace Sops.Models.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Sops.Models.Catalog;
using Sops.Models.Common;
using Sops.Models.Device;
using Sops.Models.Patients;
using Sops.Models.Transactions;
using Xunit;

public class SerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static string GetFixturesDirectory()
    {
        var p1 = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../schemas/fixtures"));
        if (Directory.Exists(p1)) return p1;
        var p2 = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../schemas/v1/fixtures"));
        if (Directory.Exists(p2)) return p2;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../schemas/v1/fixtures"));
    }

    [Fact]
    public void Money_SerializesWithCanonicalCamelCaseAndLegacySnakeCase()
    {
        // Arrange
        var money = Money.FromMinorUnits(15075, "EGP");

        // Act
        var json = JsonSerializer.Serialize(money);
        var deserialized = JsonSerializer.Deserialize<Money>(json);

        // Assert - contains both canonical camelCase and legacy snake_case, plus decimalString
        Assert.Contains("\"amountInMinorUnits\":15075", json);
        Assert.Contains("\"amount_minor_units\":15075", json);
        Assert.Contains("\"currency\":\"EGP\"", json);
        Assert.Contains("\"decimalString\":\"150.75\"", json);

        Assert.Equal(money.AmountInMinorUnits, deserialized.AmountInMinorUnits);
        Assert.Equal(money.AmountMinorUnits, deserialized.AmountMinorUnits);
        Assert.Equal(money.Currency, deserialized.Currency);
        Assert.Equal(150.75m, deserialized.ToDecimal());
    }

    [Fact]
    public void Money_DeserializesPureCanonicalCamelCaseWireFormat()
    {
        // Arrange
        var json = """{"amountInMinorUnits": 25050, "currency": "EGP", "decimalString": "250.50"}""";

        // Act
        var money = JsonSerializer.Deserialize<Money>(json);

        // Assert
        Assert.Equal(25050, money.AmountInMinorUnits);
        Assert.Equal("EGP", money.Currency);
        Assert.Equal(250.50m, money.ToDecimal());
    }

    [Fact]
    public void Money_DeserializesPureLegacySnakeCaseWireFormat()
    {
        // Arrange
        var json = """{"amount_minor_units": 18000, "currency": "EGP"}""";

        // Act
        var money = JsonSerializer.Deserialize<Money>(json);

        // Assert
        Assert.Equal(18000, money.AmountInMinorUnits);
        Assert.Equal("EGP", money.Currency);
        Assert.Equal(180.00m, money.ToDecimal());
    }

    [Fact]
    public void Money_DeserializesDecimalStringFallbackWhenAmountOmitted()
    {
        // Arrange
        var json = """{"currency": "EGP", "decimalString": "99.95"}""";

        // Act
        var money = JsonSerializer.Deserialize<Money>(json);

        // Assert
        Assert.Equal(9995, money.AmountInMinorUnits);
        Assert.Equal("EGP", money.Currency);
        Assert.Equal(99.95m, money.ToDecimal());
    }

    [Fact]
    public void Money_ProtectsLargeInteger_ExceedingJavaScriptSafeIntegerLimit()
    {
        // 9,007,199,254,740,995 is strictly greater than JS Number.MAX_SAFE_INTEGER (9,007,199,254,740,991)
        long hugeMinorUnits = 9007199254740995L;
        var money = Money.FromMinorUnits(hugeMinorUnits, "EGP");

        var json = JsonSerializer.Serialize(money);
        var deserialized = JsonSerializer.Deserialize<Money>(json);

        Assert.Equal(hugeMinorUnits, deserialized.AmountInMinorUnits);
        Assert.Equal("90071992547409.95", deserialized.DecimalString);
        Assert.Contains("\"amountInMinorUnits\":9007199254740995", json);
    }

    [Fact]
    public void Quantity_RoundTripsSuccessfully()
    {
        // Arrange
        var qty = new Quantity(12.5m, "tablets");

        // Act
        var json = JsonSerializer.Serialize(qty);
        var deserialized = JsonSerializer.Deserialize<Quantity>(json);

        // Assert
        Assert.Contains("\"value\":12.5", json);
        Assert.Contains("\"unit\":\"tablets\"", json);
        Assert.Equal(qty.Value, deserialized.Value);
        Assert.Equal(qty.Unit, deserialized.Unit);
    }

    [Fact]
    public void Drug_RoundTrips_WithArabicUnicodeAndEmbeddedPackages()
    {
        // Arrange
        var drug = new Drug
        {
            Id = Guid.NewGuid(),
            Sku = "EDA-2024-001",
            Barcode = "6221234567890",
            NameEn = "Panadol Extra 500mg",
            NameAr = "بنادول إكسترا 500 مجم",
            GenericName = "Paracetamol / Caffeine",
            DosageForm = DrugDosageForm.Tablet,
            OfficialPrice = Money.FromMinorUnits(4500, "EGP"),
            IsPriceRegulated = true,
            RequiresPrescription = false,
            StorageType = StorageType.RoomTemperature,
            Packages = new List<DrugPackage>
            {
                new("Box", 24, true),
                new("Strip", 12, false),
                new("Unit", 1, false)
            },
            ActiveIngredients = new List<ActiveIngredient>
            {
                new() { Name = "Paracetamol", Strength = "500 mg" },
                new() { Name = "Caffeine", Strength = "65 mg" }
            },
            Status = DrugStatus.Active,
            CatalogRevision = 1
        };

        // Act
        var json = JsonSerializer.Serialize(drug);
        var deserialized = JsonSerializer.Deserialize<Drug>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(drug.Id, deserialized!.Id);
        Assert.Equal(drug.NameAr, deserialized.NameAr);
        Assert.Equal(drug.Barcode, deserialized.Barcode);
        Assert.Equal(3, deserialized.Packages.Count);
        Assert.Equal(24, deserialized.Packages[0].ConversionFactorToBase);
        Assert.Equal(45.00m, deserialized.OfficialPrice.ToDecimal());
    }

    [Fact]
    public void Drug_SupportsSachetDosageForm()
    {
        var drug = new Drug
        {
            Sku = "SKU-SACHET-01",
            Barcode = "6229999000111",
            NameEn = "Catafast 50mg Sachet",
            NameAr = "كاتافاست فوار أكياس",
            DosageForm = DrugDosageForm.Sachet,
            OfficialPrice = Money.FromMinorUnits(6000, "EGP"),
            Packages = new List<DrugPackage> { new("Box", 9, true), new("Sachet", 1, false) }
        };

        var json = JsonSerializer.Serialize(drug);
        Assert.Contains("\"dosage_form\":\"Sachet\"", json);

        var deserialized = JsonSerializer.Deserialize<Drug>(json);
        Assert.Equal(DrugDosageForm.Sachet, deserialized!.DosageForm);
    }

    [Fact]
    public void Transaction_SupportsAnonymousCheckout_WithNullPatientId()
    {
        // Arrange
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = "INV-2026-9999",
            PharmacyId = Guid.NewGuid(),
            TerminalId = Guid.NewGuid(),
            CashierUserId = Guid.NewGuid(),
            ShiftId = Guid.NewGuid(),
            PatientId = null, // Anonymous checkout
            Type = TransactionType.Sale,
            Status = TransactionStatus.Completed,
            Subtotal = Money.FromMinorUnits(10000, "EGP"),
            DiscountAmount = Money.Zero("EGP"),
            TaxAmount = Money.Zero("EGP"),
            TotalAmount = Money.FromMinorUnits(10000, "EGP"),
            Items = new List<TransactionItem>
            {
                new()
                {
                    ItemId = Guid.NewGuid(),
                    DrugId = Guid.NewGuid(),
                    DrugName = "Panadol Extra",
                    PackageTier = "Box",
                    Quantity = new Quantity(2, "boxes"),
                    UnitPrice = Money.FromMinorUnits(5000, "EGP"),
                    DiscountAmount = Money.Zero("EGP"),
                    TaxRate = 0m,
                    TotalLineAmount = Money.FromMinorUnits(10000, "EGP")
                }
            },
            Payments = new List<Payment>
            {
                new()
                {
                    PaymentId = Guid.NewGuid(),
                    Method = PaymentMethod.Cash,
                    Amount = Money.FromMinorUnits(10000, "EGP"),
                    TenderedAmount = Money.FromMinorUnits(10000, "EGP"),
                    ChangeReturned = Money.Zero("EGP"),
                    Status = PaymentStatus.Completed
                }
            },
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        // Act
        var json = JsonSerializer.Serialize(transaction);
        var deserialized = JsonSerializer.Deserialize<Transaction>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Null(deserialized!.PatientId);
        Assert.Single(deserialized.Items);
        Assert.Single(deserialized.Payments);
        Assert.Equal(PaymentMethod.Cash, deserialized.Payments[0].Method);
    }

    [Fact]
    public void CrossLanguageFixtures_AllDeserializeSuccessfully()
    {
        var fixturesDir = GetFixturesDirectory();

        // 1. Money Canonical
        var moneyPath = Path.Combine(fixturesDir, "money_canonical.json");
        var money = JsonSerializer.Deserialize<Money>(File.ReadAllText(moneyPath));
        Assert.Equal(15075, money.AmountInMinorUnits);
        Assert.Equal("EGP", money.Currency);

        // 2. Drug Panadol
        var drugPath = Path.Combine(fixturesDir, "drug_panadol.json");
        var drug = JsonSerializer.Deserialize<Drug>(File.ReadAllText(drugPath));
        Assert.NotNull(drug);
        Assert.Equal("EDA-2024-001", drug!.Sku);
        Assert.Equal("بنادول إكسترا 500 مجم", drug.NameAr);
        Assert.Equal(3, drug.Packages.Count);

        // 3. Transaction Retail
        var txPath = Path.Combine(fixturesDir, "transaction_retail.json");
        var tx = JsonSerializer.Deserialize<Transaction>(File.ReadAllText(txPath));
        Assert.NotNull(tx);
        Assert.Null(tx!.PatientId);
        Assert.Equal("INV-2026-0042", tx.InvoiceNumber);

        // 4. Patient Summary
        var patPath = Path.Combine(fixturesDir, "patient_summary.json");
        var pat = JsonSerializer.Deserialize<PatientSummary>(File.ReadAllText(patPath));
        Assert.NotNull(pat);
        Assert.Equal("Fatma Mahmoud", pat!.FullName);

        // 5. Device Identity
        var devPath = Path.Combine(fixturesDir, "device_identity.json");
        var dev = JsonSerializer.Deserialize<DeviceIdentitySummary>(File.ReadAllText(devPath));
        Assert.NotNull(dev);
        Assert.Equal("D7KX-92MA-88P", dev!.DeviceCode);
    }

    [Fact]
    public void TolerantReader_SkipsUnknownJsonFieldsWithoutError()
    {
        // Arrange
        var jsonWithUnknown = """
        {
            "amountInMinorUnits": 5000,
            "currency": "EGP",
            "future_field_not_yet_known": "experimental_value",
            "experimental_nested_object": { "key": 123 }
        }
        """;

        // Act
        var money = JsonSerializer.Deserialize<Money>(jsonWithUnknown);

        // Assert
        Assert.Equal(5000, money.AmountInMinorUnits);
        Assert.Equal("EGP", money.Currency);
    }
}
