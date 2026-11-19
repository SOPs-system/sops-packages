namespace Sops.Models.Common;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Immutable value object representing a monetary amount in integer minor units with an explicit ISO 4217 currency.
/// Eliminates floating-point rounding errors across financial and point-of-sale calculations.
/// Supports canonical camelCase ('amountInMinorUnits', 'currency', 'decimalString') and legacy snake_case ('amount_minor_units').
/// </summary>
[JsonConverter(typeof(MoneyJsonConverter))]
public readonly record struct Money : IComparable<Money>, IEquatable<Money>
{
    /// <summary>
    /// Gets the monetary amount represented in the currency's smallest subunit (e.g. 100.50 EGP is 10050 piasters).
    /// </summary>
    public long AmountInMinorUnits { get; }

    /// <summary>
    /// Backward-compatible alias for <see cref="AmountInMinorUnits"/>.
    /// </summary>
    [JsonIgnore]
    public long AmountMinorUnits => AmountInMinorUnits;

    /// <summary>
    /// Gets the three-letter ISO 4217 uppercase currency code (e.g. "EGP", "SAR", "USD").
    /// </summary>
    public string Currency { get; }

    /// <summary>
    /// Gets the decimal string representation (e.g. "100.50"), preserving lossless precision across cross-language boundaries.
    /// </summary>
    public string DecimalString => $"{ToDecimal():F2}";

    /// <summary>
    /// Initializes a new instance of the <see cref="Money"/> struct.
    /// </summary>
    /// <param name="amountInMinorUnits">The monetary amount in minor units.</param>
    /// <param name="currency">The three-letter ISO currency code.</param>
    public Money(long amountInMinorUnits, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency code cannot be null or empty.", nameof(currency));
        }

        AmountInMinorUnits = amountInMinorUnits;
        Currency = currency.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Creates a new <see cref="Money"/> instance from minor units.
    /// </summary>
    public static Money FromMinorUnits(long amountInMinorUnits, string currency) =>
        new(amountInMinorUnits, currency);

    /// <summary>
    /// Creates a new <see cref="Money"/> instance from a decimal major currency value (e.g. 10.50 -> 1050 minor units).
    /// </summary>
    public static Money FromDecimal(decimal amount, string currency, int decimalPlaces = 2)
    {
        long factor = 1;
        for (int i = 0; i < decimalPlaces; i++)
        {
            factor *= 10;
        }

        long minorUnits = (long)Math.Round(amount * factor, MidpointRounding.AwayFromZero);
        return new Money(minorUnits, currency);
    }

    /// <summary>
    /// Converts the minor units back to a decimal major value (e.g. 1050 -> 10.50m).
    /// </summary>
    public decimal ToDecimal(int decimalPlaces = 2)
    {
        decimal factor = 1m;
        for (int i = 0; i < decimalPlaces; i++)
        {
            factor *= 10m;
        }

        return AmountInMinorUnits / factor;
    }

    /// <summary>
    /// Gets a zero-amount instance in the specified currency.
    /// </summary>
    public static Money Zero(string currency) => new(0, currency);

    /// <summary>
    /// Adds two money values of the same currency.
    /// </summary>
    public static Money operator +(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.AmountInMinorUnits + b.AmountInMinorUnits, a.Currency);
    }

    /// <summary>
    /// Subtracts one money value from another of the same currency.
    /// </summary>
    public static Money operator -(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.AmountInMinorUnits - b.AmountInMinorUnits, a.Currency);
    }

    /// <summary>
    /// Multiplies a money value by a scalar factor.
    /// </summary>
    public static Money operator *(Money a, long factor) =>
        new(a.AmountInMinorUnits * factor, a.Currency);

    /// <summary>
    /// Multiplies a money value by a scalar factor.
    /// </summary>
    public static Money operator *(long factor, Money a) =>
        new(a.AmountInMinorUnits * factor, a.Currency);

    /// <summary>
    /// Compares two money values of the same currency.
    /// </summary>
    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other);
        return AmountInMinorUnits.CompareTo(other.AmountInMinorUnits);
    }

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (!string.Equals(a.Currency, b.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cannot perform operations on different currencies: '{a.Currency}' and '{b.Currency}'.");
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"{DecimalString} {Currency}";
}

/// <summary>
/// Custom converter supporting canonical camelCase ('amountInMinorUnits', 'currency', 'decimalString')
/// as well as legacy snake_case ('amount_minor_units') payloads.
/// </summary>
public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    /// <inheritdoc />
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token when deserializing Money.");
        }

        long? amount = null;
        string? currency = null;
        string? decimalString = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString()!;
                reader.Read();

                if (string.Equals(propertyName, "amountInMinorUnits", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "amount_minor_units", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "amount", StringComparison.OrdinalIgnoreCase))
                {
                    if (reader.TokenType == JsonTokenType.Number)
                    {
                        amount = reader.GetInt64();
                    }
                    else if (reader.TokenType == JsonTokenType.String && long.TryParse(reader.GetString(), out var parsedLong))
                    {
                        amount = parsedLong;
                    }
                }
                else if (string.Equals(propertyName, "currency", StringComparison.OrdinalIgnoreCase))
                {
                    currency = reader.GetString();
                }
                else if (string.Equals(propertyName, "decimalString", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(propertyName, "decimal_string", StringComparison.OrdinalIgnoreCase))
                {
                    decimalString = reader.GetString();
                }
                else
                {
                    reader.Skip();
                }
            }
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new JsonException("Money object missing required property 'currency'.");
        }

        if (amount.HasValue)
        {
            return new Money(amount.Value, currency!);
        }

        if (!string.IsNullOrWhiteSpace(decimalString) && decimal.TryParse(decimalString, out var parsedDecimal))
        {
            return Money.FromDecimal(parsedDecimal, currency!);
        }

        throw new JsonException("Money object missing required amount property ('amountInMinorUnits' or 'amount_minor_units').");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("amountInMinorUnits", value.AmountInMinorUnits);
        // Include snake_case alias for legacy clients expecting amount_minor_units
        writer.WriteNumber("amount_minor_units", value.AmountInMinorUnits);
        writer.WriteString("currency", value.Currency);
        writer.WriteString("decimalString", value.DecimalString);
        writer.WriteEndObject();
    }
}
