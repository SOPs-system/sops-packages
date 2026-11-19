namespace Sops.Models.Common;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Immutable value object representing a physical inventory count or dosage quantity with a unit of measure.
/// </summary>
public readonly record struct Quantity : IComparable<Quantity>, IEquatable<Quantity>
{
    /// <summary>
    /// Gets the numerical measurement or count.
    /// </summary>
    [JsonPropertyName("value")]
    public decimal Value { get; }

    /// <summary>
    /// Gets the unit of measurement (e.g., "tablets", "boxes", "vials", "ml").
    /// </summary>
    [JsonPropertyName("unit")]
    public string Unit { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Quantity"/> struct.
    /// </summary>
    [JsonConstructor]
    public Quantity(decimal value, string unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new ArgumentException("Quantity unit cannot be null or empty.", nameof(unit));
        }

        Value = value;
        Unit = unit.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Creates a zero quantity for the given unit.
    /// </summary>
    public static Quantity Zero(string unit) => new(0m, unit);

    /// <summary>
    /// Adds two quantities of the same unit.
    /// </summary>
    public static Quantity operator +(Quantity a, Quantity b)
    {
        EnsureSameUnit(a, b);
        return new Quantity(a.Value + b.Value, a.Unit);
    }

    /// <summary>
    /// Subtracts one quantity from another of the same unit.
    /// </summary>
    public static Quantity operator -(Quantity a, Quantity b)
    {
        EnsureSameUnit(a, b);
        return new Quantity(a.Value - b.Value, a.Unit);
    }

    /// <summary>
    /// Compares two quantities of the same unit.
    /// </summary>
    public int CompareTo(Quantity other)
    {
        EnsureSameUnit(this, other);
        return Value.CompareTo(other.Value);
    }

    private static void EnsureSameUnit(Quantity a, Quantity b)
    {
        if (!string.Equals(a.Unit, b.Unit, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cannot perform operations on different units: '{a.Unit}' and '{b.Unit}'.");
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"{Value:G29} {Unit}";
}
