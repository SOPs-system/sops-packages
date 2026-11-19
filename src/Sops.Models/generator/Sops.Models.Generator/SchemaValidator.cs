namespace Sops.Models.Generator;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

/// <summary>
/// Result of validating a single JSON Schema definition.
/// </summary>
public sealed class SchemaValidationResult
{
    /// <summary>Gets or sets the schema file path.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the schema title or type name.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the schema $id URI.</summary>
    public string SchemaId { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the schema is valid.</summary>
    public bool IsValid { get; set; }

    /// <summary>Gets or sets any validation errors encountered.</summary>
    public List<string> Errors { get; set; } = new List<string>();
}

/// <summary>
/// Validates JSON schemas against structural requirements, draft 2020-12 standards, and SOPs domain conventions.
/// </summary>
public static class SchemaValidator
{
    /// <summary>
    /// Validates all JSON schema files found within the specified root directory.
    /// </summary>
    /// <param name="schemasDirectory">The directory path containing JSON schema files.</param>
    /// <returns>A collection of validation results for each discovered schema.</returns>
    public static List<SchemaValidationResult> ValidateAll(string schemasDirectory)
    {
        if (!Directory.Exists(schemasDirectory))
        {
            throw new DirectoryNotFoundException($"Schemas directory not found: {schemasDirectory}");
        }

        var results = new List<SchemaValidationResult>();
        var files = Directory.GetFiles(schemasDirectory, "*.json", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            if (file.Contains("fixtures")) continue;

            results.Add(ValidateFile(file));
        }

        return results;
    }

    /// <summary>
    /// Validates a single JSON schema file.
    /// </summary>
    /// <param name="filePath">Path to the JSON schema file.</param>
    /// <returns>The validation result.</returns>
    public static SchemaValidationResult ValidateFile(string filePath)
    {
        var errors = new List<string>();
        string title = Path.GetFileNameWithoutExtension(filePath);
        string schemaId = string.Empty;

        try
        {
            var content = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(content))
            {
                return new SchemaValidationResult
                {
                    FilePath = filePath,
                    Title = title,
                    IsValid = false,
                    Errors = new List<string> { "File is empty." }
                };
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (!root.TryGetProperty("$schema", out var schemaProp))
            {
                errors.Add("Missing '$schema' declaration.");
            }
            else if (!schemaProp.GetString()!.Contains("json-schema.org"))
            {
                errors.Add($"Invalid '$schema' value: '{schemaProp.GetString()}'. Must point to json-schema.org.");
            }

            if (!root.TryGetProperty("$id", out var idProp))
            {
                errors.Add("Missing '$id' declaration.");
            }
            else
            {
                schemaId = idProp.GetString() ?? string.Empty;
            }

            if (!root.TryGetProperty("title", out var titleProp))
            {
                errors.Add("Missing 'title' declaration.");
            }
            else
            {
                title = titleProp.GetString() ?? title;
            }

            if (!root.TryGetProperty("type", out _))
            {
                errors.Add("Missing 'type' declaration.");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"JSON parsing exception: {ex.Message}");
        }

        return new SchemaValidationResult
        {
            FilePath = filePath,
            Title = title,
            SchemaId = schemaId,
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}
