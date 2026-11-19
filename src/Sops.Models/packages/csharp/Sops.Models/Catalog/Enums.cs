namespace Sops.Models.Catalog;

using System.Text.Json.Serialization;

/// <summary>
/// Pharmaceutical dosage form classification.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DrugDosageForm
{
    /// <summary>Solid oral tablet.</summary>
    Tablet,
    /// <summary>Solid oral capsule.</summary>
    Capsule,
    /// <summary>Liquid syrup formulation.</summary>
    Syrup,
    /// <summary>Liquid oral suspension formulation.</summary>
    Suspension,
    /// <summary>Powder or granule sachet.</summary>
    Sachet,
    /// <summary>Parenteral vial or ampoule injection.</summary>
    Injection,
    /// <summary>Eye, ear, or nasal drops.</summary>
    Drop,
    /// <summary>Topical cream formulation.</summary>
    Cream,
    /// <summary>Topical ointment formulation.</summary>
    Ointment,
    /// <summary>Topical gel formulation.</summary>
    Gel,
    /// <summary>Respiratory inhaler.</summary>
    Inhaler,
    /// <summary>Transdermal patch.</summary>
    Patch,
    /// <summary>Rectal or vaginal suppository.</summary>
    Suppository,
    /// <summary>Topical or throat spray.</summary>
    Spray,
    /// <summary>Topical lotion.</summary>
    Lotion,
    /// <summary>Unspecified dosage form.</summary>
    Other
}

/// <summary>
/// Physical storage requirement for pharmaceutical products.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StorageType
{
    /// <summary>Standard ambient storage (15-25°C).</summary>
    RoomTemperature,
    /// <summary>Refrigerated cold chain (2-8°C).</summary>
    Refrigerated,
    /// <summary>Frozen storage (below -15°C).</summary>
    Frozen,
    /// <summary>Controlled schedule substance requiring lock and key storage.</summary>
    ControlledSubstance
}

/// <summary>
/// Lifecycle status of a catalogue drug.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DrugStatus
{
    /// <summary>Active and available for dispensing.</summary>
    Active,
    /// <summary>Temporarily suspended by manufacturer or authority.</summary>
    Suspended,
    /// <summary>Permanently discontinued.</summary>
    Discontinued,
    /// <summary>Recalled by regulatory body; dispensing prohibited.</summary>
    Recalled
}
