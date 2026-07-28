using CivDle.Screens;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Dojíždějící počítadla: číslo se má plynule dotáčet nahoru, ale utracení
/// surovin musí být vidět hned a velký skok se nesmí táhnout donekonečna.
/// </summary>
public sealed class RollingNumbersTests
{
    [Fact]
    public void SnapTo_StartsAtTheRealValue()
    {
        var rolling = new RollingNumbers(2);

        rolling.SnapTo(i => i == 0 ? 100.0 : 250.0);

        Assert.Equal(100.0, rolling.Shown(0));
        Assert.Equal(250.0, rolling.Shown(1));
    }

    [Fact]
    public void SmallGain_RollsUpGradually()
    {
        var rolling = new RollingNumbers(1);
        rolling.SnapTo(_ => 100.0);

        rolling.Update(1f / 60f, _ => 110.0);

        Assert.True(rolling.Shown(0) > 100.0, "číslo se musí pohnout");
        Assert.True(rolling.Shown(0) < 110.0, "a nesmí skočit rovnou na cíl");
    }

    [Fact]
    public void RollingEventuallyArrives()
    {
        var rolling = new RollingNumbers(1);
        rolling.SnapTo(_ => 100.0);

        for (int i = 0; i < 240; i++)
        {
            rolling.Update(1f / 60f, _ => 110.0);
        }

        Assert.Equal(110.0, rolling.Shown(0), precision: 2);
    }

    [Fact]
    public void Spending_ShowsImmediately()
    {
        // Utracení musí být okamžité — jinak hráč vidí suroviny, které už nemá.
        var rolling = new RollingNumbers(1);
        rolling.SnapTo(_ => 100.0);

        rolling.Update(1f / 60f, _ => 40.0);

        Assert.Equal(40.0, rolling.Shown(0));
    }

    [Fact]
    public void HugeJump_SnapsInsteadOfCrawling()
    {
        // Odměna nebo načtení savu: dojíždět z 10 na milion by trvalo věčnost.
        var rolling = new RollingNumbers(1);
        rolling.SnapTo(_ => 10.0);

        rolling.Update(1f / 60f, _ => 1_000_000.0);

        Assert.Equal(1_000_000.0, rolling.Shown(0));
    }

    [Fact]
    public void Gain_LightsTheFlash_ThenFades()
    {
        var rolling = new RollingNumbers(1);
        rolling.SnapTo(_ => 100.0);

        rolling.Update(1f / 60f, _ => 101.0);
        Assert.True(rolling.Flash(0) > 0f);

        for (int i = 0; i < 120; i++)
        {
            rolling.Update(1f / 60f, _ => 101.0);
        }

        Assert.Equal(0f, rolling.Flash(0));
    }
}
