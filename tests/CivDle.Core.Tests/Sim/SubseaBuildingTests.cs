using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Stavba na mořském dně nad opravdovým obsahem.
///
/// <para>Svět je půl na půl: souš vlevo od nuly, moře vpravo. Přesně o tenhle
/// tvar tu jde — celá vrstva stojí na tom, že se od břehu dá dojet jen kousek,
/// a to se na jednolité mapě otestovat nedá.</para>
/// </summary>
public class SubseaBuildingTests
{
    [Fact]
    public void RealContentHasASubseaLayerAndAnchors()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Gameplay.Subsea.IsEnabled, "bez dosahu v datech je celá vrstva mrtvá");
        Assert.Contains(content.Buildings.All, b => b.IsSubsea);
        Assert.Contains(content.Buildings.All, b => b.IsSubseaAnchor);
    }

    [Fact]
    public void AnchorsStandOnLandAndSubseaBuildingsDoNot()
    {
        var content = TestData.LoadRealContent();

        foreach (var def in content.Buildings.All)
        {
            if (def.IsSubseaAnchor)
            {
                Assert.False(def.IsSubsea, $"{def.Id}: kotva nesmí stát na dně");
            }
        }
    }

    [Fact]
    public void WithoutAHarbourTheSeaIsClosed()
    {
        var (sim, content) = Coast();
        int farm = Index(content, "kelp_farm");
        GrantAllTech(sim, content);
        sim.DebugFillStorages();

        Assert.Equal(PlacementResult.NoSubseaLink, sim.CanPlace(farm, 4, 4));
    }

    [Fact]
    public void AHarbourOpensTheWaterAroundIt()
    {
        var (sim, content) = Coast();
        int farm = Index(content, "kelp_farm");
        GrantAllTech(sim, content);
        sim.DebugFillStorages();

        PlaceHarbour(sim, content);

        Assert.Equal(PlacementResult.Ok, sim.CanPlace(farm, 3, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(farm, 3, 0));
    }

    [Fact]
    public void BeyondTheHarboursReachItIsStillClosed()
    {
        var (sim, content) = Coast();
        int farm = Index(content, "kelp_farm");
        GrantAllTech(sim, content);
        sim.DebugFillStorages();

        PlaceHarbour(sim, content);
        int range = content.Gameplay.Subsea.Range;

        Assert.Equal(PlacementResult.NoSubseaLink, sim.CanPlace(farm, range + 5, 0));
    }

    [Fact]
    public void ASubseaBuildingExtendsTheReach()
    {
        // Tohle je ta vlastnost, kvůli které vrstva není jen „kruh kolem přístavu":
        // dá se po ní postupovat dál do moře, dóm po dómu.
        var (sim, content) = Coast();
        int farm = Index(content, "kelp_farm");
        GrantAllTech(sim, content);
        sim.DebugFillStorages();

        PlaceHarbour(sim, content);
        int range = content.Gameplay.Subsea.Range;

        Assert.Equal(PlacementResult.NoSubseaLink, sim.CanPlace(farm, range + 2, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(farm, range - 1, 0));
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(farm, range + 2, 0));
    }

    [Fact]
    public void DemolishingTheHarbourClosesTheSeaAgain()
    {
        var (sim, content) = Coast();
        int farm = Index(content, "kelp_farm");
        GrantAllTech(sim, content);
        sim.DebugFillStorages();

        PlaceHarbour(sim, content);
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(farm, 3, 0));

        Assert.True(sim.TryGetBuildingAt(-2, 0, out int harbour));
        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(harbour));

        Assert.Equal(PlacementResult.NoSubseaLink, sim.CanPlace(farm, 3, 0));
    }

    [Fact]
    public void ALandBuildingStillCannotGoIntoTheSea()
    {
        var (sim, content) = Coast();
        int house = Index(content, "house");
        GrantAllTech(sim, content);
        sim.DebugFillStorages();

        PlaceHarbour(sim, content);

        Assert.Equal(PlacementResult.WrongBiome, sim.CanPlace(house, 3, 0));
    }

    [Fact]
    public void ASubseaBuildingCannotGoOnLand()
    {
        var (sim, content) = Coast();
        int farm = Index(content, "kelp_farm");
        GrantAllTech(sim, content);
        sim.DebugFillStorages();

        PlaceHarbour(sim, content);

        Assert.Equal(PlacementResult.WrongBiome, sim.CanPlace(farm, -6, 0));
    }

    /// <summary>Přístav na břehu: půdorys 2×2 na souši, pravou stranou k vodě.</summary>
    private static void PlaceHarbour(Simulation sim, GameContent content)
    {
        int harbour = Index(content, "harbor");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(harbour, -2, 0));
    }

    /// <summary>
    /// Odemkne celý strom. V pořadí indexů schválně: tech.json je řazený tak,
    /// že předpoklad stojí před tím, co na něm visí.
    /// </summary>
    private static void GrantAllTech(Simulation sim, GameContent content)
    {
        for (int i = 0; i < content.Techs.Count; i++)
        {
            sim.DebugGrantTech(i);
        }
    }

    private static (Simulation Sim, GameContent Content) Coast()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new CoastTerrain(content));
        sim.SkipTutorial();
        return (sim, content);
    }

    private static int Index(GameContent content, string id)
    {
        Assert.True(content.Buildings.TryIndexOf(id, out int index), id);
        return index;
    }

    /// <summary>Souš vlevo od nuly, mělčina vpravo. Rovné pobřeží na ose y.</summary>
    private sealed class CoastTerrain : ITerrain
    {
        private readonly byte _land;
        private readonly byte _sea;

        public CoastTerrain(GameContent content)
        {
            _land = (byte)content.Biomes.IndexOf("grassland");
            _sea = (byte)content.Biomes.IndexOf("shallow_water");
        }

        public byte BiomeAt(int x, int y) => x < 0 ? _land : _sea;
    }
}
