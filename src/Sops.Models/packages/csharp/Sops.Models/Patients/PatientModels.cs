namespace Sops.Models.Patients;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Sops.Models.Common;

/// <summary>
/// Biological gender enum.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Gender
{
    /// <summary>Male.</summary>
    Male,
    /// <summary>Female.</summary>
    Female,
    /// <summary>Unspecified.</summary>
    Unspecified
}

/// <summary>
/// Globally authoritative patient directory profile.
/// Protects sensitive national ID through blind index hashing and stores clinical alerts.
/// </summary>
public sealed record Patient
{
    /// <summary>
    /// Gets the unique global patient identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the blind HMAC hash of the national identity card for search without plaintext exposure.
    /// </summary>
    [JsonPropertyName("national_id_hash")]
    public string? NationalIdHash { get; init; }

    /// <summary>
    /// Gets the patient's legal full name.
    /// </summary>
    [JsonPropertyName("full_name")]
    public string FullName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the primary contact phone number.
    /// </summary>
    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional date of birth (YYYY-MM-DD).
    /// </summary>
    [JsonPropertyName("date_of_birth")]
    public string? DateOfBirth { get; init; }

    /// <summary>
    /// Gets the patient's gender.
    /// </summary>
    [JsonPropertyName("gender")]
    public Gender Gender { get; init; } = Gender.Unspecified;

    /// <summary>
    /// Gets the blood group (e.g. "A+", "O-").
    /// </summary>
    [JsonPropertyName("blood_group")]
    public string? BloodGroup { get; init; }

    /// <summary>
    /// Gets the registered chronic conditions (e.g. "Hypertension", "Diabetes Type 2").
    /// </summary>
    [JsonPropertyName("chronic_conditions")]
    public IReadOnlyList<string> ChronicConditions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets critical drug allergy alerts (e.g. "Penicillin", "Sulfa").
    /// </summary>
    [JsonPropertyName("allergies")]
    public IReadOnlyList<string> Allergies { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the registration timestamp.
    /// </summary>
    [JsonPropertyName("created_at_utc")]
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Safe, non-sensitive patient summary for POS customer selection without leaking private clinical details.
/// </summary>
public sealed record PatientSummary
{
    /// <summary>
    /// Gets the patient identifier.
    /// </summary>
    [JsonPropertyName("patient_id")]
    public Guid PatientId { get; init; }

    /// <summary>
    /// Gets the patient's full name.
    /// </summary>
    [JsonPropertyName("full_name")]
    public string FullName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the primary contact phone number.
    /// </summary>
    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the patient has registered chronic conditions without exposing diagnoses.
    /// </summary>
    [JsonPropertyName("has_chronic_conditions")]
    public bool HasChronicConditions { get; init; }

    /// <summary>
    /// Gets whether the patient has registered allergies without exposing specific allergens.
    /// </summary>
    [JsonPropertyName("has_allergies")]
    public bool HasAllergies { get; init; }
}

/// <summary>
/// Patient's active or recurring prescription medication regimen.
/// </summary>
public sealed record Medication
{
    /// <summary>
    /// Gets the medication regimen ID.
    /// </summary>
    [JsonPropertyName("medication_id")]
    public Guid MedicationId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the associated patient ID.
    /// </summary>
    [JsonPropertyName("patient_id")]
    public Guid PatientId { get; init; }

    /// <summary>
    /// Gets the master catalogue drug ID.
    /// </summary>
    [JsonPropertyName("drug_id")]
    public Guid DrugId { get; init; }

    /// <summary>
    /// Gets the drug trade name.
    /// </summary>
    [JsonPropertyName("drug_name")]
    public string DrugName { get; init; } = string.Empty;

    /// <summary>
    /// Gets dosage instructions (e.g. "1 tablet every 12 hours after meals").
    /// </summary>
    [JsonPropertyName("dosage_instructions")]
    public string DosageInstructions { get; init; } = string.Empty;

    /// <summary>
    /// Gets the prescribing physician's name.
    /// </summary>
    [JsonPropertyName("prescribing_doctor")]
    public string? PrescribingDoctor { get; init; }

    /// <summary>
    /// Gets the start date (YYYY-MM-DD).
    /// </summary>
    [JsonPropertyName("start_date")]
    public string StartDate { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional end date (YYYY-MM-DD).
    /// </summary>
    [JsonPropertyName("end_date")]
    public string? EndDate { get; init; }

    /// <summary>
    /// Gets whether this regimen is currently active.
    /// </summary>
    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// Historical record of a pharmaceutical dispensing event.
/// </summary>
public sealed record MedicationHistoryEntry
{
    /// <summary>
    /// Gets the dispensing event ID.
    /// </summary>
    [JsonPropertyName("dispense_id")]
    public Guid DispenseId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the patient ID.
    /// </summary>
    [JsonPropertyName("patient_id")]
    public Guid PatientId { get; init; }

    /// <summary>
    /// Gets the drug ID.
    /// </summary>
    [JsonPropertyName("drug_id")]
    public Guid DrugId { get; init; }

    /// <summary>
    /// Gets the drug trade name.
    /// </summary>
    [JsonPropertyName("drug_name")]
    public string DrugName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the quantity dispensed.
    /// </summary>
    [JsonPropertyName("quantity_dispensed")]
    public Quantity QuantityDispensed { get; init; }

    /// <summary>
    /// Gets the dispensing pharmacy branch ID.
    /// </summary>
    [JsonPropertyName("pharmacy_id")]
    public Guid PharmacyId { get; init; }

    /// <summary>
    /// Gets the dispensing timestamp.
    /// </summary>
    [JsonPropertyName("dispensed_at_utc")]
    public DateTimeOffset DispensedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Scope of patient consent.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConsentScope
{
    /// <summary>Permission to view previous medication history.</summary>
    ViewMedicationHistory,
    /// <summary>Permission to record a dispensing event.</summary>
    RecordDispense,
    /// <summary>Full clinical access including chronic conditions and allergies.</summary>
    FullClinicalProfile
}

/// <summary>
/// Explicit authorization granted by a patient for sharing or viewing clinical records.
/// </summary>
public sealed record PatientConsent
{
    /// <summary>
    /// Gets the unique consent ID.
    /// </summary>
    [JsonPropertyName("consent_id")]
    public Guid ConsentId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the patient granting consent.
    /// </summary>
    [JsonPropertyName("patient_id")]
    public Guid PatientId { get; init; }

    /// <summary>
    /// Gets the optional authorized chain ID.
    /// </summary>
    [JsonPropertyName("authorized_chain_id")]
    public Guid? AuthorizedChainId { get; init; }

    /// <summary>
    /// Gets the optional authorized pharmacy branch ID.
    /// </summary>
    [JsonPropertyName("authorized_pharmacy_id")]
    public Guid? AuthorizedPharmacyId { get; init; }

    /// <summary>
    /// Gets the granted consent scope.
    /// </summary>
    [JsonPropertyName("granted_scope")]
    public ConsentScope GrantedScope { get; init; }

    /// <summary>
    /// Gets the timestamp when consent was granted.
    /// </summary>
    [JsonPropertyName("granted_at_utc")]
    public DateTimeOffset GrantedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the timestamp when consent expires.
    /// </summary>
    [JsonPropertyName("expires_at_utc")]
    public DateTimeOffset ExpiresAtUtc { get; init; }

    /// <summary>
    /// Gets whether consent has been revoked by the patient.
    /// </summary>
    [JsonPropertyName("is_revoked")]
    public bool IsRevoked { get; init; }
}
