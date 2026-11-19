namespace Sops.Models.Generator;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

/// <summary>
/// Loads and compiles JSON Schemas into intermediate model definitions.
/// </summary>
public static class SchemaLoader
{
    /// <summary>
    /// Loads and parses all JSON Schema files in the specified directory.
    /// </summary>
    /// <param name="schemasDirectory">Directory containing canonical JSON schema files.</param>
    /// <returns>A list of compiled <see cref="ModelDefinition"/> instances.</returns>
    public static List<ModelDefinition> LoadAll(string schemasDirectory)
    {
        if (!Directory.Exists(schemasDirectory))
        {
            throw new DirectoryNotFoundException($"Schemas directory not found: {schemasDirectory}");
        }

        var models = new List<ModelDefinition>();
        var files = Directory.GetFiles(schemasDirectory, "*.json", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            if (file.Contains("fixtures")) continue;

            models.Add(LoadFile(file, schemasDirectory));
        }

        return models;
    }

    /// <summary>
    /// Compiles a single JSON Schema file into a <see cref="ModelDefinition"/>.
    /// </summary>
    /// <param name="filePath">File path of the JSON schema.</param>
    /// <param name="baseDirectory">Base schemas directory for relative path derivation.</param>
    /// <returns>Compiled model definition.</returns>
    public static ModelDefinition LoadFile(string filePath, string baseDirectory)
    {
        var content = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        string id = root.TryGetProperty("$id", out var idProp) ? idProp.GetString() ?? "" : "";
        string title = root.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : Path.GetFileNameWithoutExtension(filePath);
        string description = root.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? "" : "";

        var relativePath = GetRelativePath(baseDirectory, filePath);
        var categoryDir = Path.GetDirectoryName(relativePath);
        string category = string.IsNullOrWhiteSpace(categoryDir) ? "Common" : ToPascalCase(categoryDir);

        var model = new ModelDefinition
        {
            SchemaId = id,
            Title = title,
            Description = description,
            DomainCategory = category,
            FilePath = filePath
        };

        // Check if top-level enum
        if (root.TryGetProperty("enum", out var enumProp) && enumProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in enumProp.EnumerateArray())
            {
                if (item.GetString() is { } val)
                {
                    model.EnumValues.Add(val);
                }
            }
            return model;
        }

        // Required properties set
        var requiredSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("required", out var reqProp) && reqProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in reqProp.EnumerateArray())
            {
                if (item.GetString() is { } r)
                {
                    requiredSet.Add(r);
                }
            }
        }

        // Properties dictionary
        if (root.TryGetProperty("properties", out var propsProp) && propsProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in propsProp.EnumerateObject())
            {
                var pDef = ParseProperty(prop.Name, prop.Value, requiredSet.Contains(prop.Name));
                model.Properties.Add(pDef);
            }
        }

        return model;
    }

    private static PropertyDefinition ParseProperty(string name, JsonElement element, bool isRequired)
    {
        var p = new PropertyDefinition
        {
            JsonName = name,
            PascalName = ToPascalCase(name),
            IsRequired = isRequired
        };

        if (element.TryGetProperty("description", out var descProp))
        {
            p.Description = descProp.GetString() ?? "";
        }

        if (element.TryGetProperty("$ref", out var refProp))
        {
            var refVal = refProp.GetString() ?? "";
            p.RefModelName = Path.GetFileNameWithoutExtension(refVal);
            p.SchemaType = "object";
            return p;
        }

        if (element.TryGetProperty("type", out var typeProp))
        {
            if (typeProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in typeProp.EnumerateArray())
                {
                    var typeStr = t.GetString();
                    if (typeStr == "null")
                    {
                        p.IsNullable = true;
                    }
                    else if (!string.IsNullOrEmpty(typeStr))
                    {
                        p.SchemaType = typeStr!;
                    }
                }
            }
            else
            {
                p.SchemaType = typeProp.GetString() ?? "object";
            }
        }

        if (element.TryGetProperty("format", out var formatProp))
        {
            p.Format = formatProp.GetString();
        }

        if (element.TryGetProperty("enum", out var enumProp) && enumProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in enumProp.EnumerateArray())
            {
                if (item.GetString() is { } ev)
                {
                    p.EnumValues.Add(ev);
                }
            }
        }

        if (p.SchemaType == "array")
        {
            p.IsArray = true;
            if (element.TryGetProperty("items", out var itemsProp))
            {
                if (itemsProp.TryGetProperty("$ref", out var itemRef))
                {
                    p.ArrayItemType = Path.GetFileNameWithoutExtension(itemRef.GetString());
                }
                else if (itemsProp.TryGetProperty("type", out var itemType))
                {
                    p.ArrayItemType = itemType.GetString();
                }
            }
        }

        return p;
    }

    /// <summary>
    /// Normalizes a string (snake_case, camelCase, or dash-separated) into PascalCase.
    /// </summary>
    public static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var parts = input.Split(new[] { '_', '-', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
        var result = "";
        foreach (var p in parts)
        {
            if (p.Length > 0)
            {
                result += char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p.Substring(1) : "");
            }
        }

        return string.IsNullOrEmpty(result) ? input : result;
    }

    private static string GetRelativePath(string relativeTo, string path)
    {
        var fullRelativeTo = Path.GetFullPath(relativeTo);
        var fullPath = Path.GetFullPath(path);
        if (!fullRelativeTo.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            fullRelativeTo += Path.DirectorySeparatorChar;
        }
        var uriRelativeTo = new Uri(fullRelativeTo);
        var uriPath = new Uri(fullPath);
        var relativeUri = uriRelativeTo.MakeRelativeUri(uriPath);
        return Uri.UnescapeDataString(relativeUri.ToString().Replace('/', Path.DirectorySeparatorChar));
    }
}
