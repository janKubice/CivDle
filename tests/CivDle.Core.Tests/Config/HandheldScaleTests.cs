using CivDle.Core.Config;
using Xunit;

namespace CivDle.Core.Tests.Config;

/// <summary>
/// Zvětšení rozhraní na malé obrazovce.
///
/// <para>Steam Deck má míň pixelů než monitor, ale drží se sedm palců od očí.
/// Rozhraní laděné pro monitor je na něm nečitelné — a je to ta věc, kterou
/// recenze zmíní jako první.</para>
/// </summary>
public class HandheldScaleTests
{
    [Fact]
    public void ADeckSizedWindowGetsBiggerUi()
    {
        var settings = new GameSettings { UiScale = 1f };

        Assert.True(settings.UiScaleFor(800) > settings.UiScaleFor(1080));
    }

    [Fact]
    public void AMonitorIsLeftAlone()
    {
        var settings = new GameSettings { UiScale = 1f };

        Assert.Equal(1f, settings.UiScaleFor(1080), 3);
        Assert.Equal(1f, settings.UiScaleFor(1440), 3);
        Assert.Equal(1f, settings.UiScaleFor(2160), 3);
    }

    [Fact]
    public void ThePlayersOwnSettingStillCounts()
    {
        // Kdo si UI zmenšil, má ho menší i na Decku — jen ne tak, aby se do
        // toho musel trefovat. Násobí se, nepřebíjí.
        var small = new GameSettings { UiScale = GameSettings.MinUiScale };
        var large = new GameSettings { UiScale = GameSettings.MaxUiScale };

        Assert.True(small.UiScaleFor(800) < large.UiScaleFor(800));
        Assert.True(small.UiScaleFor(800) > small.UiScaleFor(1080));
    }

    [Theory]
    [InlineData(720)]
    [InlineData(800)]
    [InlineData(900)]
    public void EverySmallScreenCounts(int height)
    {
        Assert.True(GameSettings.HandheldBoost(height) > 1f);
    }

    [Fact]
    public void TheBoostIsNotAbsurd()
    {
        // Dvojnásobek by z HUDu udělal obrazovku bez mapy.
        Assert.InRange(GameSettings.HandheldBoost(800), 1.05f, 1.5f);
    }
}
