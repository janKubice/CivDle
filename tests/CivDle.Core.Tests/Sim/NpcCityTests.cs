using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Cizí města: nahradila pevný seznam „sousedů".
///
/// <para>Soused byl řádek v tabulce, který nikde nestál. Město je bod na mapě —
/// dá se k němu dojít, postavit cestu, obchodovat, koupit ho, nebo ho obestavět,
/// až sroste s tvým. Testuje se přesně tohle: že poloha plyne ze seedu (na
/// nekonečné mapě se nedá uložit), že se obchod bez spojení nekoná, a že se
/// město dá získat dvěma různými cestami.</para>
/// </summary>
public class NpcCityTests
{
    private static NpcCityCatalog Catalog(int surroundBuildings = 3, int tradeTicks = 5)
    {
        var archetypes = new[]
        {
            new NpcCityArchetype("farmtown", new RgbColor(1, 1, 1), Population: 25,
                Trade: new[] { new ResourceAmount(0, 7) }),
        };

        return new NpcCityCatalog(
            giftCost: new[] { new ResourceAmount(0, 5) },
            giftRelation: 30,
            roadCost: new[] { new ResourceAmount(0, 10) },
            tradeIntervalTicks: tradeTicks,
            buyRelation: 60,
            buyCost: new[] { new ResourceAmount(0, 50) },
            surroundRadius: 6,
            surroundBuildings: surroundBuildings,
            tradeRelation: 2,
            caravanBonusAtFullRelation: 1.0,
            archetypes: new DefRegistry<NpcCityArchetype>(archetypes, a => a.Id, "cizí město"),
            names: new[] { "Testov" });
    }

