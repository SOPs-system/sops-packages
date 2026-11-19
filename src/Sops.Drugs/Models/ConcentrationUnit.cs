namespace Sops.Drugs.Models
{
    /// <summary>
    /// Units of concentration and strength for active pharmaceutical ingredients.
    /// </summary>
    public enum ConcentrationUnit
    {
        /// <summary>Milligram (mg)</summary>
        MG = 0,
        /// <summary>Milliliter (ml)</summary>
        ML = 1,
        /// <summary>International Unit (IU)</summary>
        IU = 2,
        /// <summary>Microgram (mcg, ug, µg)</summary>
        MCG = 3,
        /// <summary>Gram (g, gm)</summary>
        G = 4,
        /// <summary>Percentage concentration (%)</summary>
        PERCENT = 5,
        /// <summary>Liter (l)</summary>
        L = 6,
        /// <summary>Other or unspecified unit</summary>
        OTHER = 99
    }
}
