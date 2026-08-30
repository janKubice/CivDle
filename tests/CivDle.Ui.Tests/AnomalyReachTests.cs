using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Rendering;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Dozví se hráč o anomáliích dřív, než na ně omylem najede?
///
/// <para>Anomálie se na mapě kreslí pulzující značkou — jenže jen tehdy, když
/// je zrovna ve výřezu. Na opravdu generovaném světě leží nejbližší kolem sta
/// dlaždic od startu, tedy dávno za okrajem obrazovky, a bez důvodu se tam
/// nikdo nerozjede. Jediné místo, odkud je vidět dřív, je minimapa — a ta má
/// smysl jen potud, pokud dohlédne dost daleko.</para>
///
/// <para>Test tedy neměří kreslení (na to je potřeba okno), ale ten slib:
/// v dosahu minimapy nějaká anomálie opravdu je.</para>
/// </summary>
public class AnomalyReachTests
{
    /// <summary>Pevná semínka, ať se test nechová pokaždé jinak.</summary>
    private static readonly long[] Seeds = { 20260728, 30313, 4242, 1, 777 };

    [Fact]
    public void EveryWorldHasAnAnomalyWithinSightOfTheMinimap()
    {
        var content = LoadContent();
        var found = new List<PointOfInterest>();

        foreach (long seed in Seeds)
        {
            var sim = NewWorld(content, seed);
            int reach = MinimapRenderer.ReachTiles;

            sim.PointsOfInterest.InRange(
                sim.CityCenterX - reach, sim.CityCenterY - reach,
                sim.CityCenterX + reach, sim.CityCenterY + reach,
                found);

            Assert.True(
                found.Count > 0,
                $"seed {seed}: v dosahu minimapy ({reach} dlaždic) není jediná anomálie");
        }
    }

    [Fact]
    public void AndTheNearestOneIsNotSoFarThatNobodyWalksThere()
    {
        // Sto dlaždic je výlet, tisíc je stěhování. Kdyby se v datech zvětšil
        // kus mapy nebo se zpřísnily biomy, tenhle test to zachytí dřív, než se
        // z výprav stane mrtvá mechanika.
        var content = LoadContent();
        var found = new List<PointOfInterest>();

        foreach (long seed in Seeds)
        {
            var sim = NewWorld(content, seed);
            const int Walkable = 300;

            sim.PointsOfInterest.InRange(
                sim.CityCenterX - Walkable, sim.CityCenterY - Walkable,
                sim.CityCenterX + Walkable, sim.CityCenterY + Walkable,
                found);

            Assert.True(found.Count > 0, $"seed {seed}: do {Walkable} dlaždic od města není žádná anomálie");
        }
    }

    [Fact]
    public void AnAnomalyIsClickableFromTheTileItIsDrawnOn()
    {
        // Tohle je ta past od česlí, jen v jiném kabátě: kdyby se značka
        // kreslila jinam, než kam sahá klik, byla by anomálie vidět a nešla by
        // otevřít.
        var content = LoadContent();
        var sim = NewWorld(content, Seeds[0]);
        var found = new List<PointOfInterest>();
        int reach = MinimapRenderer.ReachTiles;

        sim.PointsOfInterest.InRange(
            sim.CityCenterX - reach, sim.CityCenterY - reach,
            sim.CityCenterX + reach, sim.CityCenterY + reach,
            found);

        Assert.NotEmpty(found);

        foreach (var poi in found)
        {
            Assert.True(
                sim.PointsOfInterest.TryPick(poi.X, poi.Y, out var picked),
                $"anomálie na {poi.X},{poi.Y} se kreslí, ale klik na ni nic nenajde");
            Assert.Equal(poi, picked);
        }
    }

    private static Simulation NewWorld(GameContent content, long seed)
    {
        var preset = content.WorldGen.Presets[content.WorldGen.DefaultPresetIndex];
        var sim = new Simulation(content, new ProceduralTerrain(content.Biomes, preset, seed), seed);
        sim.SkipTutorial();
        return sim;
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
