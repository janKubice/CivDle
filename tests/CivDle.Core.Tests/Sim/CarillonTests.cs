using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Zvonohra: osm tónů, které si hráč složí sám.
///
/// <para>Hlídají se tři věci. Melodie musí <b>přežít save</b> (je to jediná
/// věc v savu, kterou hráč složil, ne vydobyl), zvonit se má <b>jen když je
/// čím</b> (bez postavené zvonohry by se ozývalo z ničeho) a nesmyslný tón
/// z UI má skončit pauzou, ne pádem.</para>
/// </summary>
public class CarillonTests
{
    [Fact]
    public void RealContentHasACarillonWithATune()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Carillon.IsEnabled);
        Assert.Equal("carillon", content.Buildings[content.Carillon.BuildingIndex].Id);
        Assert.NotEmpty(content.Carillon.DefaultTune);
    }

    [Fact]
    public void ANewGameStartsWithTheTuneFromTheData()
    {
        var config = Config(new[] { 0, 2, 4, Carillon.Rest, 4, 2, 0, 1 });
        var carillon = new Carillon(config);

        Assert.Equal(config.DefaultTune, carillon.Notes);
        Assert.False(carillon.IsSilent);
    }

    [Fact]
    public void ANoteOutsideTheScaleBecomesARest_NotACrash()
    {
        // Melodii mění UI a špatné číslo z něj nemá znamenat pád ani němý tón
        // bez vysvětlení.
        var carillon = new Carillon(Config(new[] { 0, 0, 0, 0, 0, 0, 0, 0 }));

        carillon.Set(0, 99);
        carillon.Set(1, -5);
        carillon.Set(99, 3); // mimo melodii — nemá co přepsat

        Assert.Equal(Carillon.Rest, carillon[0]);
        Assert.Equal(Carillon.Rest, carillon[1]);
        Assert.Equal(0, carillon[2]);
    }

    [Fact]
    public void ATuneOfNothingButRestsIsSilent()
    {
        var carillon = new Carillon(Config(Enumerable.Repeat(Carillon.Rest, Carillon.NoteCount).ToArray()));

        Assert.True(carillon.IsSilent);
    }

    [Fact]
    public void RestoringAShorterTuneFillsTheRestWithRests()
    {
        // Data se od uložení mohla změnit; kratší melodie ze savu nesmí nechat
        // v poli staré tóny z výchozí melodie.
        var carillon = new Carillon(Config(new[] { 5, 5, 5, 5, 5, 5, 5, 5 }));

        carillon.Restore(new[] { 1, 2 });

        Assert.Equal(1, carillon[0]);
        Assert.Equal(2, carillon[1]);
        Assert.Equal(Carillon.Rest, carillon[7]);
    }

    [Fact]
    public void WithoutABellTowerAFestivalDoesNotRing()
    {
        var (sim, _) = World();

        Assert.False(sim.HasCarillon);
        Assert.True(sim.TryStartBoost());
        Assert.Equal(0, sim.CarillonRings);
    }

    [Fact]
    public void AFestivalRingsOnceTheBellsStand()
    {
        var (sim, content) = World();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Carillon.BuildingIndex, 4, 4));

        Assert.True(sim.HasCarillon);
        Assert.True(sim.TryStartBoost());
        Assert.Equal(1, sim.CarillonRings);
    }

    [Fact]
    public void ASilentTuneDoesNotRingEvenWithBells()
    {
        // Ticho je legitimní volba hráče — a hlásit ho jako zvonění by znamenalo,
        // že přehrávač hraje prázdnou melodii pokaždé, když se slaví.
        var (sim, content) = World();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Carillon.BuildingIndex, 4, 4));

        for (int i = 0; i < Carillon.NoteCount; i++)
        {
            sim.Carillon.Set(i, Carillon.Rest);
        }

        Assert.True(sim.TryStartBoost());
        Assert.Equal(0, sim.CarillonRings);
    }

    [Fact]
    public void TheTuneSurvivesSaveAndLoad()
    {
        var content = CarillonContent();
        var sim = new Simulation(content, new UniformTerrain(1), seed: 1);
        sim.Carillon.Set(0, 7);
        sim.Carillon.Set(1, Carillon.Rest);
        sim.Carillon.Set(2, 3);

        var serializer = new SaveGameSerializer();
        using var stream = new MemoryStream();
        serializer.Write(stream, sim, new SaveMetadata(1, "s", "test", DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = serializer.Read(stream, content);

        Assert.Equal(7, loaded.Carillon[0]);
        Assert.Equal(Carillon.Rest, loaded.Carillon[1]);
        Assert.Equal(3, loaded.Carillon[2]);
    }

    // ----- pomocné -----

    private static CarillonConfig Config(IReadOnlyList<int> tune)
        => new(BuildingIndex: 0, tune, BaseFrequency: 523.25, NoteSeconds: 0.45);

    private static (Simulation Sim, GameContent Content) World()
    {
        var content = CarillonContent();
        return (new Simulation(content, new UniformTerrain(1), seed: 1), content);
    }

    private static GameContent CarillonContent()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var buildings = new[]
        {
            TestContent.SimpleBuilding("hut", biomes.Length),
            TestContent.SimpleBuilding("carillon", biomes.Length),
        };

        return TestContent.Build(
            biomes: biomes,
            buildings: buildings,
            carillon: new CarillonConfig(
                BuildingIndex: 1,
                new[] { 0, 2, 4, 7, Carillon.Rest, 4, 2, 0 },
                BaseFrequency: 523.25,
                NoteSeconds: 0.45));
    }
}
