using System;
using System.Text.RegularExpressions;
using Sops.Drugs.Models;

namespace Sops.Drugs.Normalization
{
    /// <summary>
    /// Parses package counts, strip counts, and packaging tiers from commercial trade names and metadata.
    /// </summary>
    public static class PackageParser
    {
        private static readonly Regex StripsPattern = new Regex(
            @"(?<strips>\d+)\s*(?:STRIP|STRIPS)\s*(?:OF|X|\*)\s*(?<perStrip>\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SimpleUnitCountPattern = new Regex(
            @"\b(?<count>\d+)\s*(?:F\.?C\.?\s*TABS?|E\.?C\.?\s*TABS?|EFF\.?\s*TABS?|TABLETS?|TABS?|CAPSULES?|CAPS?|SACHETS?|AMPOULES?|AMPS?|VIALS?|PIECES?|SUPPOSITORIES?|SUPP)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex LiquidVolumePattern = new Regex(
            @"\b(?<vol>\d+)\s*(?:ML|L)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Parses the total unit count and packaging tier from trade name and explicit unit text.
        /// </summary>
        /// <param name="tradeName">The commercial drug name.</param>
        /// <param name="explicitUnits">Optional explicit units string or number from dataset.</param>
        /// <param name="dosageForm">The identified dosage form.</param>
        /// <returns>A tuple containing total units, strip count (if any), and units per strip (if any).</returns>
        public static (int totalUnits, int strips, int unitsPerStrip) Parse(
            string? tradeName,
            string? explicitUnits,
            DrugDosageForm dosageForm)
        {
            // First check for multi-strip specifications like "3 STRIPS OF 10 TABS"
            if (!string.IsNullOrWhiteSpace(tradeName))
            {
                var stripMatch = StripsPattern.Match(tradeName!);
                if (stripMatch.Success &&
                    int.TryParse(stripMatch.Groups["strips"].Value, out int sCount) &&
                    int.TryParse(stripMatch.Groups["perStrip"].Value, out int pCount) &&
                    sCount > 0 && pCount > 0)
                {
                    return (sCount * pCount, sCount, pCount);
                }

                // Check simple unit count like "20 TABLETS"
                var unitMatch = SimpleUnitCountPattern.Match(tradeName!);
                if (unitMatch.Success && int.TryParse(unitMatch.Groups["count"].Value, out int uCount) && uCount > 0)
                {
                    // If it's 20, 30, etc. tablets, assume 10 per strip if divisible
                    int strips = (uCount % 10 == 0 && uCount >= 20) ? uCount / 10 : 1;
                    int perStrip = strips > 1 ? 10 : uCount;
                    return (uCount, strips, perStrip);
                }
            }

            // Check explicit units field (e.g. from eg-drugs dataset: units: 1, 20, etc.)
            if (!string.IsNullOrWhiteSpace(explicitUnits) &&
                int.TryParse(explicitUnits, out int expUnits) &&
                expUnits > 0)
            {
                int strips = (expUnits % 10 == 0 && expUnits >= 20) ? expUnits / 10 : 1;
                int perStrip = strips > 1 ? 10 : expUnits;
                return (expUnits, strips, perStrip);
            }

            // For liquids, creams, ointments: default 1 unit (1 bottle / 1 tube)
            return (1, 1, 1);
        }
    }
}