    private static Simulation NewSim(NpcCityCatalog? catalog = null, double wood = 1000)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: wood, BaseStorage: 100_000) };
        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0, PopulationGrowthPerSecond = 0.0001,
        };

        var content = TestContent.Build(
            biomes, 1, resources, gameplay: gameplay, npcCities: catalog ?? Catalog());
        return new Simulation(content, new UniformTerrain(1));
    }

    /// <summary>První město v dosahu — na nekonečné mapě jich je nekonečně, hledá se od středu.</summary>
    private static NpcCity FirstCity(Simulation sim)
    {
        foreach (var city in sim.CitiesNear(0, 0, NpcCityMap.CellTiles * 3))
        {
            return city;
        }

        throw new InvalidOperationException("v dosahu není žádné cizí město");
    }

    [Fact]
    public void CitiesComeFromTheSeedNotFromStorage()
    {
        // Na nekonečné mapě se polohy uložit nedají — musí být čistou funkcí
        // seedu, stejně jako terén.
        var first = new NpcCityMap(1234, 3, 5);
        var second = new NpcCityMap(1234, 3, 5);
        var other = new NpcCityMap(9999, 3, 5);

        var a = first.CitiesNear(0, 0, 400).Select(c => c.Key).ToList();
        var b = second.CitiesNear(0, 0, 400).Select(c => c.Key).ToList();
        var c = other.CitiesNear(0, 0, 400).Select(x => x.Key).ToList();

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void TheStartHasSomeQuiet()
    {
        // Cizí město hned vedle prvního domku by hráče zavalilo dřív, než pochopí
        // vlastní stavění.
        var map = new NpcCityMap(1234, 3, 5);

        Assert.Empty(map.CitiesNear(0, 0, 40));
    }

    [Fact]
    public void AnUndiscoveredCityStaysUnknown()
    {
        var sim = NewSim();
        var city = FirstCity(sim);

        Assert.False(sim.IsCityDiscovered(city));
    }

    [Fact]
    public void AGiftBuysGoodwill()
    {
        var sim = NewSim();
        var city = FirstCity(sim);

        Assert.Equal(DiplomacyResult.Ok, sim.TryGiftCity(city.Key));

        Assert.Equal(30, sim.NpcStateOf(city.Key).Relation);
    }

    [Fact]
    public void WithoutResourcesThereIsNoGift()
    {
        var sim = NewSim(wood: 0);
        var city = FirstCity(sim);

        Assert.Equal(DiplomacyResult.NotEnoughResources, sim.TryGiftCity(city.Key));
    }

    [Fact]
    public void NoRoadNoTrade()
    {
        // Tohle je důvod, proč silnice v téhle hře nejsou dekorace.
        var sim = NewSim();
        var city = FirstCity(sim);
        sim.Fog.Reveal(city.X, city.Y, 4);
        double before = sim.GetResource(0);

        for (int i = 0; i < 50; i++)
        {
            sim.Tick();
        }

        Assert.False(sim.NpcStateOf(city.Key).RoadLinked);
        Assert.Equal(before, sim.GetResource(0), 3);
    }

    [Fact]
    public void ARoadTurnsOnTheDeliveries()
    {
        var sim = NewSim();
        var city = FirstCity(sim);
        sim.Fog.Reveal(city.X, city.Y, 4);
        Assert.Equal(DiplomacyResult.Ok, sim.TryConnectCity(city.Key));
        double before = sim.GetResource(0);

        for (int i = 0; i < 50; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.GetResource(0) > before);
        Assert.True(sim.NpcStateOf(city.Key).Trades > 0);
    }

    [Fact]
    public void BuyingNeedsThemToLikeYouFirst()
    {
        // Bez téhle podmínky by z diplomacie byl jen nákup.
        var sim = NewSim();
        var city = FirstCity(sim);

        Assert.Equal(DiplomacyResult.RelationTooLow, sim.TryBuyCity(city.Key));

        sim.TryGiftCity(city.Key);
        sim.TryGiftCity(city.Key);
        Assert.Equal(DiplomacyResult.Ok, sim.TryBuyCity(city.Key));
        Assert.True(sim.NpcStateOf(city.Key).Absorbed);
    }

    [Fact]
    public void AJoinedCityBringsItsPeople()
    {
        var sim = NewSim();
        var city = FirstCity(sim);
        double before = sim.Population;
        sim.TryGiftCity(city.Key);
        sim.TryGiftCity(city.Key);

        Assert.Equal(DiplomacyResult.Ok, sim.TryBuyCity(city.Key));

        Assert.True(sim.Population >= before + 25);
        Assert.Equal(1, sim.CitiesJoined);
    }

    [Fact]
    public void BuildingAllAroundSwallowsItWithoutAWord()
    {
        // Nikdo nikoho nedobývá — jen se kolem rozrostlo město a hranice
        // přestala dávat smysl.
        var sim = NewSim(Catalog(surroundBuildings: 3));
        var city = FirstCity(sim);

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, city.X + 2 + i, city.Y + 2));
        }

        for (int i = 0; i < 20; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.NpcStateOf(city.Key).Absorbed);
    }

    [Fact]
    public void RelationsSurviveASave()
    {
        var content = TestContent.Build(
            new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") }, 1,
            new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 1000, BaseStorage: 100_000) },
            npcCities: Catalog());
        var sim = new Simulation(content, new UniformTerrain(1));
        var city = FirstCity(sim);
        sim.TryGiftCity(city.Key);
        sim.TryConnectCity(city.Key);

        var stream = new MemoryStream();
        new CivDle.Core.Save.SaveGameSerializer().Write(
            stream, sim, new CivDle.Core.Save.SaveMetadata(1, "s", "test", DateTime.UnixEpoch));
        stream.Position = 0;
        var (loaded, _) = new CivDle.Core.Save.SaveGameSerializer().Read(stream, content);

        var state = loaded.NpcStateOf(city.Key);
        Assert.Equal(30, state.Relation);
        Assert.True(state.RoadLinked);
    }

    [Fact]
    public void WithoutDataTheMechanicIsOff()
    {
        // Cizí města jsou volitelná — hra bez npc-cities.json musí běžet dál.
        var sim = new Simulation(TestContent.Build(), new UniformTerrain(1));

        Assert.False(sim.NpcCitiesEnabled);
        Assert.Empty(sim.CitiesNear(0, 0, 1000));
        Assert.Equal(DiplomacyResult.Unavailable, sim.TryGiftCity(0));
    }

    [Fact]
    public void ForeignCitiesHaveTheirOwnRoads()
    {
        // Svět má vypadat, že existoval dřív, než tam hráč přišel: cizí města
        // spolu obchodují bez ohledu na něj. Cesty musí být odvozené ze seedu,
        // jinak by se při každém pohledu překreslily jinam.
        var map = new NpcCityMap(1234, 3, 5);

        var first = map.LinksNear(0, 0, 600).Select(l => (l.From.Key, l.To.Key)).ToList();
        var second = map.LinksNear(0, 0, 600).Select(l => (l.From.Key, l.To.Key)).ToList();

        Assert.NotEmpty(first);
        Assert.Equal(first, second);

        // Každá cesta právě jednou — jinak by render kreslil dvojmo.
        Assert.Equal(first.Count, first.Distinct().Count());
    }

    [Fact]
    public void ARoadlessCitySendsNoCaravan()
    {
        // Karavana je vidět důsledek spojení. Bez cesty nemá kdo poslat.
        var sim = NewSim();
        var city = FirstCity(sim);
        sim.Fog.Reveal(city.X, city.Y, 4);

        Assert.False(sim.TryPickTradeCity(out _));

        Assert.Equal(DiplomacyResult.Ok, sim.TryConnectCity(city.Key));
        Assert.True(sim.TryPickTradeCity(out long key));
        Assert.Equal(city.Key, key);
    }

    [Fact]
    public void ADeliveredCaravanWarmsTheRelation()
    {
        var sim = NewSim();
        var city = FirstCity(sim);
        sim.Fog.Reveal(city.X, city.Y, 4);
        sim.TryConnectCity(city.Key);

        int paid = sim.CompleteCaravan(city.Key, resourceIndex: 0, basePayout: 100);

        var state = sim.NpcStateOf(city.Key);
        Assert.Equal(1, state.Trades);
        Assert.Equal(2, state.Relation);       // tradeRelation z katalogu
        Assert.True(paid > 100);               // přátelé platí líp
    }

    [Fact]
    public void RealContent_HasCitiesToMeet()
    {
        var catalog = TestData.LoadRealContent().NpcCities;

        Assert.True(catalog.IsEnabled);
        Assert.True(catalog.Archetypes.Count >= 3);
        Assert.True(catalog.Names.Count >= 10);
    }
}
