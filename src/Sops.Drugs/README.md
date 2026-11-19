# Sops.Drugs

`Sops.Drugs` is a production-quality, high-performance C# library for Egyptian pharmaceutical catalog models, bilingual normalization (Arabic and English), dosage form and strength parsing, check-digit barcode validation, side-name generation, and in-memory indexed drug search.

## Features

- **Standard Models**: Typed domain models (`Drug`, `DrugCategory`, `DrugIngredient`, `DrugPackageInfo`, `DrugCatalog`).
- **Bilingual Normalization**: Advanced phonetic and linguistic normalization for Egyptian Arabic search aliases (Alef/Yeh/Teh-Marbuta normalization, tashkeel removal) and English trade names.
- **SideName Grammar Engine**: Dedicated `SideNameFormatter` and `SideNameValidator` ensuring 100% compliance with SOPs server strength, dosage, and package specification grammar (`500 mg / 20 tablet`).
- **Resilient CSV Engine**: High-performance, streaming RFC-4180 CSV parser and writer with native UTF-8 and Arabic character support.
- **GS1 Barcode Validation**: Modulo-10 check digit verification for EAN-13, EAN-8, UPC-A, and GTIN-14 barcodes, with conflict detection.
- **Storage Classification**: Identifies cold-chain items (`REFRIGERATOR` 2°C–8°C for insulins, vaccines, and biologics, `FREEZER` -20°C, and `NORMAL`).
- **Fast In-Memory Search**: Zero-dependency `DrugCatalogIndex` providing instant bilingual tokenized search, barcode lookups, ingredient queries, and consistency validation.
- **Universal Compatibility**: Targets `netstandard2.0` (compatible with .NET 6/8/9+ and .NET Framework 4.8).

---

## Installation

```bash
dotnet add package Sops.Drugs
```

---

## Quick Usage

### 1. In-Memory Search & Barcode Lookup

```csharp
using Sops.Drugs.Runtime;

// Load and index catalog from JSON
var index = DrugCatalogIndex.LoadFromFile("sops-drugs-import.json");

// Search by English or Arabic brand name
var resultsEn = index.Search("Panadol Extra");
var resultsAr = index.Search("كونجيستال");

// Lookup by Barcode
var drug = index.FindByBarcode("6221000003295");
if (drug != null)
{
    Console.WriteLine($"Found: {drug.NameEn} - {drug.Price:F2} EGP - SideName: {drug.SideName}");
}
```

### 2. SideName Formatting and Validation

```csharp
using Sops.Drugs.Models;
using Sops.Drugs.SideName;

var ingredients = new List<DrugIngredient>
{
    new DrugIngredient("Paracetamol", 500, ConcentrationUnit.MG)
};

string sideName = SideNameFormatter.Format(ingredients, DrugDosageForm.TABLET, units: 20);
Console.WriteLine(sideName); // "500 mg / 20 tablet"

bool isValid = SideNameValidator.IsValid(sideName); // true
```

### 3. Barcode Check Digit Verification

```csharp
using Sops.Drugs.Normalization;

bool valid = BarcodeValidator.ValidateCheckDigit("6221000003295"); // true
bool invalid = BarcodeValidator.ValidateCheckDigit("6221000003290"); // false
```

### 4. Catalog Building and Acquisition

```csharp
using Sops.Drugs.Builder;

// Download verified source datasets from GitHub
var downloader = new SourceAcquisitionService();
var (src1, src2) = await downloader.AcquireSourcesAsync("./data");

// Merge, normalize, and export complete catalog
var builder = new CatalogBuilderService();
var catalog = builder.BuildCatalog("./data", "./output", src1, src2);

// Or execute the CLI tool programmatically
await DrugCatalogCli.RunAsync(new[] { "build", "--data-dir", "./data", "--out-dir", "./output" });
```
