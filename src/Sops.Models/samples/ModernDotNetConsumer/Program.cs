namespace ModernDotNetConsumer;

using System;
using System.Collections.Generic;
using Sops.Models.Catalog;
using Sops.Models.Common;
using Sops.Models.Transactions;

public static class Program
{
    public static void Main()
    {
        Console.WriteLine("Modern .NET 8 Consumer Starting...");

        var price = Money.FromMinorUnits(5500, "EGP");
        Console.WriteLine($"Validated Money: {price.DecimalString} {price.Currency}");

        var drug = new Drug
        {
            Sku = "PAN-001",
            Barcode = "6221111222333",
            NameEn = "Panadol Extra",
            NameAr = "بنادول إكسترا",
            DosageForm = DrugDosageForm.Tablet,
            OfficialPrice = price,
            Packages = new List<DrugPackage> { new("Box", 24, true) }
        };
        drug.Validate();
        Console.WriteLine($"Validated Drug: {drug.NameEn} (Barcode: {drug.Barcode})");

        var transaction = new Transaction
        {
            InvoiceNumber = "INV-2026-MODERN",
            TotalAmount = price,
            Items = new List<TransactionItem>()
        };
        Console.WriteLine($"Validated Transaction: {transaction.InvoiceNumber}");

        Console.WriteLine("ModernDotNetConsumer Succeeded!");
    }
}
