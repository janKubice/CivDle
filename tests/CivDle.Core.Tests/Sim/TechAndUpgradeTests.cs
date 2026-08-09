using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Tech tree a vylepšení budov. Používá skutečná data: základní budovy jsou od
/// startu odemčené, technologiemi hlídané (windmill/market/toolmaker) zamčené;
/// domy jdou vylepšit na chalupu.
/// </summary>
public class TechAndUpgradeTests
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

    // ----- odemykání -----

    [Fact]
    public void BaseBuildingsUnlocked_TechGatedLocked()
    {
        var sim = Grass(out var content);

        Assert.True(sim.IsBuildingBuildable(content.Buildings.IndexOf("house")));
        Assert.True(sim.IsBuildingBuildable(content.Buildings.IndexOf("farm")));
        Assert.False(sim.IsBuildingUnlocked(content.Buildings.IndexOf("windmill")));
        Assert.False(sim.IsBuildingUnlocked(content.Buildings.IndexOf("market")));
    }

    [Fact]
    public void UpgradeTargets_AreBuildableOnceResearched()
    {
        // Vyšší stupně bydlení jde stavět rovnou, ne jen vylepšovat. Ve třetí
        // éře je stavět dům a hned ho vylepšovat na chalupu jen klikání navíc.
        //
        // „Rovnou" ale neznamená „hned": brání tomu výzkum, ne příznak
        // buildable. Bez vyzkoumané technologie se chalupa pořád nepostaví.
        var sim = Grass(out var content);
        int cottage = content.Buildings.IndexOf("cottage");

        Assert.False(sim.IsBuildingUnlocked(cottage), "chalupa má být na začátku zamčená výzkumem");
        Assert.Equal(PlacementResult.NotUnlocked, sim.CanPlace(cottage, 0, 0));
    }

    [Fact]
    public void MergedHouses_StayUpgradeOnly()
    {
        // Sloučené domy jsou naopak výsledek gesta, ne položka v katalogu:
        // postavit blok 2×2 přímo by ze slučování udělalo zbytečnou mechaniku.
        var sim = Grass(out var content);

        Assert.False(sim.IsBuildingBuildable(content.Buildings.IndexOf("manor")));
    }

    [Fact]
    public void LockedBuilding_CannotBePlaced()
    {
        var sim = Grass(out var content);
        TopUp(sim, content);

        Assert.Equal(PlacementResult.NotUnlocked, sim.TryPlaceBuilding(content.Buildings.IndexOf("windmill"), 0, 0));
    }

    // ----- výzkum -----

    [Fact]
    public void Research_UnlocksBuilding_AndDeductsCost()
    {
        var sim = Grass(out var content);
        TopUp(sim, content);
        int milling = content.Techs.IndexOf("milling");
        int wood = content.Resources.IndexOf("wood");
        double woodBefore = sim.GetResource(wood);

        // Cena z dat je základ, ne konečná částka — strhnout se musí přesně to,
        // co hráč vidí v UI (obojí jde přes ResearchCost).
        int charged = sim.ResearchCost(40); // milling: wood 40

        Assert.Equal(PlacementResult.Ok, sim.TryResearch(milling));

        Assert.True(sim.IsTechResearched(milling));
        Assert.True(sim.IsBuildingUnlocked(content.Buildings.IndexOf("windmill")));
        Assert.Equal(woodBefore - charged, sim.GetResource(wood));
    }

    [Fact]
    public void EachFinishedTechMakesTheNextOneDearer()
    {
        // Bez toho měl celý strom prakticky stejnou cenu a druhá půlka hry se
        // proklikala za pár minut.
        var sim = Grass(out var content);
        TopUp(sim, content);

        int before = sim.ResearchCost(100);
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(content.Techs.IndexOf("milling")));
        int after = sim.ResearchCost(100);

        Assert.True(after > before, $"po výzkumu má být dráž, je {after} vs {before}");
    }

    [Fact]
    public void RealContent_ResearchIsNotACheapClickthrough()
    {
        // Ceny v datech jsou základ; násobič je to, co drží strom drahý. Kdyby
        // někdo blok z gameplay.json vyhodil, tenhle test to chytí.
        var research = TestData.LoadRealContent().Gameplay.Research;

        Assert.True(research.CostMultiplier >= 2.0, "strom má stát aspoň dvakrát tolik co základ");
        Assert.True(research.CostGrowthPerTech > 0, "každý další výzkum má stát víc");
    }

    [Fact]
    public void Research_RespectsPrerequisites()
    {
        var sim = Grass(out var content);
        TopUp(sim, content);
        int tools = content.Techs.IndexOf("tools"); // vyžaduje milling

        Assert.Equal(PlacementResult.NotUnlocked, sim.CanResearch(tools));

        Assert.Equal(PlacementResult.Ok, sim.TryResearch(content.Techs.IndexOf("milling")));
        Assert.Equal(PlacementResult.Ok, sim.CanResearch(tools));
    }

    [Fact]
    public void Research_WithoutResources_Fails()
    {
        var sim = Grass(out var content);
        // Bez topování: start má wood 30 < 40 (milling).
        Assert.Equal(PlacementResult.NotEnoughResources, sim.CanResearch(content.Techs.IndexOf("milling")));
    }

    // ----- vylepšení budov -----

    [Fact]
    public void Upgrade_ReplacesBuilding_AndAdjustsBonuses()
    {
        var sim = Grass(out var content);
        int house = content.Buildings.IndexOf("house");
        int cottage = content.Buildings.IndexOf("cottage");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 3, 3));
        int capAfterHouse = sim.HousingCapacity;
        TopUp(sim, content);

        Assert.True(sim.TryGetBuildingAt(3, 3, out int idx));
        Assert.Equal(PlacementResult.Ok, sim.TryUpgradeBuilding(idx));

        Assert.Equal(cottage, sim.Buildings[idx].DefIndex);
        // Dům +4, chalupa +9 → kapacita se zvedne o rozdíl.
        Assert.Equal(capAfterHouse - 4 + 9, sim.HousingCapacity);
    }

    [Fact]
    public void Upgrade_WithoutResources_Fails()
    {
        var sim = Grass(out var content);
        int house = content.Buildings.IndexOf("house");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 3, 3));
        // Po stavbě domu nezbývá dost prken/kamene na upgrade (planks 8 + stone 6).
        Assert.True(sim.TryGetBuildingAt(3, 3, out int idx));

        Assert.Equal(PlacementResult.NotEnoughResources, sim.CanUpgrade(idx));
    }

    [Fact]
    public void Warehouse_HasNoUpgrade()
    {
        var sim = Grass(out var content);
        int warehouse = content.Buildings.IndexOf("warehouse");
        TopUp(sim, content);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(warehouse, 5, 5));

        Assert.True(sim.TryGetBuildingAt(5, 5, out int idx));
        Assert.Equal(PlacementResult.NotUnlocked, sim.CanUpgrade(idx)); // bez další úrovně
    }

    [Fact]
    public void GetBuildingAt_FindsBuildingUnderFootprint()
    {
        var sim = Grass(out var content);
        TopUp(sim, content);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Buildings.IndexOf("farm"), 4, 4)); // 2×2

        Assert.True(sim.TryGetBuildingAt(5, 5, out int idx)); // druhý roh farmy
        Assert.Equal("farm", content.Buildings[sim.Buildings[idx].DefIndex].Id);
        Assert.False(sim.TryGetBuildingAt(20, 20, out _));
    }

    [Fact]
    public void Save_PersistsResearchedTech()
    {
        var sim = Grass(out var content);
        TopUp(sim, content);
        int milling = content.Techs.IndexOf("milling");
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(milling));

        var metadata = new SaveMetadata(1, "medium", "continents", DateTime.UtcNow);
        using var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, metadata);
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.True(loaded.IsTechResearched(milling));
        Assert.True(loaded.IsBuildingUnlocked(content.Buildings.IndexOf("windmill")));
    }

    [Fact]
    public void RealData_HasTechAndCategories()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Techs.Count >= 3);
        Assert.Contains(content.Buildings.All, b => b.Category == "housing");
        Assert.Contains(content.Buildings.All, b => b.Category == "production");
        Assert.Contains(content.Buildings.All, b => b.HasUpgrade);
    }
}
