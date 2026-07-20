using CivDle.Core.WorldGen;
using Xunit;

namespace CivDle.Core.Tests.WorldGen;

public class SeedUtilTests
{
    [Theory]
    [InlineData("42", 42L)]
    [InlineData("-7", -7L)]
    [InlineData("  123456789  ", 123456789L)]
    public void Parse_NumericInput_PassesThrough(string input, long expected)
    {
        Assert.Equal(expected, SeedUtil.Parse(input));
    }

    [Fact]
    public void Parse_Text_IsStableFnv1a64()
    {
        // Známá hodnota FNV-1a 64 pro "abc" — pojistka, že se hash nezmění
        // (stejný textový seed musí dávat stejný svět i v budoucích verzích).
        Assert.Equal(unchecked((long)0xE71FA2190541574BUL), SeedUtil.Parse("abc"));
    }

    [Fact]
    public void Parse_SameText_SameSeed_DifferentText_DifferentSeed()
    {
        Assert.Equal(SeedUtil.Parse("muj svet"), SeedUtil.Parse("muj svet"));
        Assert.NotEqual(SeedUtil.Parse("muj svet"), SeedUtil.Parse("muj svet 2"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyInput_ReturnsRandomPositiveSeed(string? input)
    {
        long seed = SeedUtil.Parse(input);

        Assert.InRange(seed, 1, 999_999_999);
    }
}
