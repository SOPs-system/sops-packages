namespace Sops.Models.Generator;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Result of checking schema-to-code drift.
/// </summary>
public sealed class DriftCheckResult
{
    /// <summary>Gets or sets the total count of schemas checked.</summary>
    public int TotalSchemasChecked { get; set; }

    /// <summary>Gets whether any drift or discrepancy was detected.</summary>
    public bool HasDrift => Discrepancies.Count > 0;

    /// <summary>Gets or sets the list of discrepancies found.</summary>
    public List<string> Discrepancies { get; set; } = new List<string>();
}

/// <summary>
/// Detects discrepancies and schema drift between canonical JSON schemas and code artifacts.
/// </summary>
public static class DriftChecker
{
    /// <summary>
    /// Checks for drift between schemas in <paramref name="schemasDirectory"/> and C# models in <paramref name="csharpSourceDirectory"/>.
    /// Inspects all C# source files for matching type declarations (class, record, struct, enum).
    /// </summary>
    /// <param name="schemasDirectory">Directory containing canonical JSON schemas.</param>
    /// <param name="csharpSourceDirectory">Directory containing C# source files.</param>
    /// <returns>The drift check summary.</returns>
    public static DriftCheckResult CheckCSharpDrift(string schemasDirectory, string csharpSourceDirectory)
    {
        var discrepancies = new List<string>();
        var schemaFiles = Directory.GetFiles(schemasDirectory, "*.json", SearchOption.AllDirectories)
            .Where(f => !f.Contains("fixtures"))
            .ToList();

        var csFiles = Directory.GetFiles(csharpSourceDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("obj") && !f.Contains("bin"))
            .ToList();

        var combinedCSharp = string.Join("\n", csFiles.Select(File.ReadAllText));

        foreach (var file in schemaFiles)
        {
            var schemaName = Path.GetFileNameWithoutExtension(file);
            var pattern = $@"\b(class|record|struct|enum)\s+{Regex.Escape(schemaName)}\b";
            if (!Regex.IsMatch(combinedCSharp, pattern))
            {
                discrepancies.Add($"Schema '{schemaName}' ({Path.GetFileName(file)}) does not have a matching C# type definition in '{csharpSourceDirectory}'.");
            }
        }

        return new DriftCheckResult
        {
            TotalSchemasChecked = schemaFiles.Count,
            Discrepancies = discrepancies
        };
    }

    /// <summary>
    /// Checks for drift between schemas in <paramref name="schemasDirectory"/> and TypeScript contract declarations in <paramref name="typeScriptSourceFile"/>.
    /// </summary>
    /// <param name="schemasDirectory">Directory containing canonical JSON schemas.</param>
    /// <param name="typeScriptSourceFile">File containing TypeScript declarations.</param>
    /// <returns>The drift check summary.</returns>
    public static DriftCheckResult CheckTypeScriptDrift(string schemasDirectory, string typeScriptSourceFile)
    {
        var discrepancies = new List<string>();
        var schemaFiles = Directory.GetFiles(schemasDirectory, "*.json", SearchOption.AllDirectories)
            .Where(f => !f.Contains("fixtures"))
            .ToList();

        if (!File.Exists(typeScriptSourceFile))
        {
            return new DriftCheckResult
            {
                TotalSchemasChecked = 0,
                Discrepancies = new List<string> { $"TypeScript source file not found: {typeScriptSourceFile}" }
            };
        }

        var tsContent = File.ReadAllText(typeScriptSourceFile);

        foreach (var file in schemaFiles)
        {
            var schemaName = Path.GetFileNameWithoutExtension(file);
            var interfaceDecl = $"interface {schemaName}";
            var typeDecl = $"type {schemaName}";
            var enumDecl = $"enum {schemaName}";

            if (!tsContent.Contains(interfaceDecl) && 
                !tsContent.Contains(typeDecl) && 
                !tsContent.Contains(enumDecl))
            {
                discrepancies.Add($"Schema '{schemaName}' ({Path.GetFileName(file)}) is missing from TypeScript contracts in '{typeScriptSourceFile}'.");
            }
        }

        return new DriftCheckResult
        {
            TotalSchemasChecked = schemaFiles.Count,
            Discrepancies = discrepancies
        };
    }

    /// <summary>
    /// Checks for drift between schemas in <paramref name="schemasDirectory"/> and Dart contract declarations in <paramref name="dartSourceFile"/>.
    /// </summary>
    /// <param name="schemasDirectory">Directory containing canonical JSON schemas.</param>
    /// <param name="dartSourceFile">File containing Dart declarations.</param>
    /// <returns>The drift check summary.</returns>
    public static DriftCheckResult CheckDartDrift(string schemasDirectory, string dartSourceFile)
    {
        var discrepancies = new List<string>();
        var schemaFiles = Directory.GetFiles(schemasDirectory, "*.json", SearchOption.AllDirectories)
            .Where(f => !f.Contains("fixtures"))
            .ToList();

        if (!File.Exists(dartSourceFile) && !Directory.Exists(dartSourceFile))
        {
            return new DriftCheckResult
            {
                TotalSchemasChecked = 0,
                Discrepancies = new List<string> { $"Dart source file not found: {dartSourceFile}" }
            };
        }

        string dartContent = "";
        if (Directory.Exists(dartSourceFile))
        {
            var files = Directory.GetFiles(dartSourceFile, "*.dart", SearchOption.AllDirectories);
            dartContent = string.Join("\n", files.Select(File.ReadAllText));
        }
        else
        {
            var dir = Path.GetDirectoryName(dartSourceFile);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "*.dart", SearchOption.AllDirectories);
                dartContent = string.Join("\n", files.Select(File.ReadAllText));
            }
            else
            {
                dartContent = File.ReadAllText(dartSourceFile);
            }
        }

        foreach (var file in schemaFiles)
        {
            var schemaName = Path.GetFileNameWithoutExtension(file);
            var classDecl = $"class {schemaName}";
            var enumDecl = $"enum {schemaName}";

            if (!dartContent.Contains(classDecl) && !dartContent.Contains(enumDecl))
            {
                discrepancies.Add($"Schema '{schemaName}' ({Path.GetFileName(file)}) is missing from Dart contracts in '{dartSourceFile}'.");
            }
        }

        return new DriftCheckResult
        {
            TotalSchemasChecked = schemaFiles.Count,
            Discrepancies = discrepancies
        };
    }
}
