using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Opakovatelné technologie (bod 36).
///
/// <para>Drobná vylepšení („+6 % výroby") měla v datech popis „drobné vylepšení,
/// ale sčítá se" — jenže sčítat se nemělo co: každý uzel šel vyzkoumat jednou.
/// Teď má takový uzel <c>maxLevel</c>, bonus se násobí úrovní a cena další
/// úrovně roste mocninou.</para>
/// </summary>
public class RepeatableTechTests
{
    private static Simulation Grass(out GameContent content)
    {
        content = TestData.LoadRealContent();
        return new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
    }

    private static void TopUp(Simulation sim, GameContent content)
    {
        for (int i = 0; i < content.Resources.Count; i++)
        {
            sim.AddResource(i, sim.GetStorageCap(i));
        }
    }

    /// <summary>
    /// Postaví řádku skladišť. Vyšší úrovně stojí mocninou víc, než je základní
    /// strop zásob — bez skladů by je nešlo koupit ani ve hře, ani v testu.
    /// </summary>
    private static void BuildWarehouses(Simulation sim, GameContent content, int count)
    {
        int warehouse = content.Buildings.IndexOf("warehouse");
        for (int i = 0; i < count; i++)
        {
            TopUp(sim, content);
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(warehouse, i * 3, 0));
        }
    }

    /// <summary>Vyzkoumá řetěz prerekvizit až k dané technologii (bez ní samotné).</summary>
    private static void ResearchChain(Simulation sim, GameContent content, params string[] ids)
    {
        foreach (string id in ids)
        {
            TopUp(sim, content);
            Assert.Equal(PlacementResult.Ok, sim.TryResearch(content.Techs.IndexOf(id)));
        }
    }

    [Fact]
    public void RealContentHasRepeatableTechs()
    {
        var content = TestData.LoadRealContent();

        int repeatable = 0;
        for (int i = 0; i < content.Techs.Count; i++)
        {
            if (content.Techs[i].IsRepeatable)
            {
                repeatable++;
            }
        }

        Assert.True(repeatable >= 10, $"opakovatelných výzkumů je jen {repeatable}");
    }

    [Fact]
    public void NoTechStillCarriesTheGenericDescription()
    {
        // Přesně tenhle text si hráč vypsal jako „výzkumy s obecným popiskem".
        var content = TestData.LoadRealContent();
        var loc = new Localization(content.Languages, "cs");

        for (int i = 0; i < content.Techs.Count; i++)
        {
            string desc = loc[content.Techs[i].DescriptionKey];
            Assert.False(desc.Contains("Drobné vylepšení", StringComparison.Ordinal),
                $"technologie '{content.Techs[i].Id}' má pořád obecný popis");
        }
    }

    [Fact]
    public void SecondLevelStacksTheBonus()
    {
        var sim = Grass(out var content);
        int ledgers = content.Techs.IndexOf("ledgers"); // storage_mult, opakovatelný
        ResearchChain(sim, content, "milling", "trade");

        int wood = content.Resources.IndexOf("wood");
        double capBefore = sim.GetStorageCap(wood);

        TopUp(sim, content);
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(ledgers));
        double capLevel1 = sim.GetStorageCap(wood);

        TopUp(sim, content);
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(ledgers));
        double capLevel2 = sim.GetStorageCap(wood);

        Assert.Equal(2, sim.TechLevel(ledgers));
        Assert.True(capLevel1 > capBefore, "první úroveň nezvedla sklad");
        // Druhá úroveň přidá přesně tolik co první — bonus je lineární v úrovni.
        Assert.Equal(capLevel1 - capBefore, capLevel2 - capLevel1, 6);
    }

    [Fact]
    public void EachLevelCostsMore()
    {
        var sim = Grass(out var content);
        int ledgers = content.Techs.IndexOf("ledgers");
        ResearchChain(sim, content, "milling", "trade");
        TopUp(sim, content);

        int firstLevel = sim.ScaledResearchCost(ledgers)[0].Amount;
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(ledgers));
        int secondLevel = sim.ScaledResearchCost(ledgers)[0].Amount;

        Assert.True(secondLevel > firstLevel,
            $"druhá úroveň má stát víc, stojí {secondLevel} vs {firstLevel}");
    }

    [Fact]
    public void MaxedTechCannotBeResearchedAgain()
    {
        var sim = Grass(out var content);
        int ledgers = content.Techs.IndexOf("ledgers");
        BuildWarehouses(sim, content, 12);
        ResearchChain(sim, content, "milling", "trade");

        for (int level = 0; level < content.Techs[ledgers].MaxLevel; level++)
        {
            TopUp(sim, content);
            Assert.Equal(PlacementResult.Ok, sim.TryResearch(ledgers));
        }

        TopUp(sim, content);
        Assert.True(sim.IsTechMaxed(ledgers));
        Assert.Equal(PlacementResult.Occupied, sim.CanResearch(ledgers));
    }

    [Fact]
    public void OneShotTechIsMaxedAfterASingleResearch()
    {
        var sim = Grass(out var content);
        int milling = content.Techs.IndexOf("milling"); // jen odemyká budovu
        TopUp(sim, content);

        Assert.Equal(PlacementResult.Ok, sim.TryResearch(milling));

        Assert.Equal(1, sim.TechLevel(milling));
        Assert.True(sim.IsTechMaxed(milling));
    }

    [Fact]
    public void CountedNodesDoNotGrowWithLevels()
    {
        // Statistika „vyzkoumaných technologií" počítá uzly, ne úrovně — jinak by
        // hráči po pěti úrovních jednoho uzlu tvrdila, že jich má pět.
        var sim = Grass(out var content);
        int ledgers = content.Techs.IndexOf("ledgers");
        BuildWarehouses(sim, content, 12);
        ResearchChain(sim, content, "milling", "trade");
        int nodesBefore = sim.TechsResearched;

        for (int level = 0; level < 3; level++)
        {
            TopUp(sim, content);
            Assert.Equal(PlacementResult.Ok, sim.TryResearch(ledgers));
        }

        Assert.Equal(nodesBefore + 1, sim.TechsResearched);
    }

    [Fact]
    public void LevelsSurviveSaveAndLoad()
    {
        var sim = Grass(out var content);
        int ledgers = content.Techs.IndexOf("ledgers");
        BuildWarehouses(sim, content, 12);
        ResearchChain(sim, content, "milling", "trade");
        for (int level = 0; level < 3; level++)
        {
            TopUp(sim, content);
            Assert.Equal(PlacementResult.Ok, sim.TryResearch(ledgers));
        }

        var metadata = new SaveMetadata(1, "medium", "continents", DateTime.UtcNow);
        using var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, metadata);
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.Equal(3, loaded.TechLevel(ledgers));
        Assert.Equal(sim.GetStorageCap(0), loaded.GetStorageCap(0), 6);
    }
}
