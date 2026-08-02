using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Mlha války: co hráč neviděl, je tma.
///
/// <para>Testuje se to, co by z mlhy udělalo obtíž místo lákadla: start musí být
/// vidět hned (jinak hráč neví, kde stojí), stavba a ruční sběr musí svět
/// otevírat, Vzestup ji musí vrátit (nový svět = nové objevování) a nesmí se
/// ztratit v savu.</para>
/// </summary>
public class FogOfWarTests
{
    private const int Hut = 0;

    private static Simulation NewSim() =>
        new(TestContent.Build(), new UniformTerrain(1));

    [Fact]
    public void TheStartIsVisibleFromTheFirstSecond()
    {
        // Hráč musí vidět, kde stojí — jinak je první dojem černá obrazovka.
        var sim = NewSim();

        Assert.True(sim.Fog.IsExplored(0, 0));
    }

    [Fact]
    public void TheFarSideOfTheWorldStaysDark()
    {
        var sim = NewSim();

        Assert.False(sim.Fog.IsExplored(5_000, 5_000));
    }

    [Fact]
    public void BuildingSomewhereRevealsIt()
    {
        // Bez tohohle by expanze do tmy neměla žádnou odezvu.
        var sim = NewSim();
        Assert.False(sim.Fog.IsExplored(60, 60));

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Hut, 60, 60));

        Assert.True(sim.Fog.IsExplored(60, 60));
    }

    [Fact]
    public void HandHarvestingOpensTheWorldToo()
    {
        // V rané hře je ruční sběr jediný způsob, jak se dostat dál — kdyby
        // neodhaloval, nešlo by se hnout z místa.
        var sim = NewSim();
        Assert.False(sim.Fog.IsExplored(80, 0));

        sim.TryHarvest(80, 0, out _, out _);

        Assert.True(sim.Fog.IsExplored(80, 0));
    }

    [Fact]
    public void RevealingReportsWhetherAnythingIsNew()
    {
        // Volající z toho pozná, jestli má cenu hlásit „objevil jsi kus světa".
        var fog = new FogOfWar();

        Assert.True(fog.Reveal(500, 500, 4));
        Assert.False(fog.Reveal(500, 500, 4));
    }

    [Fact]
    public void NegativeCoordinatesGetTheirOwnChunks()
    {
        // Mapa jde na obě strany; kdyby se dělilo k nule, slily by se čtverce
        // kolem osy a mlha by se odhalovala „přes okraj".
        var fog = new FogOfWar();

        fog.Reveal(4, 4, 0);

        Assert.True(fog.IsExplored(4, 4));
        Assert.False(fog.IsExplored(-12, -12));
    }

    [Fact]
    public void AscendingPutsTheWorldBackIntoTheDark()
    {
        var content = TestContent.Build(
            prestige: new PrestigeConfig(new GoalCondition(MetricKind.Population, -1, 1), MetricKind.Population, -1, 5));
        var sim = new Simulation(content, new UniformTerrain(1));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Hut, 60, 60));
        Assert.True(sim.Fog.IsExplored(60, 60));

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.False(sim.Fog.IsExplored(60, 60));
        Assert.True(sim.Fog.IsExplored(0, 0)); // ale start je vidět zas
    }

    [Fact]
    public void WhatYouExploredSurvivesASave()
    {
        var content = TestContent.Build();
        var sim = new Simulation(content, new UniformTerrain(1));
        sim.TryHarvest(200, 200, out _, out _);
        Assert.True(sim.Fog.IsExplored(200, 200));

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, new SaveMetadata(1, "s", "test", DateTime.UnixEpoch));
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.True(loaded.Fog.IsExplored(200, 200));
        Assert.False(loaded.Fog.IsExplored(5_000, 5_000));
    }
}
