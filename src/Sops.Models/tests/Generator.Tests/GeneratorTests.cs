namespace Sops.Models.Generator.Tests;

using System;
using System.IO;
using System.Linq;
using Sops.Models.Generator;
using Xunit;

public class GeneratorTests
{
    private static string GetSchemasDir() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../schemas/v1"));

    private static string GetCSharpDir() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../packages/csharp/Sops.Models"));

    private static string GetTypeScriptFile() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../packages/typescript/src/index.ts"));

    private static string GetDartFile() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../packages/dart/lib/sops_models.dart"));

    [Fact]
    public void GeneratorEngine_Validate_ValidatesAll57CanonicalSchemas()
    {
        var schemasDir = GetSchemasDir();
        var results = GeneratorEngine.Validate(schemasDir);

        Assert.Equal(57, results.Count);
        Assert.All(results, r => Assert.True(r.IsValid, $"Validation failed for {r.Title}: {string.Join(", ", r.Errors)}"));
    }

    [Fact]
    public void GeneratorEngine_ListModels_GroupsModelsCorrectly()
    {
        var schemasDir = GetSchemasDir();
        var models = GeneratorEngine.ListModels(schemasDir);

        Assert.Equal(57, models.Count);
        var categories = models.Select(m => m.DomainCategory).Distinct().ToList();
        Assert.Contains("Catalog", categories);
        Assert.Contains("Common", categories);
        Assert.Contains("Transactions", categories);
    }

    [Fact]
    public void GeneratorEngine_CheckDrift_ReportsZeroDriftAcrossAllLanguages()
    {
        var schemasDir = GetSchemasDir();
        var csDir = GetCSharpDir();
        var tsFile = GetTypeScriptFile();
        var dartFile = GetDartFile();

        var (cs, ts, dart) = GeneratorEngine.CheckDrift(schemasDir, csDir, tsFile, dartFile);

        Assert.False(cs.HasDrift, $"C# drift detected: {string.Join(", ", cs.Discrepancies)}");
        Assert.False(ts.HasDrift, $"TypeScript drift detected: {string.Join(", ", ts.Discrepancies)}");
        Assert.False(dart.HasDrift, $"Dart drift detected: {string.Join(", ", dart.Discrepancies)}");
    }

    [Fact]
    public void GeneratorEngine_Generate_ProducesArtifactsDeterministically()
    {
        var schemasDir = GetSchemasDir();
        var tempOut = Path.Combine(Path.GetTempPath(), "sops_gen_test_" + Guid.NewGuid().ToString("N"));

        try
        {
            int count = GeneratorEngine.Generate(schemasDir, tempOut, "all");
            Assert.True(count > 0, "Should generate artifacts");
            Assert.True(File.Exists(Path.Combine(tempOut, "typescript", "generated_contracts.ts")));
            Assert.True(File.Exists(Path.Combine(tempOut, "dart", "sops_models_generated.dart")));
        }
        finally
        {
            if (Directory.Exists(tempOut))
            {
                Directory.Delete(tempOut, true);
            }
        }
    }
}
