namespace Sops.Models.Generator.Tool;

using System;
using System.IO;
using System.Linq;
using Sops.Models.Generator;

/// <summary>
/// CLI tool entry point for sops-models-gen.
/// </summary>
public static class Program
{
    /// <summary>
    /// Executes the sops-models-gen command line tool.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code (0 for success, non-zero for error).</returns>
    public static int Main(string[] args)
    {
        Console.WriteLine("=============================================================");
        Console.WriteLine($"SOPs Canonical Model Generator Tool (sops-models-gen) v{GeneratorEngine.GeneratorVersion}");
        Console.WriteLine("=============================================================\n");

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return 0;
        }

        string command = args[0].ToLowerInvariant();

        // Parse optional arguments
        string? customSchemasDir = GetArgValue(args, "--schemas");
        string? customOutputDir = GetArgValue(args, "--output");
        string language = GetArgValue(args, "--language") ?? "all";

        // Determine default paths
        string rootDir = AppContext.BaseDirectory;
        var dirInfo = new DirectoryInfo(rootDir);
        while (dirInfo != null && !File.Exists(Path.Combine(dirInfo.FullName, "SOPs.Packages.sln")))
        {
            dirInfo = dirInfo.Parent;
        }

        string repoRoot = dirInfo?.FullName ?? Path.GetFullPath(Path.Combine(rootDir, "../../../../../../../"));
        string modelsEcosystemRoot = Path.Combine(repoRoot, "src", "Sops.Models");

        string schemasDir = customSchemasDir ?? Path.Combine(modelsEcosystemRoot, "schemas", "v1");
        string csharpDir = Path.Combine(modelsEcosystemRoot, "packages", "csharp", "Sops.Models");
        string tsFile = Path.Combine(modelsEcosystemRoot, "packages", "typescript", "src", "index.ts");
        string dartFile = Path.Combine(modelsEcosystemRoot, "packages", "dart", "lib", "sops_models.dart");
        string outputDir = customOutputDir ?? Path.Combine(modelsEcosystemRoot, "generated");

        switch (command)
        {
            case "version":
                Console.WriteLine($"Generator Tool Version : {GeneratorEngine.GeneratorVersion}");
                Console.WriteLine($"Canonical Schema Version: {GeneratorEngine.SchemaVersion}");
                Console.WriteLine($"Target Framework        : .NET 8.0");
                return 0;

            case "list":
                Console.WriteLine($"Discovering canonical models in: {schemasDir}\n");
                var models = GeneratorEngine.ListModels(schemasDir);
                Console.WriteLine($"Found {models.Count} canonical models across domain categories:\n");
                foreach (var group in models.GroupBy(m => m.DomainCategory).OrderBy(g => g.Key))
                {
                    Console.WriteLine($"  [{group.Key}]");
                    foreach (var m in group.OrderBy(m => m.Title))
                    {
                        string kind = m.IsEnum ? "Enum" : $"{m.Properties.Count} properties";
                        Console.WriteLine($"    - {m.Title,-28} ({kind}) -> {m.SchemaId}");
                    }
                    Console.WriteLine();
                }
                return 0;

            case "validate":
                Console.WriteLine($"Validating canonical JSON schemas in: {schemasDir}\n");
                var valResults = GeneratorEngine.Validate(schemasDir);
                int invalidCount = 0;
                foreach (var r in valResults)
                {
                    if (r.IsValid)
                    {
                        Console.WriteLine($"  [VALID]   {r.Title,-26} ({r.SchemaId})");
                    }
                    else
                    {
                        invalidCount++;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  [INVALID] {r.Title,-26} Errors: {string.Join(", ", r.Errors)}");
                        Console.ResetColor();
                    }
                }
                Console.WriteLine($"\nValidation Complete. Total: {valResults.Count}, Valid: {valResults.Count - invalidCount}, Errors: {invalidCount}");
                return invalidCount == 0 ? 0 : 1;

            case "check":
                Console.WriteLine($"Verifying zero schema-to-code drift across language packages...\n");
                var (csDrift, tsDrift, dartDrift) = GeneratorEngine.CheckDrift(schemasDir, csharpDir, tsFile, dartFile);
                int totalIssues = 0;

                if (csDrift.HasDrift)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("C# Model Drift:");
                    foreach (var d in csDrift.Discrepancies) { Console.WriteLine($"  - {d}"); totalIssues++; }
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine("  [OK] C# package (packages/csharp/Sops.Models) has ZERO drift.");
                }

                if (tsDrift.HasDrift)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\nTypeScript Contract Drift:");
                    foreach (var d in tsDrift.Discrepancies) { Console.WriteLine($"  - {d}"); totalIssues++; }
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine("  [OK] TypeScript package (packages/typescript) has ZERO drift.");
                }

                if (dartDrift.HasDrift)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\nDart Model Drift:");
                    foreach (var d in dartDrift.Discrepancies) { Console.WriteLine($"  - {d}"); totalIssues++; }
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine("  [OK] Dart package (packages/dart) has ZERO drift.");
                }

                Console.WriteLine($"\nDrift Check Complete. Total Discrepancies: {totalIssues}");
                return totalIssues == 0 ? 0 : 1;

            case "generate":
                Console.WriteLine($"Generating {language} model sources...");
                Console.WriteLine($"Schemas Source : {schemasDir}");
                Console.WriteLine($"Output Target  : {outputDir}\n");

                int count = GeneratorEngine.Generate(schemasDir, outputDir, language);
                Console.WriteLine($"Generation Complete. Generated {count} artifact(s).");
                return 0;

            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Unknown command: '{command}'\n");
                Console.ResetColor();
                PrintUsage();
                return 1;
        }
    }

    private static string? GetArgValue(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: sops-models-gen <command> [options]\n");
        Console.WriteLine("Commands:");
        Console.WriteLine("  validate   Validate all canonical JSON schemas and references");
        Console.WriteLine("  generate   Regenerate all supported language packages from schemas");
        Console.WriteLine("  check      Verify committed code matches schemas without rewriting files");
        Console.WriteLine("  list       Display canonical model inventory by domain category");
        Console.WriteLine("  version    Display generator and schema versions\n");
        Console.WriteLine("Options:");
        Console.WriteLine("  --schemas <path>   Custom path to schemas directory (default: schemas/v1)");
        Console.WriteLine("  --output <path>    Custom output path for generation (default: generated/)");
        Console.WriteLine("  --language <lang>  Target language: csharp, typescript, dart, all (default: all)");
    }
}
