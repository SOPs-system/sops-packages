namespace Sops.Models.Generator;

using System;
using System.Collections.Generic;

/// <summary>
/// Intermediate representation of a single property within a canonical model.
/// </summary>
public sealed class PropertyDefinition
{
    /// <summary>Gets or sets the canonical JSON property name (snake_case or camelCase).</summary>
    public string JsonName { get; set; } = string.Empty;

    /// <summary>Gets or sets the normalized PascalCase member name.</summary>
    public string PascalName { get; set; } = string.Empty;

    /// <summary>Gets or sets the primitive JSON Schema type (e.g. string, integer, number, boolean, array, object).</summary>
    public string SchemaType { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional format (e.g. uuid, date-time, date).</summary>
    public string? Format { get; set; }

    /// <summary>Gets or sets the reference model name if this property references another schema.</summary>
    public string? RefModelName { get; set; }

    /// <summary>Gets or sets whether this property is required according to the schema.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Gets or sets whether this property permits null values.</summary>
    public bool IsNullable { get; set; }

    /// <summary>Gets or sets the descriptive documentation text.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets whether this property represents an array.</summary>
    public bool IsArray { get; set; }

    /// <summary>Gets or sets the array item type if this property represents an array.</summary>
    public string? ArrayItemType { get; set; }

    /// <summary>Gets or sets the allowed enum string values if this property is a constrained string enum.</summary>
    public List<string> EnumValues { get; set; } = new List<string>();

    /// <summary>
    /// Gets the target C# type string.
    /// </summary>
    public string GetCSharpType()
    {
        string type;
        if (!string.IsNullOrWhiteSpace(RefModelName))
        {
            type = RefModelName!;
        }
        else if (EnumValues.Count > 0)
        {
            type = PascalName + "Type";
        }
        else
        {
            type = SchemaType switch
            {
                "integer" => Format == "int32" ? "int" : "long",
                "number" => "decimal",
                "boolean" => "bool",
                "string" => Format switch
                {
                    "uuid" => "Guid",
                    "date-time" => "DateTimeOffset",
                    "date" => "string",
                    _ => "string"
                },
                _ => "object"
            };
        }

        if (IsArray)
        {
            var itemType = ArrayItemType switch
            {
                "integer" => "long",
                "number" => "decimal",
                "boolean" => "bool",
                "string" => "string",
                "object" => "object",
                null => "string",
                _ => ArrayItemType
            };
            type = $"IReadOnlyList<{itemType}>";
        }

        if (!IsRequired || IsNullable)
        {
            if (type is "Guid" or "DateTimeOffset" or "int" or "long" or "decimal" or "bool")
            {
                type += "?";
            }
            else if (!IsArray)
            {
                type += "?";
            }
        }

        return type;
    }

    /// <summary>
    /// Gets the target TypeScript type string.
    /// </summary>
    public string GetTypeScriptType()
    {
        string type;
        if (!string.IsNullOrWhiteSpace(RefModelName))
        {
            type = RefModelName!;
        }
        else if (EnumValues.Count > 0)
        {
            type = string.Join(" | ", EnumValues.ConvertAll(v => $"'{v}'"));
        }
        else
        {
            type = SchemaType switch
            {
                "integer" or "number" => "number",
                "boolean" => "boolean",
                "string" => "string",
                _ => "any"
            };
        }

        if (IsArray)
        {
            var itemType = ArrayItemType switch
            {
                "integer" or "number" => "number",
                "boolean" => "boolean",
                "string" => "string",
                "object" => "Record<string, unknown>",
                null => "string",
                _ => ArrayItemType
            };
            type = $"{itemType}[]";
        }

        if (!IsRequired || IsNullable)
        {
            type += " | null";
        }

        return type;
    }

    /// <summary>
    /// Gets the target Dart type string.
    /// </summary>
    public string GetDartType()
    {
        string type;
        if (!string.IsNullOrWhiteSpace(RefModelName))
        {
            type = RefModelName!;
        }
        else
        {
            type = SchemaType switch
            {
                "integer" => "int",
                "number" => "double",
                "boolean" => "bool",
                "string" => "String",
                _ => "dynamic"
            };
        }

        if (IsArray)
        {
            var itemType = ArrayItemType switch
            {
                "integer" => "int",
                "number" => "double",
                "boolean" => "bool",
                "string" => "String",
                "object" => "Map<String, dynamic>",
                null => "String",
                _ => ArrayItemType == "string" ? "String" : (ArrayItemType == "object" ? "Map<String, dynamic>" : ArrayItemType)
            };
            type = $"List<{itemType}>";
        }

        if (!IsRequired || IsNullable)
        {
            if (type != "dynamic")
            {
                type += "?";
            }
        }

        return type;
    }
}

/// <summary>
/// Intermediate representation of a compiled domain model schema.
/// </summary>
public sealed class ModelDefinition
{
    /// <summary>Gets or sets the canonical schema URI identifier.</summary>
    public string SchemaId { get; set; } = string.Empty;

    /// <summary>Gets or sets the model title / class name.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the schema description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the domain category name (e.g. common, catalog, transactions).</summary>
    public string DomainCategory { get; set; } = "Common";

    /// <summary>Gets or sets the schema file path on disk.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Gets the collection of defined properties on this model.</summary>
    public List<PropertyDefinition> Properties { get; set; } = new List<PropertyDefinition>();

    /// <summary>Gets or sets the top-level enum values if this model is a pure enum definition.</summary>
    public List<string> EnumValues { get; set; } = new List<string>();

    /// <summary>Gets whether this model represents an enum.</summary>
    public bool IsEnum => EnumValues.Count > 0;
}
