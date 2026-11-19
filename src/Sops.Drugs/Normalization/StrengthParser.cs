using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Sops.Drugs.Models;

namespace Sops.Drugs.Normalization
{
    /// <summary>
    /// Parses pharmaceutical active ingredient strengths, combinations, and units from text.
    /// </summary>
    public static class StrengthParser
    {
        private static readonly Regex StrengthRegex = new Regex(
            @"(?<val>\d+(?:\.\d+)?)\s*(?<unit>MG|ML|MCG|UG|µG|GM|G|IU|PERCENT|L|%)(?=\b|[^a-zA-Z0-9]|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ComboSlashRegex = new Regex(
            @"(?<v1>\d+(?:\.\d+)?)\s*/\s*(?<v2>\d+(?:\.\d+)?)(?:\s*/\s*(?<v3>\d+(?:\.\d+)?))?\s*(?<unit>MG|MCG|G|GM|IU)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Parses concentration unit string into <see cref="ConcentrationUnit"/>.
        /// </summary>
        /// <param name="unitString">The unit string (e.g. "MG", "GM", "%").</param>
        /// <returns>The matched <see cref="ConcentrationUnit"/>.</returns>
        public static ConcentrationUnit ParseUnit(string? unitString)
        {
            if (string.IsNullOrWhiteSpace(unitString))
                return ConcentrationUnit.MG;

            string s = unitString!.Trim().ToUpperInvariant();
            switch (s)
            {
                case "MG":
                case "MGS":
                    return ConcentrationUnit.MG;

                case "ML":
                case "MLS":
                    return ConcentrationUnit.ML;

                case "IU":
                case "I.U.":
                case "I.U":
                    return ConcentrationUnit.IU;

                case "MCG":
                case "UG":
                case "µG":
                case "ΜG":
                    return ConcentrationUnit.MCG;

                case "G":
                case "GM":
                case "GMS":
                case "GRAM":
                case "GRAMS":
                    return ConcentrationUnit.G;

                case "%":
                case "PERCENT":
                    return ConcentrationUnit.PERCENT;

                case "L":
                case "LITER":
                case "LITRE":
                    return ConcentrationUnit.L;

                default:
                    return ConcentrationUnit.OTHER;
            }
        }

        /// <summary>
        /// Parses active ingredient strings (separated by '+', ',', or ';') into structured <see cref="DrugIngredient"/> models.
        /// </summary>
        /// <param name="activeString">The raw active ingredient string (e.g. "PARACETAMOL 500 MG + CAFFEINE 65 MG").</param>
        /// <param name="tradeName">Optional trade name to supplement missing strength values.</param>
        /// <returns>A list of parsed active ingredients.</returns>
        public static List<DrugIngredient> ParseIngredients(string? activeString, string? tradeName = null)
        {
            var results = new List<DrugIngredient>();
            if (string.IsNullOrWhiteSpace(activeString) && string.IsNullOrWhiteSpace(tradeName))
                return results;

            string source = activeString ?? string.Empty;
            string[] parts = source.Split(new[] { '+', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length > 0)
            {
                foreach (string part in parts)
                {
                    string trimmed = part.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    var match = StrengthRegex.Match(trimmed);
                    if (match.Success &&
                        double.TryParse(match.Groups["val"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double conc))
                    {
                        var unit = ParseUnit(match.Groups["unit"].Value);
                        string rawIngName = StrengthRegex.Replace(trimmed, string.Empty).Trim();
                        string ingName = DrugNameNormalizer.ToTitleCase(rawIngName);
                        results.Add(new DrugIngredient(ingName, conc, unit));
                    }
                    else
                    {
                        // Active ingredient without explicit strength in its part
                        string ingName = DrugNameNormalizer.ToTitleCase(trimmed);
                        results.Add(new DrugIngredient(ingName, 0, ConcentrationUnit.MG));
                    }
                }
            }

            // If ingredients had no strength, try parsing strength from the trade name (e.g. "AUGMENTIN 1 GM")
            if (results.Count > 0 && results.TrueForAll(i => i.Concentration == 0) && !string.IsNullOrWhiteSpace(tradeName))
            {
                var nameStrengths = ExtractStrengthsFromText(tradeName!);
                if (nameStrengths.Count > 0)
                {
                    for (int i = 0; i < Math.Min(results.Count, nameStrengths.Count); i++)
                    {
                        results[i].Concentration = nameStrengths[i].concentration;
                        results[i].Unit = nameStrengths[i].unit;
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Extracts all numeric concentration strengths and units found within a text string.
        /// </summary>
        public static List<(double concentration, ConcentrationUnit unit)> ExtractStrengthsFromText(string text)
        {
            var list = new List<(double, ConcentrationUnit)>();
            if (string.IsNullOrWhiteSpace(text)) return list;

            // Check slash combination like "10/160 MG"
            var comboMatch = ComboSlashRegex.Match(text);
            if (comboMatch.Success)
            {
                var unit = ParseUnit(comboMatch.Groups["unit"].Value);
                if (double.TryParse(comboMatch.Groups["v1"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double v1))
                {
                    list.Add((v1, unit));
                }
                if (double.TryParse(comboMatch.Groups["v2"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double v2))
                {
                    list.Add((v2, unit));
                }
                if (comboMatch.Groups["v3"].Success &&
                    double.TryParse(comboMatch.Groups["v3"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double v3))
                {
                    list.Add((v3, unit));
                }
                return list;
            }

            var matches = StrengthRegex.Matches(text);
            foreach (Match m in matches)
            {
                if (double.TryParse(m.Groups["val"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                {
                    var unit = ParseUnit(m.Groups["unit"].Value);
                    list.Add((val, unit));
                }
            }

            return list;
        }
    }
}
