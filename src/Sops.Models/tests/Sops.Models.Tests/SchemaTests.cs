namespace Sops.Models.Tests;

using System;
using System.IO;
using System.Text.Json;
using Sops.Models.Generator;
using Xunit;

public class SchemaTests
{
    private static string GetSchemasDirectory() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../schemas/v1"));

    private static string GetCSharpDirectory()
    {
        var p1 = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../packages/csharp/Sops.Models"));
        if (Directory.Exists(p1)) return p1;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Sops.Models"));
    }

    private static string GetTypeScriptSourceFile()
    {
        var p1 = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../packages/typescript/src/index.ts"));
        if (File.Exists(p1)) return p1;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/sops-models-ts/src/index.ts"));
    }

    [Fact]
    public void AllJsonSchemas_AreWellFormedAndValidJson()
    {
        // Arrange
        var schemaDirectory = GetSchemasDirectory();
        Assert.True(Directory.Exists(schemaDirectory), $"Schema directory not found: {schemaDirectory}");

        var schemaFiles = Directory.GetFiles(schemaDirectory, "*.json", SearchOption.AllDirectories);
        Assert.NotEmpty(schemaFiles);

        // Act & Assert
        foreach (var file in schemaFiles)
        {
            if (file.Contains("fixtures")) continue;

            var content = File.ReadAllText(file);
            Assert.False(string.IsNullOrWhiteSpace(content), $"Schema file is empty: {file}");

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("$schema", out var schemaProp), $"Missing $schema in {file}");
            Assert.Contains("json-schema.org", schemaProp.GetString());

            Assert.True(root.TryGetProperty("title", out _), $"Missing title in {file}");
            Assert.True(root.TryGetProperty("type", out _), $"Missing type in {file}");
        }
    }

    [Fact]
    public void DrugSchema_HasEmbeddedPackagesAndAuthoritativeBarcode()
    {
        var schemaPath = Path.Combine(GetSchemasDirectory(), "catalog/Drug.json");
        var content = File.ReadAllText(schemaPath);
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var properties = root.GetProperty("properties");

        Assert.True(properties.TryGetProperty("barcode", out _));
        Assert.True(properties.TryGetProperty("packages", out _));
        Assert.True(properties.TryGetProperty("official_price", out _));
    }

    [Fact]
    public void Generator_ValidateAll_ReportsAllCanonicalSchemasValid()
    {
        var schemasDir = GetSchemasDirectory();
        var results = SchemaValidator.ValidateAll(schemasDir);

        Assert.Equal(57, results.Count);
        foreach (var r in results)
        {
            Assert.True(r.IsValid, $"Schema '{r.Title}' validation failed: {string.Join(", ", r.Errors)}");
        }
    }

    [Fact]
    public void Generator_DriftChecker_VerifiesZeroCSharpDrift()
    {
        var schemasDir = GetSchemasDirectory();
        var csharpDir = GetCSharpDirectory();

        var result = DriftChecker.CheckCSharpDrift(schemasDir, csharpDir);
        Assert.False(result.HasDrift, $"Detected C# schema drift:\n{string.Join("\n", result.Discrepancies)}");
        Assert.Equal(57, result.TotalSchemasChecked);
    }

    [Fact]
    public void Generator_DriftChecker_VerifiesZeroTypeScriptDrift()
    {
        var schemasDir = GetSchemasDirectory();
        var tsFile = GetTypeScriptSourceFile();

        var result = DriftChecker.CheckTypeScriptDrift(schemasDir, tsFile);
        Assert.False(result.HasDrift, $"Detected TypeScript schema drift:\n{string.Join("\n", result.Discrepancies)}");
        Assert.Equal(57, result.TotalSchemasChecked);
    }

    [Fact]
    public void Generator_CodeGenerator_GeneratesDeterministicSchemaIndex()
    {
        var schemasDir = GetSchemasDirectory();
        var markdown = GeneratorEngine.GenerateSchemaIndexMarkdown(schemasDir);

        Assert.False(string.IsNullOrWhiteSpace(markdown));
        Assert.Contains("| **Drug** | `https://sops.org/schemas/v1/catalog/Drug.json` |", markdown);
        Assert.Contains("| **Money** | `https://sops.org/schemas/v1/common/Money.json` |", markdown);
    }
}
