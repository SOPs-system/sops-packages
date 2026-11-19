using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Sops.Drugs.Builder;
using Xunit;

namespace Sops.Drugs.Tests
{
    public class BuilderTests
    {
        [Fact]
        public void ComputeFileSha256_ComputesCorrectHexHash()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "Hello, SOPs Egyptian Drug Database!", Encoding.UTF8);
                string hash = SourceAcquisitionService.ComputeFileSha256(tempFile);

                Assert.NotNull(hash);
                Assert.Equal(64, hash.Length);
                Assert.Matches("^[0-9a-f]{64}$", hash);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void CatalogBuilderService_BuildsCatalogFromSyntheticData()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "sops_builder_test_" + Guid.NewGuid().ToString("N"));
            string outDir = Path.Combine(tempDir, "output");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create minimal source 1 CSV
                string s1 = Path.Combine(tempDir, "eg_drugs.csv");
                File.WriteAllText(s1,
                    "name,arabic,active,company,price,oldprice,barcode,units,description,uses\n" +
                    "\"CONCOR 5 MG 30 TAB\",\"كونكور\",\"Bisoprolol 5mg\",\"MERCK\",\"40.5\",\"35.0\",\"6221234567897\",\"30 Tablets\",\"Antihypertensive\",\"High blood pressure\"\n",
                    Encoding.UTF8);

                // Create minimal source 2 CSV
                string s2 = Path.Combine(tempDir, "egyptian-drugs.csv");
                File.WriteAllText(s2,
                    "commercial_name_en,commercial_name_ar,scientific_name,manufacturer,drug_class,route,price_egp\n" +
                    "\"CONCOR 5MG 30 TABLETS\",\"كونكور 5 مجم\",\"Bisoprolol\",\"Merck\",\"Cardiovascular\",\"Oral\",\"40.5\"\n",
                    Encoding.UTF8);

                var builder = new CatalogBuilderService();
                var catalog = builder.BuildCatalog(tempDir, outDir);

                Assert.NotNull(catalog);
                Assert.Single(catalog.Drugs);
                var drug = catalog.Drugs[0];
                Assert.Equal("CONCOR 5 MG 30 TAB", drug.NameEn);
                Assert.Equal(40.5m, drug.Price);
                Assert.Equal(4050, drug.PricePiastres);
                Assert.Contains("eg-drugs", drug.Sources);
                Assert.Contains("egyptian-drug-database", drug.Sources);

                // Verify exported artifacts
                Assert.True(File.Exists(Path.Combine(outDir, "sops-drugs-import.json")));
                Assert.True(File.Exists(Path.Combine(outDir, "sop-db-export.json")));
                Assert.True(File.Exists(Path.Combine(outDir, "sop-db-export-SAMPLE-50.json")));
                Assert.True(File.Exists(Path.Combine(outDir, "sops-drugs-review.csv")));
                Assert.True(File.Exists(Path.Combine(outDir, "sops-drugs-barcode-conflicts.csv")));
                Assert.True(File.Exists(Path.Combine(outDir, "sops-drugs-build-manifest.json")));
                Assert.True(File.Exists(Path.Combine(outDir, "sops-drugs-validation-report.json")));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task DrugCatalogCli_HelpCommand_ReturnsZero()
        {
            int exitCode = await DrugCatalogCli.RunAsync(new[] { "help" });
            Assert.Equal(0, exitCode);
        }

        [Fact]
        public async Task DrugCatalogCli_ValidateNonExistentFile_ReturnsOne()
        {
            int exitCode = await DrugCatalogCli.RunAsync(new[] { "validate", "--catalog", "non_existent_catalog.json" });
            Assert.Equal(1, exitCode);
        }
    }
}
