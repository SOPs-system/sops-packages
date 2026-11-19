using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sops.Drugs.Runtime;
using Sops.Drugs.Serialization;

namespace Sops.Drugs.Builder
{
    /// <summary>
    /// Command-line interface and programmatic orchestrator for building Egyptian drug catalogs.
    /// </summary>
    public static class DrugCatalogCli
    {
        /// <summary>
        /// Main application CLI execution method.
        /// </summary>
        public static async Task<int> RunAsync(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine(" Sops.Drugs — Egyptian Drug Catalog Builder CLI  ");
            Console.WriteLine("=================================================");

            string command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

            string baseDir = AppContext.BaseDirectory;
            string currentDir = Directory.GetCurrentDirectory();

            string defaultDataDir = Path.Combine(currentDir, "data");
            if (!File.Exists(Path.Combine(defaultDataDir, "eg_drugs.csv")))
            {
                string candidate1 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "data"));
                string candidate2 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "data"));
                if (File.Exists(Path.Combine(candidate1, "eg_drugs.csv")))
                {
                    defaultDataDir = candidate1;
                }
                else if (File.Exists(Path.Combine(candidate2, "eg_drugs.csv")))
                {
                    defaultDataDir = candidate2;
                }
            }

            string defaultOutDir = Path.Combine(currentDir, "output");
            if (!Directory.Exists(defaultOutDir))
            {
                string candidateOut = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "output"));
                if (Directory.Exists(candidateOut) || Directory.Exists(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."))))
                {
                    defaultOutDir = candidateOut;
                }
            }

            string dataDir = GetArgument(args, new[] { "--data-dir", "--data", "-d" }, defaultDataDir);
            string outDir = GetArgument(args, new[] { "--out-dir", "--out", "-o" }, defaultOutDir);
            string catalogPath = GetArgument(args, new[] { "--catalog", "-c" }, Path.Combine(outDir, "sops-drugs-import.json"));
            int sampleCount = int.TryParse(GetArgument(args, new[] { "--count", "-n" }, "50"), out int sc) ? sc : 50;

            using (var cts = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                    Console.WriteLine("\n[Operation canceled by user]");
                };

                try
                {
                    switch (command)
                    {
                        case "fetch":
                            return await ExecuteFetchAsync(dataDir, cts.Token).ConfigureAwait(false);

                        case "build":
                            return ExecuteBuild(dataDir, outDir);

                        case "validate":
                            return ExecuteValidate(catalogPath);

                        case "export":
                            return ExecuteExport(catalogPath, outDir);

                        case "sample":
                            return ExecuteSample(catalogPath, outDir, sampleCount);

                        case "report":
                            return ExecuteReport(dataDir, outDir);

                        case "all":
                            int fetchResult = await ExecuteFetchAsync(dataDir, cts.Token).ConfigureAwait(false);
                            if (fetchResult != 0) return fetchResult;
                            return ExecuteBuild(dataDir, outDir);

                        case "help":
                        case "--help":
                        case "-h":
                        default:
                            PrintHelp();
                            return 0;
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("\nExecution was interrupted.");
                    return 130;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n[FATAL ERROR]: {ex.Message}");
                    Console.ResetColor();
                    Console.WriteLine(ex.StackTrace);
                    return 1;
                }
            }
        }

        private static async Task<int> ExecuteFetchAsync(string dataDir, CancellationToken cancellationToken)
        {
            Console.WriteLine($"\n[ACTION]: Fetching source datasets to {dataDir}...");
            var service = new SourceAcquisitionService();
            await service.AcquireSourcesAsync(dataDir, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("[SUCCESS]: Source datasets acquired and verified successfully.");
            return 0;
        }

        private static int ExecuteBuild(string dataDir, string outDir)
        {
            Console.WriteLine($"\n[ACTION]: Building pharmaceutical catalog from {dataDir} -> {outDir}...");
            var service = new CatalogBuilderService();

            string s1Path = Path.Combine(dataDir, "eg_drugs.csv");
            string s2Path = Path.Combine(dataDir, "egyptian-drugs.csv");

            var s1Prov = File.Exists(s1Path) ? new Models.SourceProvenance
            {
                Name = "mahmoudfalous/eg-drugs",
                RepositoryUrl = "https://github.com/mahmoudfalous/eg-drugs",
                CommitHash = SourceAcquisitionService.FalousCommit,
                FilePath = "data/eg_drugs.csv",
                Sha256Hash = SourceAcquisitionService.ComputeFileSha256(s1Path),
                FileSizeBytes = new FileInfo(s1Path).Length,
                License = "CC BY 4.0"
            } : null;

            var s2Prov = File.Exists(s2Path) ? new Models.SourceProvenance
            {
                Name = "karem505/egyptian-drug-database",
                RepositoryUrl = "https://github.com/karem505/egyptian-drug-database",
                CommitHash = SourceAcquisitionService.KaremCommit,
                FilePath = "data/egyptian-drugs.csv",
                Sha256Hash = SourceAcquisitionService.ComputeFileSha256(s2Path),
                FileSizeBytes = new FileInfo(s2Path).Length,
                License = "CC0 1.0 (Public Domain)"
            } : null;

            var catalog = service.BuildCatalog(dataDir, outDir, s1Prov, s2Prov);
            Console.WriteLine($"[SUCCESS]: Successfully built catalog with {catalog.Drugs.Count:N0} drugs and {catalog.Categories.Count:N0} categories.");
            return 0;
        }

        private static int ExecuteValidate(string catalogPath)
        {
            Console.WriteLine($"\n[ACTION]: Validating catalog at: {catalogPath}...");
            if (!File.Exists(catalogPath))
            {
                Console.WriteLine($"Error: Catalog file not found at {catalogPath}.");
                return 1;
            }

            var index = DrugCatalogIndex.LoadFromFile(catalogPath);
            var issues = index.ValidateCatalog();

            int errors = 0;
            int warnings = 0;
            foreach (var issue in issues)
            {
                if (issue.Severity == "ERROR") errors++;
                else if (issue.Severity == "WARNING") warnings++;
            }

            Console.WriteLine($"Validation Results: {errors} Errors, {warnings} Warnings across {index.Count:N0} drugs.");
            return errors == 0 ? 0 : 1;
        }

        private static int ExecuteExport(string catalogPath, string outDir)
        {
            Console.WriteLine($"\n[ACTION]: Exporting server formats from {catalogPath} -> {outDir}...");
            var catalog = DrugCatalogSerializer.DeserializeCatalogFromFile(catalogPath);
            string dbPath = Path.Combine(outDir, "sop-db-export.json");
            DrugCatalogSerializer.SerializeServerDbExportToFile(catalog, dbPath);
            Console.WriteLine($"[SUCCESS]: Exported {dbPath}");
            return 0;
        }

        private static int ExecuteSample(string catalogPath, string outDir, int count)
        {
            Console.WriteLine($"\n[ACTION]: Generating {count}-item sample export from {catalogPath}...");
            var catalog = DrugCatalogSerializer.DeserializeCatalogFromFile(catalogPath);
            string samplePath = Path.Combine(outDir, $"sop-db-export-SAMPLE-{count}.json");
            DrugCatalogSerializer.SerializeServerDbExportToFile(catalog, samplePath, limit: count);
            Console.WriteLine($"[SUCCESS]: Generated sample export at {samplePath}");
            return 0;
        }

        private static int ExecuteReport(string dataDir, string outDir)
        {
            Console.WriteLine($"\n[ACTION]: Generating reports for {dataDir}...");
            return ExecuteBuild(dataDir, outDir);
        }

        private static void PrintHelp()
        {
            Console.WriteLine("\nUsage: sops-drugs <command> [options]\n");
            Console.WriteLine("Commands:");
            Console.WriteLine("  fetch     Download pinned source CSV files from GitHub");
            Console.WriteLine("  build     Parse, normalize, match, format SideNames, and generate catalogs");
            Console.WriteLine("  validate  Validate referential integrity and SideName grammar");
            Console.WriteLine("  export    Export snake_case database schema for Sop.Server");
            Console.WriteLine("  sample    Export sample subset of N drugs (--count 50)");
            Console.WriteLine("  report    Generate review, barcode conflict, and refrigerated CSV reports");
            Console.WriteLine("  all       Run fetch, build, validate, export, and reports end-to-end");
            Console.WriteLine("\nOptions:");
            Console.WriteLine("  --data-dir <path>   Directory containing source CSVs (default: ./data)");
            Console.WriteLine("  --out-dir <path>    Directory for generated catalogs and reports (default: ./output)");
            Console.WriteLine("  --catalog <path>    Path to sops-drugs-import.json");
            Console.WriteLine("  --count <number>    Count for sample command (default: 50)");
        }

        private static string GetArgument(string[] args, string[] flags, string defaultValue)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                foreach (string flag in flags)
                {
                    if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    {
                        return args[i + 1];
                    }
                }
            }
            return defaultValue;
        }
    }
}
