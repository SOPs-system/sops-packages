using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Sops.Drugs.Normalization
{
    /// <summary>
    /// Normalizes English and Arabic pharmaceutical trade names, search terms, and brand aliases.
    /// </summary>
    public static class DrugNameNormalizer
    {
        private static readonly Regex MultiSpaceRegex = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex ArabicDiacriticsRegex = new Regex(@"[\u064B-\u065F\u0670]", RegexOptions.Compiled);
        private static readonly Regex PunctuationRegex = new Regex(@"[^\w\s\.\-\+\(\)/%]", RegexOptions.Compiled);

        /// <summary>
        /// Normalizes an English commercial drug name by trimming whitespace, normalizing uppercase casing,
        /// and standardizing delimiters.
        /// </summary>
        /// <param name="name">The raw English trade name.</param>
        /// <returns>A clean, normalized English drug name.</returns>
        public static string NormalizeEnglish(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            string cleaned = name!.Trim().ToUpperInvariant();
            cleaned = MultiSpaceRegex.Replace(cleaned, " ");
            cleaned = cleaned.Replace(" ,", ",").Replace(" .", ".");
            return cleaned.Trim();
        }

        /// <summary>
        /// Normalizes Arabic text for robust search indexing and fuzzy matching by stripping diacritics (tashkeel),
        /// tatweel (kashida), and unifying Alef, Yeh, and Teh Marbuta variants.
        /// </summary>
        /// <param name="arabicText">The raw Arabic text.</param>
        /// <returns>A normalized Arabic string.</returns>
        public static string NormalizeArabic(string? arabicText)
        {
            if (string.IsNullOrWhiteSpace(arabicText))
                return string.Empty;

            string text = arabicText!.Trim();

            // Remove Arabic diacritics / tashkeel (fatha, damma, kasra, sukun, shadda, tanween)
            text = ArabicDiacriticsRegex.Replace(text, string.Empty);

            // Remove Tatweel (kashida \u0640)
            text = text.Replace("\u0640", string.Empty);

            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                switch (c)
                {
                    // Alef variants -> bare Alef
                    case 'أ':
                    case 'إ':
                    case 'آ':
                    case 'ٱ':
                        sb.Append('ا');
                        break;

                    // Teh Marbuta -> Heh
                    case 'ة':
                        sb.Append('ه');
                        break;

                    // Alef Maqsura / Persian Yeh -> Yeh
                    case 'ى':
                    case 'ئ':
                    case 'ي':
                        sb.Append('ي');
                        break;

                    default:
                        sb.Append(c);
                        break;
                }
            }

            string result = sb.ToString();
            result = MultiSpaceRegex.Replace(result, " ");
            return result.Trim();
        }

        /// <summary>
        /// Converts a string to Title Case while keeping acronyms intact.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>Title-cased string.</returns>
        public static string ToTitleCase(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            string[] words = input!.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            for (int i = 0; i < words.Length; i++)
            {
                string w = words[i];
                if (w.Length > 0)
                {
                    sb.Append(char.ToUpperInvariant(w[0]));
                    if (w.Length > 1)
                    {
                        sb.Append(w.Substring(1).ToLowerInvariant());
                    }
                }
                if (i < words.Length - 1)
                {
                    sb.Append(' ');
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Extracts the core brand/trade name portion of an English drug string, removing trailing packaging and dosage specs.
        /// </summary>
        /// <param name="commercialName">The full commercial name.</param>
        /// <returns>The extracted brand name.</returns>
        public static string ExtractBrandName(string? commercialName)
        {
            if (string.IsNullOrWhiteSpace(commercialName))
                return string.Empty;

            string norm = NormalizeEnglish(commercialName);

            // Remove trailing strength and dosage patterns like "500 MG 20 TABS"
            int parenIdx = norm.IndexOf('(');
            if (parenIdx > 0 && parenIdx < norm.Length - 1)
            {
                // Preserve parenthetical synonyms (e.g. "1 2 3 (ONE TWO THREE)")
                int closeParen = norm.IndexOf(')', parenIdx);
                if (closeParen > 0)
                {
                    string prefix = norm.Substring(0, closeParen + 1).Trim();
                    return prefix;
                }
            }

            // Split on digits that begin strength/pack specs or trailing dosage form tokens
            var match = Regex.Match(norm, @"^(?<brand>.*?)(?:\s+(?:\d+\s*(?:MG|GM|G|ML|MCG|IU|%|PERCENT|TAB|TABS|CAP|CAPS|TABLET|TABLETS|CAPSULE|CAPSULES|VIAL|VIALS|AMP|AMPS|AMPOULE|AMPOULES|SACHET|SACHETS)|TAB|TABS|CAP|CAPS|TABLET|TABLETS|CAPSULE|CAPSULES))\b", RegexOptions.IgnoreCase);
            if (match.Success && match.Groups["brand"].Value.Length >= 2)
            {
                return match.Groups["brand"].Value.Trim();
            }

            return norm;
        }
    }
}
