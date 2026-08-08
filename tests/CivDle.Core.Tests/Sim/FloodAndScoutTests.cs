using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Povodeň a viditelnost změn terénu — dvě věci, které navenek „nedělaly nic".
///
/// <para>Povodeň zaplavovala jen dlaždice, které už sousedily s vodou, takže
/// mimo pobřeží neudělala doslova nic a hráč nevěděl proč. A i tam, kde něco
/// udělala, to nebylo vidět: render kreslil z upečených chunků a o přepisech
/// terénu nevěděl.</para>
/// </summary>
public class FloodAndScoutTests
{
    /// <summary>Pobřeží: vlevo od hranice voda, vpravo souš.</summary>
    private sealed class CoastTerrain : ITerrain
    {
        private readonly int _shoreX;

        public CoastTerrain(int shoreX) => _shoreX = shoreX;

        public byte BiomeAt(int x, int y) => (byte)(x < _shoreX ? 0 : 1);

        public float HeightAt(int x, int y) => 0.5f;
    }

    /// <summary>Obsah s jedinou modlitbou — povodní, která vždycky vyjde.</summary>
    private static GameContent Content()
    {
        var flood = new PrayerDef(
            "flood", "smite_flood", BaseCost: 1, BaseChance: 1.0, ChanceFalloff: 0.0,
            Magnitude: 10, RadiusTiles: 8);

        // Povodeň potřebuje biom „shallow_water" — bez něj nemá čím zaplavit
        // a tiše se vzdá. Ostrá data ho mají, testovací obsah si ho musí přidat.
        return TestContent.Build(
            new[]
            {
                TestContent.WaterBiome(),
                TestContent.LandBiome("grass"),
                TestContent.WaterBiome("shallow_water"),
            },
            1,
            new[]
            {
                new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 100, BaseStorage: 1000),
                new Resource("faith", new RgbColor(1, 1, 1), StartAmount: 100, BaseStorage: 1000),
            },
            faith: new FaithCatalog(
                1, new DefRegistry<PrayerDef>(new[] { flood }, p => p.Id, "modlitba")));
    }

    [Fact]
    public void FloodReachesInlandFromTheShore()
    {
        var sim = new Simulation(Content(), new CoastTerrain(shoreX: 20));
        int before = sim.TerrainRevision;

        Assert.Equal(PrayerOutcome.Answered, sim.TryPray(0, 1, 24, 10));

        Assert.True(sim.TerrainRevision > before, "povodeň nezměnila terén");
        Assert.True(sim.IsWaterAt(21, 10), "dlaždice hned za břehem měla zůstat pod vodou");
    }

    [Fact]
    public void FloodFarFromWaterChangesNothing()
    {
        // Suchá pevnina bez kapky vody: není co vylít. Hra to hráči řekne
        // hláškou — tichá modlitba, která spolkla víru, je horší.
        var sim = new Simulation(Content(), new CoastTerrain(shoreX: -5000));
        int before = sim.TerrainRevision;

        Assert.Equal(PrayerOutcome.Answered, sim.TryPray(0, 1, 100, 100));

        Assert.Equal(before, sim.TerrainRevision);
    }

    [Fact]
    public void TerrainChangesAreVisibleToTheRenderer()
    {
        // Render čte přepisy a podle revize pozná, že musí přepéct chunky.
        // Bez obojího byla terraformace neviditelná.
        var sim = new Simulation(Content(), new CoastTerrain(shoreX: 20));
        int before = sim.TerrainRevision;

        Assert.Equal(PrayerOutcome.Answered, sim.TryPray(0, 1, 24, 10));

        Assert.True(sim.TerrainRevision > before);
        Assert.NotEmpty(sim.BiomeOverrideMap);
    }
}
