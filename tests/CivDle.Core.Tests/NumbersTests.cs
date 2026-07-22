using CivDle.Core;
using Xunit;

namespace CivDle.Core.Tests;

/// <summary>Formátování velkých čísel: krátký zápis se suffixy, malé hodnoty celé.</summary>
public class NumbersTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(42, "42")]
    [InlineData(999, "999")]
    [InlineData(1000, "1.00K")]
    [InlineData(1234, "1.23K")]
    [InlineData(12345, "12.3K")]
    [InlineData(123456, "123K")]
    [InlineData(1_500_000, "1.50M")]
    [InlineData(2_000_000_000, "2.00B")]
    [InlineData(3_500_000_000_000, "3.50T")]
    public void Format_ShortNotation(double value, string expected)
    {
        Assert.Equal(expected, Numbers.Format(value));
    }

    [Fact]
    public void Format_UsesTwoLetterSuffixesBeyondTrillion()
    {
        // 10^15 = aa
        Assert.EndsWith("aa", Numbers.Format(1e15));
        Assert.EndsWith("ab", Numbers.Format(1e18));
    }

    [Fact]
    public void FormatRatio_CombinesBoth()
    {
        Assert.Equal("1.50M/2.00M", Numbers.FormatRatio(1_500_000, 2_000_000));
    }
}
