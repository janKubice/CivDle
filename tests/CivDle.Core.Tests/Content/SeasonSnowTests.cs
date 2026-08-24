using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Sníh na střechách — vlastnost období v datech.
///
/// <para>Barevný nádech přes scénu řekne „je zima" jen tomu, kdo si toho
/// všimne. Bílé střechy to řeknou i tomu, kdo se dívá na jeden dům.</para>
/// </summary>

public class SeasonSnowTests
{
    [Fact]
    public void WinterCarriesSnow()
    {
        var content = TestData.LoadRealContent();
        var winter = content.Seasons.Seasons.First(s => s.Id == "winter");
        Assert.True(winter.SnowCover > 0, $"snowCover = {winter.SnowCover}");
        Assert.True(winter.HasSnow);
    }

    [Fact]
    public void TickingToAPostcardMomentDoesNotLeaveWinter()
    {
        // Zimní záběr nejdřív dotiká na zimu a pak na poledne s jasnem. Když
        // to druhé tiká statisíce tiků, projede zimou skrz a snímek je jarní.
        var content = TestData.LoadRealContent();
        var sim = new CivDle.Core.Sim.Simulation(content, new CivDle.Core.World.UniformTerrain(content.Biomes.IndexOf("grassland")));

        for (int i = 0; i < 500_000 && sim.CurrentSeason?.Id != "winter"; i++)
        {
            sim.Tick();
        }

        Assert.Equal("winter", sim.CurrentSeason?.Id);

        for (int i = 0; i < 500_000; i++)
        {
            double t = sim.TimeOfDay01;
            int weather = sim.CurrentWeatherIndex;
            bool clear = weather < 0 || content.Weather[weather].Id == "clear";
            if (t >= 0.40 && t < 0.58 && clear)
            {
                break;
            }

            sim.Tick();
        }

        Assert.Equal("winter", sim.CurrentSeason?.Id);
    }
}
