using System;
using System.Globalization;

namespace Sops.Payments.Models
{
    /// <summary>
    /// Represents a monetary value with explicit currency and integer minor currency units (cents/piastres).
    /// Prevents floating-point rounding errors and enforces currency safety across payment operations.
    /// </summary>
    public readonly struct Money : IEquatable<Money>, IComparable<Money>
    {
        private const int DecimalPlaces = 2;
        private const decimal Multiplier = 100m;

        /// <summary>
        /// Gets the decimal monetary amount formatted to two decimal places.
        /// </summary>
        public decimal Amount { get; }

        /// <summary>
        /// Gets the three-letter ISO 4217 currency code in uppercase (e.g. "EGP", "USD", "SAR").
        /// </summary>
        public string Currency { get; }

        /// <summary>
        /// Gets the amount represented in minor currency units (e.g. cents, piastres, halalas).
        /// For example, 100.50 EGP equals 10050 cents.
        /// </summary>
        public long Cents { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Money"/> struct.
        /// </summary>
        /// <param name="amount">The decimal amount (must be non-negative).</param>
        /// <param name="currency">The three-letter ISO 4217 currency code.</param>
        /// <exception cref="ArgumentException">Thrown when currency is invalid or amount is negative.</exception>
        public Money(decimal amount, string currency = "EGP")
        {
            if (amount < 0)
            {
                throw new ArgumentException("Monetary amount cannot be negative.", nameof(amount));
            }

            if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            {
                throw new ArgumentException("Currency code must be a valid 3-letter ISO code.", nameof(currency));
            }

            Currency = currency.Trim().ToUpperInvariant();
            Amount = Math.Round(amount, DecimalPlaces, MidpointRounding.AwayFromZero);
            Cents = checked((long)Math.Round(Amount * Multiplier, MidpointRounding.AwayFromZero));
        }

        private Money(decimal amount, string currency, long cents)
        {
            Amount = amount;
            Currency = currency;
            Cents = cents;
        }

        /// <summary>
        /// Creates a <see cref="Money"/> instance from a decimal amount and currency code.
        /// </summary>
        /// <param name="amount">The decimal amount.</param>
        /// <param name="currency">The three-letter ISO currency code. Defaults to "EGP".</param>
        /// <returns>A new <see cref="Money"/> instance.</returns>
        public static Money FromDecimal(decimal amount, string currency = "EGP") => new Money(amount, currency);

        /// <summary>
        /// Creates a <see cref="Money"/> instance from minor currency units (cents/piastres).
        /// </summary>
        /// <param name="cents">The amount in minor units (e.g. 10050 for 100.50).</param>
        /// <param name="currency">The three-letter ISO currency code. Defaults to "EGP".</param>
        /// <returns>A new <see cref="Money"/> instance.</returns>
        public static Money FromCents(long cents, string currency = "EGP")
        {
            if (cents < 0)
            {
                throw new ArgumentException("Cents cannot be negative.", nameof(cents));
            }

            if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            {
                throw new ArgumentException("Currency code must be a valid 3-letter ISO code.", nameof(currency));
            }

            string upperCurrency = currency.Trim().ToUpperInvariant();
            decimal amount = cents / Multiplier;
            return new Money(amount, upperCurrency, cents);
        }

        /// <summary>
        /// Adds two money values of the same currency.
        /// </summary>
        public static Money operator +(Money left, Money right)
        {
            AssertSameCurrency(left, right);
            return FromCents(checked(left.Cents + right.Cents), left.Currency);
        }

        /// <summary>
        /// Subtracts one money value from another of the same currency.
        /// </summary>
        public static Money operator -(Money left, Money right)
        {
            AssertSameCurrency(left, right);
            if (left.Cents < right.Cents)
            {
                throw new InvalidOperationException("Resulting monetary amount cannot be negative.");
            }
            return FromCents(left.Cents - right.Cents, left.Currency);
        }

        /// <summary>
        /// Compares two money values for equality.
        /// </summary>
        public static bool operator ==(Money left, Money right) => left.Equals(right);

        /// <summary>
        /// Compares two money values for inequality.
        /// </summary>
        public static bool operator !=(Money left, Money right) => !left.Equals(right);

        /// <summary>
        /// Determines whether one money value is greater than another.
        /// </summary>
        public static bool operator >(Money left, Money right)
        {
            AssertSameCurrency(left, right);
            return left.Cents > right.Cents;
        }

        /// <summary>
        /// Determines whether one money value is less than another.
        /// </summary>
        public static bool operator <(Money left, Money right)
        {
            AssertSameCurrency(left, right);
            return left.Cents < right.Cents;
        }

        /// <summary>
        /// Determines whether one money value is greater than or equal to another.
        /// </summary>
        public static bool operator >=(Money left, Money right)
        {
            AssertSameCurrency(left, right);
            return left.Cents >= right.Cents;
        }

        /// <summary>
        /// Determines whether one money value is less than or equal to another.
        /// </summary>
        public static bool operator <=(Money left, Money right)
        {
            AssertSameCurrency(left, right);
            return left.Cents <= right.Cents;
        }

        private static void AssertSameCurrency(Money a, Money b)
        {
            if (!string.Equals(a.Currency, b.Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Cannot perform arithmetic on different currencies: '{a.Currency}' and '{b.Currency}'.");
            }
        }

        /// <inheritdoc />
        public bool Equals(Money other) =>
            Cents == other.Cents && string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Money other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Cents.GetHashCode();
                hash = (hash * 397) ^ (Currency != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Currency) : 0);
                return hash;
            }
        }

        /// <inheritdoc />
        public int CompareTo(Money other)
        {
            AssertSameCurrency(this, other);
            return Cents.CompareTo(other.Cents);
        }

        /// <inheritdoc />
        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0:0.00} {1}", Amount, Currency);
    }
}
