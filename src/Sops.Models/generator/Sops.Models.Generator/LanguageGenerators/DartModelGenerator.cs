namespace Sops.Models.Generator.LanguageGenerators;

using System;
using System.Text;

/// <summary>
/// Deterministic code generator for Dart domain contracts.
/// </summary>
public static class DartModelGenerator
{
    /// <summary>
    /// Generates Dart class definition for the specified model definition.
    /// </summary>
    /// <param name="model">The compiled model definition.</param>
    /// <returns>Deterministic Dart source code string.</returns>
    public static string GenerateSource(ModelDefinition model)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(model.Description))
        {
            sb.AppendLine("/// " + model.Description);
        }

        if (model.IsEnum)
        {
            sb.AppendLine($"enum {model.Title} {{");
            for (int i = 0; i < model.EnumValues.Count; i++)
            {
                var ev = model.EnumValues[i];
                var comma = i < model.EnumValues.Count - 1 ? "," : "";
                sb.AppendLine($"  {ev}{comma}");
            }
            sb.AppendLine("}");
            return sb.ToString();
        }

        sb.AppendLine($"class {model.Title} {{");

        // Fields
        foreach (var p in model.Properties)
        {
            sb.AppendLine($"  final {p.GetDartType()} {ToCamelCase(p.PascalName)};");
        }
        sb.AppendLine();

        // Constructor
        sb.AppendLine($"  const {model.Title}({{");
        foreach (var p in model.Properties)
        {
            string req = p.IsRequired && !p.IsNullable ? "required " : "";
            sb.AppendLine($"    {req}this.{ToCamelCase(p.PascalName)},");
        }
        sb.AppendLine("  });");
        sb.AppendLine();

        // fromJson factory
        sb.AppendLine($"  factory {model.Title}.fromJson(Map<String, dynamic> json) => {model.Title}(");
        foreach (var p in model.Properties)
        {
            sb.AppendLine($"    {ToCamelCase(p.PascalName)}: json['{p.JsonName}'] as {p.GetDartType()},");
        }
        sb.AppendLine("  );");
        sb.AppendLine();

        // toJson method
        sb.AppendLine("  Map<String, dynamic> toJson() => {");
        foreach (var p in model.Properties)
        {
            sb.AppendLine($"    '{p.JsonName}': {ToCamelCase(p.PascalName)},");
        }
        sb.AppendLine("  };");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string ToCamelCase(string pascal)
    {
        if (string.IsNullOrEmpty(pascal)) return "";
        return char.ToLowerInvariant(pascal[0]) + (pascal.Length > 1 ? pascal.Substring(1) : "");
    }
}
