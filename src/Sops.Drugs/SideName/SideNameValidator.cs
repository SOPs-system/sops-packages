using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Sops.Drugs.Models;

namespace Sops.Drugs.SideName
{
    /// <summary>
    /// Validates SideName strings against the official SOPs server dynamic grammar regex.
    /// SideName represents dosage, strength, combination, or pack specification (e.g. "5", "10 mg", "500 mg / 20 tablet").
    /// </summary>
    public static class SideNameValidator
    {
        private static readonly ConcurrentDictionary<string, ConcentrationUnit> CustomUnits =
            new ConcurrentDictionary<string, ConcentrationUnit>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, DrugDosageForm> CustomForms =
            new ConcurrentDictionary<string, DrugDosageForm>(StringComparer.OrdinalIgnoreCase);

        private static string _unitPattern = string.Empty;
        private static string _dosagePattern = string.Empty;
        private static string _regexPattern = string.Empty;
        private static Regex _compiledRegex = null!;

        static SideNameValidator()
        {
            RebuildRegex();
        }

        /// <summary>Gets the compiled regex pattern string.</summary>
        public static string Pattern => _regexPattern;

        /// <summary>
        /// Checks whether a given string is a valid SideName according to SOPs grammar rules.
        /// </summary>
        /// <param name="value">The string to validate.</param>
        /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
        public static bool IsValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return _compiledRegex.IsMatch(value!.Trim());
        }

        /// <summary>
        /// Normalizes whitespace and standardizes '/' separators to " / ".
        /// </summary>
        /// <param name="input">The raw string.</param>
        /// <returns>A normalized string.</returns>
        public static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            string trimmed = Regex.Replace(input!.Trim(), @"\s+", " ");
            return Regex.Replace(trimmed, @"\s*/\s*", " / ");
        }

        /// <summary>
        /// Rebuilds the internal regex patterns using all supported concentration units and dosage forms.
        /// </summary>
        public static void RebuildRegex()
        {
            var unitTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "mg", "mgs", "ml", "mls", "iu", "mcg", "g", "gm", "gms", "ug", "µg", "μg", "%", "percent", "l", "pwd"
            };

            foreach (var key in CustomUnits.Keys)
            {
                unitTokens.Add(key);
            }

            var dosageTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "tablet", "tablets", "tab", "tabs",
                "tablet_fc", "tablet fc", "f.c. tab", "f.c. tabs", "f.c.tab", "f.c.tabs", "fct", "fc",
                "tablet_ec", "tablet ec", "e.c. tab", "e.c. tabs", "e.c.tab", "e.c.tabs",
                "tablet_eff", "eff. tab", "eff. tabs", "eff",
                "capsule", "capsules", "cap", "caps",
                "syrup", "syrups", "syr",
                "suspension", "suspensions", "susp",
                "solution", "solutions", "sol",
                "ointment", "ointments", "oint",
                "cream", "creams", "crm",
                "lotion", "lotions",
                "drops", "drop",
                "powder", "powders",
                "sachet", "sachets", "sach",
                "granules", "granule",
                "suppository", "suppositories", "supp", "supps",
                "patch", "patches",
                "inhaler", "inhalers",
                "spray", "sprays",
                "gel", "gels",
                "emulgel", "emulgels",
                "vial", "vials",
                "ampoule", "ampoules", "amp", "amps",
                "bottle", "bottles", "piece", "pieces", "strip", "strips", "box", "boxes", "dose", "doses"
            };

            foreach (var formName in Enum.GetNames(typeof(DrugDosageForm)))
            {
                string lower = formName.ToLowerInvariant();
                dosageTokens.Add(lower);
                if (lower.Contains("_"))
                {
                    dosageTokens.Add(lower.Replace('_', ' '));
                    dosageTokens.Add(lower.Replace('_', '-'));
                    dosageTokens.Add(lower.Replace("_", ""));
                }
            }

            foreach (var key in CustomForms.Keys)
            {
                dosageTokens.Add(key);
            }

            _unitPattern = BuildTokenAlternation(unitTokens);
            _dosagePattern = BuildTokenAlternation(dosageTokens);

            string segment = @"(?:" +
                @"(?:\d+(?:\.\d+)?(?:\s*[xX*]\s*\d+(?:\.\d+)?)?(?:\s*" + _unitPattern + @")?(?:\s*" + _dosagePattern + @")?)" +
                @"|" +
                @"(?:" + _unitPattern + @"|" + _dosagePattern + @")" +
                @")";

            _regexPattern = @"^(?=.*\d)" + segment + @"(?:\s*[/+,]\s*" + segment + @")*$";
            _compiledRegex = new Regex(_regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        private static string BuildTokenAlternation(IEnumerable<string> tokens)
        {
            var sorted = tokens
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(t => t.Length)
                .Select(t => Regex.Escape(t).Replace(@"\ ", @"\s*"));

            return "(?i:" + string.Join("|", sorted) + ")";
        }
    }
}
