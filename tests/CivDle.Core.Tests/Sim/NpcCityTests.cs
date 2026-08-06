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
                Trade: new[] { new ResourceAmount(0, 7) },
                BuildingIndices: new[] { 0 }),
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

    /// <summary>Odtiká simulaci. Cizí města se staví v tiku, ne na požádání z renderu.</summary>
    private static void Tick(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
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
    public void ADiscoveredTownIsBuiltFromTheSameBuildingsAsThePlayers()
    {
        // Hráč si stěžoval, že cizí město nevypadá jako město: byly to barevné
        // obdélníky se svým vlastním kreslením. Teď musí ve světě opravdu STÁT —
        // ze stejných instancí budov a stejných silničních dlaždic jako hráčovy.
        var sim = NewSim();
        var city = FirstCity(sim);

        Tick(sim, 20);
        Assert.Empty(sim.NpcBuildings.ToArray()); // za mlhou se nic nestaví

        sim.Fog.Reveal(city.X, city.Y, 20);
        Tick(sim, 20);

        Assert.NotEmpty(sim.NpcBuildings.ToArray());
        Assert.NotEmpty(sim.NpcRoadTiles);
        Assert.All(sim.NpcBuildings.ToArray(), b => Assert.InRange(b.DefIndex, 0, 0)); // paleta testu má jedinou budovu
        Assert.All(sim.NpcBuildings.ToArray(), b => Assert.True(b.IsComplete)); // město tam stálo dřív než hráč
    }

    [Fact]
    public void ForeignBuildingsBlockThePlayersOwn()
    {
        // Kdyby cizí město nebylo pro umisťování překážka, dalo by se do něj
        // stavět skrz — a byla by to zase jen kulisa.
        var sim = NewSim();
        var city = FirstCity(sim);
        sim.Fog.Reveal(city.X, city.Y, 20);
        Tick(sim, 20);

        var house = sim.NpcBuildings[0];
        Assert.Equal(PlacementResult.Occupied, sim.CanPlace(house.DefIndex, house.X, house.Y));

        var street = sim.NpcRoadTiles[0];
        Assert.Equal(PlacementResult.Occupied, sim.CanBuildRoad(street.X, street.Y));
    }

    [Fact]
    public void TheTownLooksTheSameEveryTime()
    {
        // Plán se neukládá — na nekonečné mapě musí vyjít pokaždé stejně, jinak
        // by se město po každém načtení přestavělo jinak.
        var sim = NewSim();
        var city = FirstCity(sim);
        sim.Fog.Reveal(city.X, city.Y, 20);
        Tick(sim, 20);

        var again = NewSim();
        again.Fog.Reveal(city.X, city.Y, 20);
        Tick(again, 20);

        Assert.Equal(sim.NpcBuildings.Length, again.NpcBuildings.Length);
        Assert.Equal(sim.NpcRoadTiles.Count, again.NpcRoadTiles.Count);
        for (int i = 0; i < sim.NpcBuildings.Length; i++)
        {
            Assert.Equal(sim.NpcBuildings[i].X, again.NpcBuildings[i].X);
            Assert.Equal(sim.NpcBuildings[i].Y, again.NpcBuildings[i].Y);
        }
    }

    [Fact]
    public void ClickingAnywhereInTheTownFindsTheCity()
    {
        // Menu města se otevírá klikem na město — a městem je celá jeho zástavba,
        // ne jeden pixel uprostřed.
        var sim = NewSim();
        var city = FirstCity(sim);
        sim.Fog.Reveal(city.X, city.Y, 20);
        Tick(sim, 20);

        var house = sim.NpcBuildings[0];
        Assert.True(sim.TryNpcCityAt(house.X, house.Y, out var hit));
        Assert.Equal(city.Key, hit.Key);
    }

    [Fact]
    public void JoiningACityHandsOverTheSameBuildingsThatStoodThere()
    {
        // Tohle je celý smysl obestavění: hráč nedostane číslo, ale město.
        // A dostane ho CELÉ — dřív se předávané budovy hnaly přes CanPlace, což
        // znamenalo, že se nepředalo skoro nic (cizí město stojí i z budov, které
        // hráč ještě nemá vyzkoumané) a město prostě zmizelo.
        var sim = NewSim();
        var city = FirstCity(sim);
        sim.Fog.Reveal(city.X, city.Y, 20);
        Tick(sim, 20);

        var stood = sim.NpcBuildings.ToArray();
        Assert.NotEmpty(stood);

        int buildingsBefore = sim.Buildings.Length;
        int roadsBefore = sim.RoadTiles.Count;

        sim.TryGiftCity(city.Key);
        sim.TryGiftCity(city.Key);
        Assert.Equal(DiplomacyResult.Ok, sim.TryBuyCity(city.Key));

        // Budov přesně tolik, kolik jich stálo — ani jedna se cestou neztratila.
        Assert.Equal(buildingsBefore + stood.Length, sim.Buildings.Length);
        Assert.Empty(sim.NpcBuildings.ToArray()); // přestaly být cizí, ne zmizely

        // A stojí tam, kde stály — nepřestavěly se vedle.
        var owned = sim.Buildings.ToArray();
        Assert.All(stood, b => Assert.Contains(owned, o => o.X == b.X && o.Y == b.Y && o.DefIndex == b.DefIndex));

        // Ulice města taky přešly. Cesty MEZI městy ne — ty nepatří ani jednomu
        // z nich a zůstávají v krajině, i když se město přidá k říši.
        Assert.True(sim.RoadTiles.Count > roadsBefore, "ulice připojeného města patří taky hráči");
    }

    [Fact]
    public void AJoinedTownKeepsItsName()
    {
        // Hráč si stěžoval, že se mu po pohlcení „vedle založí další město".
        // Bylo to tím, že sídlo z těch domů dostalo náhodné jméno z klobouku —
        // původní tím zmizelo. Zděděné město si své jméno nechává.
        var sim = NewSim();
        var city = FirstCity(sim);
        sim.Fog.Reveal(city.X, city.Y, 20);
        Tick(sim, 20);

        Assert.Equal(-1, sim.InheritedNameAt(city.X, city.Y)); // dokud je cizí, nic nedědí

        sim.TryGiftCity(city.Key);
        sim.TryGiftCity(city.Key);
        Assert.Equal(DiplomacyResult.Ok, sim.TryBuyCity(city.Key));

        Assert.Equal(city.NameIndex, sim.InheritedNameAt(city.X, city.Y));
        Assert.Equal(-1, sim.InheritedNameAt(city.X + 200, city.Y)); // na druhém konci mapy ne
    }

    [Fact]
    public void ARoadBuiltUpToTheCityLinksItByItself()
    {
        // Hráč postavil silnici až k městu a nestalo se nic — spojení šlo získat
        // jedině tlačítkem v menu. Cesta, která tam fyzicky vede, ho teď naváže
        // sama; tlačítko zůstává jako zkratka pro toho, kdo stavět nechce.
        var sim = NewSim();
        var city = FirstCity(sim);
        sim.Fog.Reveal(city.X, city.Y, 20);
        Tick(sim, 20);

        Assert.False(sim.NpcStateOf(city.Key).RoadLinked);
        Assert.True(sim.TryNpcTownBounds(city.Key, out var bounds));

        // Silnice končící těsně u okraje zástavby.
        for (int y = bounds.MinY; y <= bounds.MaxY; y++)
        {
            sim.TryBuildRoad(bounds.MinX - 2, y);
        }

        Tick(sim, 20);

        Assert.True(sim.NpcStateOf(city.Key).RoadLinked);
    }

    [Fact]
    public void ARoadRunsToTheNeighbourEvenWhenItIsStillInTheFog()
    {
        // Cesty mezi městy se dřív stavěly, jen když hráč znal OBA konce — a to
        // se skoro nestalo, takže je nikdy neviděl. Stačí jeden konec; zbytek
        // schová mlha a z města vede silnice do neznáma.
        var sim = NewSim();
        var city = FirstCity(sim);
        sim.Fog.Reveal(city.X, city.Y, 20);
        Tick(sim, 20);

        Assert.True(sim.TryNpcTownBounds(city.Key, out var bounds));

        // Aspoň jedna cizí silnice musí ležet mimo zástavbu toho města — to je
        // cesta k sousedovi, ne jeho vlastní ulice.
        Assert.Contains(sim.NpcRoadTiles, r =>
            r.X < bounds.MinX - 2 || r.X > bounds.MaxX + 2 || r.Y < bounds.MinY - 2 || r.Y > bounds.MaxY + 2);
    }

    [Fact]
    public void ATownHasItsGrandBuildingsNearTheSquare()
    {
        // Město bez centra vypadá jako náhodně vysypaná hrst budov. Paleta je
        // řazená od nejběžnějšího po nejzvláštnější, takže to výjimečné má stát
        // spíš u náměstí než na kraji pole.
        var archetypes = new[]
        {
            new NpcCityArchetype("farmtown", new RgbColor(1, 1, 1), Population: 150,
                Trade: new[] { new ResourceAmount(0, 7) },
                BuildingIndices: new[] { 0, 0, 1 }), // 0 = běžné, 1 = honosné
        };
        var catalog = new NpcCityCatalog(
            giftCost: new[] { new ResourceAmount(0, 5) }, giftRelation: 30,
            roadCost: new[] { new ResourceAmount(0, 10) }, tradeIntervalTicks: 5,
            buyRelation: 60, buyCost: new[] { new ResourceAmount(0, 50) },
            surroundRadius: 6, surroundBuildings: 99, tradeRelation: 2,
            caravanBonusAtFullRelation: 1.0,
            archetypes: new DefRegistry<NpcCityArchetype>(archetypes, a => a.Id, "cizí město"),
            names: new[] { "Testov" });

        var content = TestContent.Build(
            new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") }, 1,
            new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 1000, BaseStorage: 100_000) },
            buildings: new[] { TestContent.SimpleBuilding("hut", 2), TestContent.SimpleBuilding("temple", 2) },
            npcCities: catalog);

        var sim = new Simulation(content, new UniformTerrain(1));
        var city = FirstCity(sim);
        sim.Fog.Reveal(city.X, city.Y, 25);
        Tick(sim, 20);

        var grand = sim.NpcBuildings.ToArray().Where(b => b.DefIndex == 1).ToList();
        Assert.NotEmpty(grand);

        // Průměrná vzdálenost honosných budov od středu musí být menší než
        // u těch běžných — jinak paleta na poloze nezáleží.
        var plain = sim.NpcBuildings.ToArray().Where(b => b.DefIndex == 0).ToList();
        Assert.NotEmpty(plain);

        double Distance(BuildingInstance b) => Math.Abs(b.X - city.X) + Math.Abs(b.Y - city.Y);
        Assert.True(grand.Average(Distance) < plain.Average(Distance),
            "honosné budovy mají stát blíž náměstí než domky");
    }

    [Fact]
    public void ARazedCityLeavesNoHousesStanding()
    {
        // Zničené město nesmí dál stát — jinak by po meteoritu zbyla nedotčená
        // zástavba bez majitele.
        var meteor = new PrayerDef(
            "meteor", "smite_meteor", BaseCost: 1, BaseChance: 1.0, ChanceFalloff: 0.0,
            Magnitude: 10, RadiusTiles: 12);
        var content = TestContent.Build(
            new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") }, 1,
            new[]
            {
                new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 1000, BaseStorage: 100_000),
                new Resource("faith", new RgbColor(1, 1, 1), StartAmount: 1000, BaseStorage: 10_000),
            },
            npcCities: Catalog(),
            faith: new FaithCatalog(
                1, new DefRegistry<PrayerDef>(new[] { meteor }, p => p.Id, "modlitba")));

        var sim = new Simulation(content, new UniformTerrain(1));
        var city = FirstCity(sim);
        sim.Fog.Reveal(city.X, city.Y, 20);
        Tick(sim, 20);
        Assert.NotEmpty(sim.NpcBuildings.ToArray());

        Assert.Equal(PrayerOutcome.Answered, sim.TryPray(0, 1, city.X, city.Y));

        Assert.Empty(sim.NpcBuildings.ToArray());
        Assert.True(sim.NpcStateOf(city.Key).Destroyed);
    }

    [Fact]
    public void RealContent_CitiesAreBuiltFromRealBuildings()
    {
        // Kdyby někdo paletu z dat vyhodil, města by byla zase prázdná.
        var catalog = TestData.LoadRealContent().NpcCities;

        Assert.All(catalog.Archetypes.All, a =>
            Assert.True(a.BuildingIndices.Count > 0, $"druh '{a.Id}' nemá z čeho stavět"));
    }

    [Fact]
    public void RealContent_CitiesShareNamesWithThePlayersSettlements()
    {
        // Vlastní seznam jmen dělal z cizích měst jiný svět.
        var content = TestData.LoadRealContent();

        Assert.Equal(content.SettlementNames, content.NpcCities.Names);
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
