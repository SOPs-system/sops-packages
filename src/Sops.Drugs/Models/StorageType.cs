namespace Sops.Drugs.Models
{
    /// <summary>
    /// Storage temperature and environmental conditions for pharmaceutical medications.
    /// </summary>
    public enum StorageType
    {
        /// <summary>Room temperature / ambient condition (typically 15°C to 25°C).</summary>
        NORMAL = 0,
        /// <summary>Refrigerated condition (typically 2°C to 8°C) for insulins, vaccines, and biologics.</summary>
        REFRIGERATOR = 1,
        /// <summary>Frozen condition (typically -20°C or colder).</summary>
        FREEZER = 2,
        /// <summary>Storage conditions not reliably verified.</summary>
        UNKNOWN = 99
    }
}
