namespace Net48Consumer
{
    using System;
    using System.Collections.Generic;
    using Sops.Models.Catalog;
    using Sops.Models.Common;
    using Sops.Models.Transactions;

    public static class Program
    {
        public static void Main()
        {
            Console.WriteLine(".NET Framework 4.8 Consumer Starting...");

            var price = Money.FromMinorUnits(3500, "EGP");
            Console.WriteLine(".NET 4.8 Money: " + price.DecimalString + " " + price.Currency);

            var drug = new Drug
            {
                Sku = "NET48-001",
                Barcode = "6221234567890",
                NameEn = "Aspirin Protect 100mg",
                NameAr = "أسبيرين بروتكت",
                DosageForm = DrugDosageForm.Tablet,
                OfficialPrice = price,
                Packages = new List<DrugPackage> { new DrugPackage("Box", 30, true) }
            };
            drug.Validate();
            Console.WriteLine(".NET 4.8 Drug Validated: " + drug.NameEn);

            Console.WriteLine("Net48Consumer Succeeded!");
        }
    }
}
