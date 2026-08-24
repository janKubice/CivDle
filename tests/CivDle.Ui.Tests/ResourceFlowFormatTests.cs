using CivDle.Screens;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Zápis toku surovin v bublině („+45/s", „−32/s").
///
/// <para>Drobnost, která se čte špatně, i když je „skoro správně":
/// vyrovnaná bilance zapsaná jako <c>+0</c> vypadá jako drobný zisk
/// a hráč pak neví, proč mu zásoba nestoupá.</para>
/// </summary>
public class ResourceFlowFormatTests
{
    [Fact]
    public void GainGetsAPlus()
    {
        Assert.StartsWith("+", GameplayScreen.Flow(13));
    }

    [Fact]
    public void LossGetsAMinus()
    {
        Assert.StartsWith("-", GameplayScreen.Flow(-13));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.001)]
    [InlineData(-0.001)]
    public void BalancedFlowHasNoSign(double value)
    {
        // Vyrovnaná bilance se musí číst jako vyrovnaná, ne jako zisk.
        string text = GameplayScreen.Flow(value);

        Assert.DoesNotContain("+", text);
        Assert.DoesNotContain("-", text);
    }

    [Fact]
    public void TheNumberItselfIsNeverNegative()
    {
        // Znaménko nese předpona; "-−32" by bylo dvakrát.
        Assert.Equal("-" + CivDle.Core.Numbers.Format(32), GameplayScreen.Flow(-32));
    }
}
