namespace Sops.Models.Generator.LanguageGenerators;

using System;
using System.Text;

/// <summary>
/// Deterministic code generator for TypeScript domain contracts.
/// </summary>
public static class TypeScriptModelGenerator
{
    /// <summary>
    /// Generates TypeScript interface or type definition for the specified model definition.
    /// </summary>
    /// <param name="model">The compiled model definition.</param>
    /// <returns>Deterministic TypeScript declaration string.</returns>
    public static string GenerateSource(ModelDefinition model)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(model.Description))
        {
            sb.AppendLine("/**");
            sb.AppendLine($" * {model.Description}");
            sb.AppendLine(" */");
        }

        if (model.IsEnum)
        {
            sb.Append($"export type {model.Title} = ");
            sb.Append(string.Join(" | ", model.EnumValues.ConvertAll(v => $"'{v}'")));
            sb.AppendLine(";");
            return sb.ToString();
        }

        sb.AppendLine($"export interface {model.Title} {{");
        foreach (var p in model.Properties)
        {
            if (!string.IsNullOrWhiteSpace(p.Description))
            {
                sb.AppendLine($"  /** {p.Description} */");
            }
            string optional = (!p.IsRequired || p.IsNullable) ? "?" : "";
            sb.AppendLine($"  {p.JsonName}{optional}: {p.GetTypeScriptType()};");
        }
        sb.AppendLine("}");

        return sb.ToString();
    }
}
