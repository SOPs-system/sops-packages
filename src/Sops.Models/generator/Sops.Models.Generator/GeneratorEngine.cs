namespace Sops.Models.Generator;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Sops.Models.Generator.LanguageGenerators;

/// <summary>
/// High-level orchestration engine for the SOPs cross-language generator library.
/// </summary>
public static class GeneratorEngine
{
    /// <summary>Canonical schema version.</summary>
    public const string SchemaVersion = "1.0.0";

    /// <summary>Generator tool and library version.</summary>
    public const string GeneratorVersion = "0.1.0";

    /// <summary>
    /// Validates all schemas in the specified directory.
    /// </summary>
    public static List<SchemaValidationResult> Validate(string schemasDirectory) =>
        SchemaValidator.ValidateAll(schemasDirectory);

    /// <summary>
    /// Lists all canonical models discovered in the specified directory.
    /// </summary>
    public static List<ModelDefinition> ListModels(string schemasDirectory) =>
        SchemaLoader.LoadAll(schemasDirectory);

    /// <summary>
    /// Generates code for all canonical models into the specified language and directory.
    /// </summary>
    /// <param name="schemasDirectory">Directory containing canonical JSON schemas.</param>
    /// <param name="outputDirectory">Output directory to write generated code.</param>
    /// <param name="language">Target language: 'csharp', 'typescript', 'dart', or 'all'.</param>
    /// <returns>Count of files generated.</returns>
    public static int Generate(string schemasDirectory, string outputDirectory, string language = "all")
    {
        var models = SchemaLoader.LoadAll(schemasDirectory);
        int generatedCount = 0;

        string lang = language.ToLowerInvariant();
        bool genCSharp = lang is "all" or "csharp" or "c#";
        bool genTs = lang is "all" or "typescript" or "ts";
        bool genDart = lang is "all" or "dart";

        if (genCSharp)
        {
            var csDir = Path.Combine(outputDirectory, "csharp");
            Directory.CreateDirectory(csDir);
            foreach (var m in models)
            {
                var targetSubdir = Path.Combine(csDir, m.DomainCategory);
                Directory.CreateDirectory(targetSubdir);
                var code = CSharpModelGenerator.GenerateSource(m);
                var filePath = Path.Combine(targetSubdir, $"{m.Title}.g.cs");
                File.WriteAllText(filePath, code);
                generatedCount++;
            }
        }

        if (genTs)
        {
            var tsDir = Path.Combine(outputDirectory, "typescript");
            Directory.CreateDirectory(tsDir);
            var sb = new StringBuilder();
            sb.AppendLine("/**");
            sb.AppendLine(" * @sops-system/models — Deterministically generated TypeScript contracts");
            sb.AppendLine($" * Generator: v{GeneratorVersion} | Schema: v{SchemaVersion}");
            sb.AppendLine(" */");
            sb.AppendLine();
            foreach (var m in models)
            {
                sb.AppendLine(TypeScriptModelGenerator.GenerateSource(m));
            }
            File.WriteAllText(Path.Combine(tsDir, "generated_contracts.ts"), sb.ToString());
            generatedCount++;
        }

        if (genDart)
        {
            var dartDir = Path.Combine(outputDirectory, "dart");
            Directory.CreateDirectory(dartDir);
            var sb = new StringBuilder();
            sb.AppendLine("// Deterministically generated Dart models");
            sb.AppendLine($"// Generator: v{GeneratorVersion} | Schema: v{SchemaVersion}");
            sb.AppendLine("library sops_models_generated;");
            sb.AppendLine();
            foreach (var m in models)
            {
                sb.AppendLine(DartModelGenerator.GenerateSource(m));
            }
            File.WriteAllText(Path.Combine(dartDir, "sops_models_generated.dart"), sb.ToString());
            generatedCount++;
        }

        return generatedCount;
    }

    /// <summary>
    /// Checks code drift across all languages against canonical schemas.
    /// </summary>
    public static (DriftCheckResult CSharp, DriftCheckResult TypeScript, DriftCheckResult Dart) CheckDrift(
        string schemasDirectory,
        string csharpDirectory,
        string typeScriptFile,
        string dartFile)
    {
        var cs = DriftChecker.CheckCSharpDrift(schemasDirectory, csharpDirectory);
        var ts = DriftChecker.CheckTypeScriptDrift(schemasDirectory, typeScriptFile);
        var dart = DriftChecker.CheckDartDrift(schemasDirectory, dartFile);
        return (cs, ts, dart);
    }

    /// <summary>
    /// Generates markdown summary table documenting canonical models.
    /// </summary>
    public static string GenerateSchemaIndexMarkdown(string schemasDirectory)
    {
        var models = SchemaLoader.LoadAll(schemasDirectory)
            .OrderBy(m => m.Title, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("| Model Title | Schema ID | Domain Category | Properties Count | Description |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");

        foreach (var m in models)
        {
            sb.AppendLine($"| **{m.Title}** | `{m.SchemaId}` | `{m.DomainCategory}` | {m.Properties.Count} | {m.Description} |");
        }

        return sb.ToString();
    }
}
