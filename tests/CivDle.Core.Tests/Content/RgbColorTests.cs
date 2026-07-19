using CivDle.Core.Content;
using Xunit;

namespace CivDle.Core.Tests.Content;

public class RgbColorTests
{
    [Theory]
    [InlineData("#FFAA00", 255, 170, 0)]
    [InlineData("ffaa00", 255, 170, 0)]
    [InlineData("#000000", 0, 0, 0)]
    [InlineData("#FFFFFF", 255, 255, 255)]
    public void Parse_ValidHex_ReturnsColor(string input, byte r, byte g, byte b)
    {
        var color = RgbColor.Parse(input);

        Assert.Equal(new RgbColor(r, g, b), color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#FFF")]
    [InlineData("#GGHHII")]
    [InlineData("#FFAA0000")]
    [InlineData("modrá")]
    public void Parse_InvalidInput_ThrowsWithValueInMessage(string input)
    {
        var ex = Assert.Throws<FormatException>(() => RgbColor.Parse(input));

        Assert.Contains("#RRGGBB", ex.Message);
    }
}
