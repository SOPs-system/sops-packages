using System;
using Sops.Payments.Models;
using Xunit;

namespace Sops.Payments.Tests
{
    public class MoneyTests
    {
        [Theory]
        [InlineData(100.50, "EGP", 10050)]
        [InlineData(0.00, "USD", 0)]
        [InlineData(99.99, "EUR", 9999)]
        [InlineData(1.05, "SAR", 105)]
        [InlineData(0.01, "AED", 1)]
        public void FromDecimal_CalculatesCentsAccurately(decimal amount, string currency, long expectedCents)
        {
            var money = Money.FromDecimal(amount, currency);

            Assert.Equal(amount, money.Amount);
            Assert.Equal(currency, money.Currency);
            Assert.Equal(expectedCents, money.Cents);
        }

        [Theory]
        [InlineData(10050, "EGP", 100.50)]
        [InlineData(0, "USD", 0.00)]
        [InlineData(9999, "EUR", 99.99)]
        [InlineData(1, "EGP", 0.01)]
        public void FromCents_CalculatesDecimalAccurately(long cents, string currency, decimal expectedAmount)
        {
            var money = Money.FromCents(cents, currency);

            Assert.Equal(expectedAmount, money.Amount);
            Assert.Equal(currency, money.Currency);
            Assert.Equal(cents, money.Cents);
        }

        [Fact]
        public void NegativeAmount_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => Money.FromDecimal(-1.00m, "EGP"));
            Assert.Throws<ArgumentException>(() => Money.FromCents(-100, "EGP"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("EG")]
        [InlineData("EGPP")]
        public void InvalidCurrency_ThrowsArgumentException(string? invalidCurrency)
        {
            Assert.Throws<ArgumentException>(() => Money.FromDecimal(10.00m, invalidCurrency!));
        }

        [Fact]
        public void Currency_IsNormalizedToUppercase()
        {
            var money = Money.FromDecimal(50.00m, "egp");
            Assert.Equal("EGP", money.Currency);
        }

        [Fact]
        public void Arithmetic_Addition_WorksForSameCurrency()
        {
            var a = Money.FromDecimal(10.25m, "EGP");
            var b = Money.FromDecimal(20.50m, "EGP");

            var result = a + b;

            Assert.Equal(30.75m, result.Amount);
            Assert.Equal(3075, result.Cents);
            Assert.Equal("EGP", result.Currency);
        }

        [Fact]
        public void Arithmetic_Subtraction_WorksForSameCurrency()
        {
            var a = Money.FromDecimal(50.00m, "EGP");
            var b = Money.FromDecimal(20.00m, "EGP");

            var result = a - b;

            Assert.Equal(30.00m, result.Amount);
            Assert.Equal(3000, result.Cents);
        }

        [Fact]
        public void Arithmetic_DifferentCurrencies_ThrowsInvalidOperationException()
        {
            var egp = Money.FromDecimal(10.00m, "EGP");
            var usd = Money.FromDecimal(10.00m, "USD");

            Assert.Throws<InvalidOperationException>(() => egp + usd);
            Assert.Throws<InvalidOperationException>(() => egp - usd);
        }

        [Fact]
        public void Arithmetic_SubtractionResultingInNegative_ThrowsInvalidOperationException()
        {
            var smaller = Money.FromDecimal(5.00m, "EGP");
            var larger = Money.FromDecimal(10.00m, "EGP");

            Assert.Throws<InvalidOperationException>(() => smaller - larger);
        }

        [Fact]
        public void Equality_AndComparisons_WorkExpectedly()
        {
            var m1 = Money.FromDecimal(100.00m, "EGP");
            var m2 = Money.FromCents(10000, "egp");
            var m3 = Money.FromDecimal(150.00m, "EGP");

            Assert.Equal(m1, m2);
            Assert.True(m1 == m2);
            Assert.False(m1 != m2);
            Assert.True(m3 > m1);
            Assert.True(m1 < m3);
            Assert.True(m1 <= m2);
            Assert.True(m1 >= m2);
        }

        [Fact]
        public void ToString_FormatsWithTwoDecimalsAndCurrency()
        {
            var money = Money.FromDecimal(12.5m, "USD");
            Assert.Equal("12.50 USD", money.ToString());
        }
    }
}
