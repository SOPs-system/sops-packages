using System;
using System.Text.RegularExpressions;
using Sops.Drugs.Models;

namespace Sops.Drugs.Normalization
{
    /// <summary>
    /// Classifies drug storage temperature conditions (NORMAL, REFRIGERATOR, FREEZER, UNKNOWN).
    /// </summary>
    public static class StorageClassifier
    {
        // Verified active ingredients and brand patterns requiring cold storage (2°C - 8°C)
        private static readonly Regex ColdChainRegex = new Regex(
            @"\b(INSULIN|GLARGINE|ASPART|LISPRO|DETEMIR|DEGLUDEC|SEMAGLUTIDE|LIRAGLUTIDE|DULAGLUTIDE|TIRZEPATIDE|" +
            @"LANTUS|HUMALOG|NOVORAPID|MIXTARD|ACTRAPID|INSULATARD|TOUJEO|TRESIBA|LEVEMIR|RYZODEG|OZEMPIC|VICTOZA|TRULICITY|MOUNJARO|" +
            @"VACCINE|TOXOID|IMMUNOGLOBULIN|ERYTHROPOIETIN|EPOETIN|FILGRASTIM|INTERFERON|SOMATROPIN|" +
            @"ETANERCEPT|ADALIMUMAB|INFLIXIMAB|TRASTUZUMAB|RITUXIMAB|BEVACIZUMAB|" +
            @"OXYTOCIN|LATANOPROST|XALATAN|BOTULINUM)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Explicit temperature mentions in descriptions
        private static readonly Regex ExplicitColdRegex = new Regex(
            @"(?:2\s*[-–]\s*8\s*°?\s*C|REFRIGERAT|COLD\s*STORAGE|KEEP\s*IN\s*FRIDGE|ثلاجة|حفظ\s*في\s*الثلاجة|2\s*إلى\s*8\s*درجة)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Classifies the storage condition of a drug based on verified active ingredients, trade names, and descriptions.
        /// </summary>
        /// <param name="name">Commercial trade name.</param>
        /// <param name="activeIngredients">Active ingredients string.</param>
        /// <param name="description">Product description or route.</param>
        /// <returns>The verified or determined <see cref="StorageType"/>.</returns>
        public static StorageType Classify(string? name, string? activeIngredients, string? description = null)
        {
            string combined = $"{name} {activeIngredients} {description}".ToUpperInvariant();

            if (ExplicitColdRegex.IsMatch(combined) || ColdChainRegex.IsMatch(combined))
            {
                return StorageType.REFRIGERATOR;
            }

            if (combined.Contains("FREEZER") || combined.Contains("FROZEN") || combined.Contains("-20"))
            {
                return StorageType.FREEZER;
            }

            // Standard ambient products
            return StorageType.NORMAL;
        }
    }
}
