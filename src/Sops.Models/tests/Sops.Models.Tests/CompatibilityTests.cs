namespace Sops.Models.Tests;

using System;
using Sops.Models.Common;
using Xunit;

public class CompatibilityTests
{
    [Fact]
    public void Money_AdditionAndSubtraction_CalculatesAccurately()
    {
        var m1 = Money.FromMinorUnits(1050, "EGP");
        var m2 = Money.FromMinorUnits(2025, "EGP");

        var sum = m1 + m2;
        Assert.Equal(3075, sum.AmountMinorUnits);
        Assert.Equal(30.75m, sum.ToDecimal());

        var diff = m2 - m1;
        Assert.Equal(975, diff.AmountMinorUnits);
        Assert.Equal(9.75m, diff.ToDecimal());
    }

    [Fact]
    public void Money_CrossCurrencyOperations_ThrowInvalidOperationException()
    {
        var egp = Money.FromMinorUnits(1000, "EGP");
        var usd = Money.FromMinorUnits(1000, "USD");

        Assert.Throws<InvalidOperationException>(() => _ = egp + usd);
        Assert.Throws<InvalidOperationException>(() => _ = egp - usd);
        Assert.Throws<InvalidOperationException>(() => egp.CompareTo(usd));
    }

    [Fact]
    public void Quantity_Arithmetic_CalculatesAccurately()
    {
        var q1 = new Quantity(5.5m, "boxes");
        var q2 = new Quantity(2.5m, "boxes");

        var sum = q1 + q2;
        Assert.Equal(8.0m, sum.Value);
        Assert.Equal("boxes", sum.Unit);

        var diff = q1 - q2;
        Assert.Equal(3.0m, diff.Value);
    }

    [Fact]
    public void Quantity_CrossUnitOperations_ThrowInvalidOperationException()
    {
        var q1 = new Quantity(5, "boxes");
        var q2 = new Quantity(10, "tablets");

        Assert.Throws<InvalidOperationException>(() => _ = q1 + q2);
    }
}
