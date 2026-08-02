using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Guvernér: stupně správy za výzkumem a rezerva surovin.
///
/// <para>Obojí řeší tutéž stížnost — automatika, do které hráč nevidí a nemůže
/// jí sáhnout do rukou, je jen ztráta kontroly. Stupně se odemykají
/// (automatizace je odměna, ne přepínač od začátku, viz living-city.md §4)
/// a rezerva říká, do čeho guvernér nesmí sáhnout.</para>
/// </summary>
public class GovernorReserveTests
{
    /// <summary>Obsah se čtyřmi technologiemi guvernéra zdarma — testuje se chování, ne cena.</summary>
    private static GameContent Content()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 500, BaseStorage: 1000) };
        var free = Array.Empty<ResourceAmount>();

        TechDef Tech(string id) => new(id, free, Array.Empty<int>(), Array.Empty<int>());

        var techs = new[]
        {
            Tech(Simulation.GovernorTechId),
            Tech(Simulation.GovernorLevel2TechId),
            Tech(Simulation.GovernorLevel3TechId),
            Tech(Simulation.GovernorReserveTechId),
        };

        var buildings = new[] { TestContent.SimpleBuilding("hut", biomes.Length) };
        return TestContent.Build(biomes, 1, resources, buildings, techs: techs);
    }

    /// <summary>Simulace s vyzkoumanými technologiemi daných indexů.</summary>
    private static Simulation NewSim(params int[] researched)
    {
        var sim = new Simulation(Content(), new UniformTerrain(1));
        foreach (int index in researched)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryResearch(index));
        }

        return sim;
    }

    private const int Governor = 0;
    private const int Level2 = 1;
    private const int Level3 = 2;
    private const int Reserve = 3;

    [Fact]
    public void WithoutResearchTheGovernorDoesNothing()
    {
        var sim = NewSim();

        Assert.False(sim.IsGovernorUnlocked);
        Assert.Equal(0, sim.MaxUnlockedAutoUpgradeLevel);
    }

    [Fact]
    public void EachLevelHasToBeEarned()
    {
        // Kdyby šlo hned na „vše a svižně", přišel by hráč o celou střední hru.
        Assert.Equal(1, NewSim(Governor).MaxUnlockedAutoUpgradeLevel);
        Assert.Equal(2, NewSim(Governor, Level2).MaxUnlockedAutoUpgradeLevel);
        Assert.Equal(
            Simulation.MaxAutoUpgradeLevel,
            NewSim(Governor, Level2, Level3).MaxUnlockedAutoUpgradeLevel);
    }

    [Fact]
    public void ALevelYouHaveNotEarnedCannotBeSet()
    {
        var sim = NewSim(Governor);

        sim.SetAutoUpgradeLevel(Simulation.MaxAutoUpgradeLevel);

        Assert.Equal(1, sim.AutoUpgradeLevel);
    }

    [Fact]
    public void WithoutTheReserveTechThereIsNoReserve()
    {
        var sim = NewSim(Governor);

        sim.SetGovernorReserve(0.5);

        Assert.False(sim.IsGovernorReserveUnlocked);
        Assert.Equal(0.0, sim.GovernorReserve);
    }

    [Fact]
    public void TheReserveIsWhatTheGovernorMustNotTouch()
    {
        var sim = NewSim(Governor, Reserve);

        sim.SetGovernorReserve(0.5);

        Assert.Equal(0.5, sim.GovernorReserve);
    }

    [Fact]
    public void TheReserveNeverGoesAboveNinetyPercent()
    {
        // Stoprocentní rezerva by automatiku umlčela úplně a vypadalo by to
        // jako rozbitá hra, ne jako nastavení.
        var sim = NewSim(Governor, Reserve);

        sim.SetGovernorReserve(5.0);

        Assert.Equal(0.9, sim.GovernorReserve);
    }

    [Fact]
    public void ASavedReserveComesBack()
    {
        var content = Content();
        var sim = new Simulation(content, new UniformTerrain(1));
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(Governor));
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(Reserve));
        sim.SetGovernorReserve(0.25);

        var stream = new MemoryStream();
        new CivDle.Core.Save.SaveGameSerializer().Write(
            stream, sim, new CivDle.Core.Save.SaveMetadata(1, "s", "test", DateTime.UnixEpoch));
        stream.Position = 0;
        var (loaded, _) = new CivDle.Core.Save.SaveGameSerializer().Read(stream, content);

        Assert.Equal(0.25, loaded.GovernorReserve);
    }

    [Fact]
    public void RealContent_HasATechForEveryGovernorStep()
    {
        // Stupně visí na ID z kódu — kdyby se technologie v datech přejmenovala,
        // guvernér by tiše zamrzl na prvním stupni a nikdo by nevěděl proč.
        var techs = TestData.LoadRealContent().Techs;

        foreach (string id in new[]
                 {
                     Simulation.GovernorTechId, Simulation.GovernorLevel2TechId,
                     Simulation.GovernorLevel3TechId, Simulation.GovernorReserveTechId,
                     Simulation.GovernorMergeTechId,
                 })
        {
            Assert.True(techs.TryIndexOf(id, out _), $"v datech chybí technologie '{id}'");
        }
    }
}
