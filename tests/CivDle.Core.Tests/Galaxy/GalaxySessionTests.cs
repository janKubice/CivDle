using CivDle.Core.Content;
using CivDle.Core.Galaxy;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Galaxy;

/// <summary>
/// Galaxie za běhu (svety-design.md 2.2–2.4): kolonizační loď, přistání
/// a přepínání světů.
///
/// <para>Hlídá se, na čem by to hráče bolelo: loď bere jen přebytky, kolonie
/// přistane s výbavou a znalostmi, Domovina za nepřítomnosti dál vyrábí
/// a přepnutí ani save uprostřed nic neztratí.</para>
/// </summary>
public class GalaxySessionTests
{
    private const int Wood = 0;

    [Fact]
    public void WithoutTheGateNoShipCanBeBuilt()
    {
        var session = NewSession(gateOpen: false);

        Assert.Equal(ShipBlocker.GalaxyClosed, session.CanStartShip("dune"));
    }

    [Fact]
    public void TheShipIsPaidStageByStageFromHomeSurplus()
    {
        var session = NewSession();
        session.Active.DebugSetResource(Wood, 150);
        session.StartShip("dune");

        Assert.Equal(ShipBlocker.ShipInProgress, session.CanStartShip("dune"));
        Assert.Equal(100, session.InvestInShip(), 6);   // první stupeň celý
        Assert.Equal(50, session.InvestInShip(), 6);    // druhý z půlky
        Assert.False(session.IsShipReady);
        Assert.Equal(50, session.ShipInvested(Wood), 6);

        session.Active.AddResource(Wood, 1_000);
        session.InvestInShip();

        Assert.True(session.IsShipReady);
        Assert.Equal(0, session.InvestInShip()); // hotová loď už nic nebere
    }

    [Fact]
    public void TheDebugShipIsReadyForTheFirstOpenWorldAndOnlyBehindTheGate()
    {
        // Páka ladicího menu: bez brány nic, s ní hotová loď k prvnímu
        // dostupnému světu — a kolonie se pak zakládá stejnou cestou jako ve hře.
        Assert.Null(NewSession(gateOpen: false).DebugReadyShip());

        var session = NewSession();
        var world = session.DebugReadyShip();

        Assert.Equal("dune", world?.Id);
        Assert.True(session.IsShipReady);
        var site = session.LandingSites("dune")[0];
        Assert.Equal("dune", session.Colonize(site.X, site.Y).Content.World.Id);
        Assert.Null(session.DebugReadyShip()); // z kolonie ne (a Duna už kolonií je)
    }

    [Fact]
    public void TheDebugShipFinishesAShipAlreadyUnderway()
    {
        var session = NewSession();
        session.Active.DebugSetResource(Wood, 150);
        session.StartShip("dune");
        session.InvestInShip();
        session.InvestInShip(); // druhý stupeň z půlky
        Assert.False(session.IsShipReady);

        Assert.Equal("dune", session.DebugReadyShip()?.Id);

        Assert.True(session.IsShipReady);
        Assert.Equal(0, session.ShipInvested(Wood));
    }

    [Fact]
    public void DebugOpenWorldsIgnoreTheStarsButNotTheGateAndAreNotSaved()
    {
        var state = NewSession(gateOpen: false).State;
        var far = new WorldDef("xeno", 6, 12, false, Array.Empty<ProjectStage>(), 1, "xeno", Planet());
        state.DebugAllWorldsOpen = true;

        Assert.Equal(WorldAvailability.Locked, state.AvailabilityOf(far)); // bez brány galaxie není
        state.GateOpened = true;
        Assert.Equal(WorldAvailability.Available, state.AvailabilityOf(far));

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            GalaxyCodec.Write(writer, state);
        }

        stream.Position = 0;
        var loaded = GalaxyCodec.Read(new BinaryReader(stream));
        Assert.False(loaded.DebugAllWorldsOpen);
        Assert.Equal(WorldAvailability.Locked, loaded.AvailabilityOf(far)); // po načtení zase hvězdy
    }

    [Fact]
    public void TheGalaxyEpilogueSummaryListsEveryWorldAndOnlyReads()
    {
        var session = NewSession();
        BuildShip(session);
        var site = session.LandingSites("dune")[0];
        var colony = session.Colonize(site.X, site.Y);
        var homeSnapshot = session.State.Records[WorldScope.HomeId].Snapshot!.ToArray();
        long tick = colony.TickCount;

        var summary = GalaxyEndingSummary.Of(session);

        Assert.Equal(new[] { WorldScope.HomeId, "dune" }, summary.Worlds.Select(w => w.WorldId));
        Assert.Same(colony, session.Active);
        Assert.Equal(tick, colony.TickCount);
        Assert.Equal(homeSnapshot, session.State.Records[WorldScope.HomeId].Snapshot);
        Assert.Equal(summary.Worlds.Sum(w => w.Summary.Buildings), summary.Buildings);
        Assert.True(summary.Worlds[1].Summary.Buildings >= 1); // přistávací modul
    }

    [Fact]
    public void ColonizingLandsTheModuleWithTheCargoAndTheKnowledge()
    {
        var session = NewSession();
        session.Active.DebugGrantTech(0); // sdílená technologie
        BuildShip(session);
        var home = session.Active;
        var site = session.LandingSites("dune")[0];

        var colony = session.Colonize(site.X, site.Y);

        Assert.Same(colony, session.Active);
        Assert.Equal("dune", colony.Content.World.Id);
        Assert.Equal("dune", session.State.ActiveWorldId);
        Assert.Null(session.State.Ship);
        Assert.True(colony.Buildings[0].IsComplete);
        Assert.Equal((site.X, site.Y), (colony.Buildings[0].X, colony.Buildings[0].Y));
        Assert.Equal(60, colony.GetResource(colony.Content.Resources.IndexOf("adobe")), 6); // 10 start + 50 výbava
        Assert.True(colony.IsTechResearched(colony.Content.Techs.IndexOf("storage")));
        Assert.NotNull(session.State.Records[WorldScope.HomeId].Snapshot);
        Assert.NotNull(session.State.Records[WorldScope.HomeId].Summary);
        Assert.Equal(1, session.State.ColonyCount);
        Assert.NotSame(home, session.Active);
    }

    [Fact]
    public void TheLandingSitesAreOnGroundTheModuleFitsAndFarApart()
    {
        var session = NewSession();

        var sites = session.LandingSites("dune");

        Assert.NotEmpty(sites);
        var colonyContent = session.Contents.For("dune");
        var terrain = WorldTerrain.Create(
            colonyContent, colonyContent.WorldGen.Presets[colonyContent.World.PresetIndex], session.ColonySeed("dune"));
        foreach (var (x, y) in sites)
        {
            Assert.True(colonyContent.Buildings[0].IsBiomeAllowed(terrain.BiomeAt(x, y)));
        }

        for (int i = 0; i < sites.Count; i++)
        {
            for (int j = i + 1; j < sites.Count; j++)
            {
                Assert.True(Math.Abs(sites[i].X - sites[j].X) + Math.Abs(sites[i].Y - sites[j].Y) >= 48);
            }
        }

        Assert.Equal(sites, session.LandingSites("dune")); // deterministické
    }

    [Fact]
    public void HomeKeepsProducingWhileThePlayerIsAway()
    {
        var session = NewSession();
        Workshop(session.Active);
        RunWithMeter(session, 700); // minuta a kus: měřič ví, co dílna dělá
        BuildShip(session);
        double woodAtLeaving = session.Active.GetResource(Wood);
        long homeTicks = session.Active.TickCount;
        var site = session.LandingSites("dune")[0];
        session.Colonize(site.X, site.Y);

        RunWithMeter(session, 1_200); // dvě minuty na kolonii

        var entry = session.SwitchTo(WorldScope.HomeId);
        var home = entry.Simulation;

        Assert.Null(entry.CatchUp); // krátká nepřítomnost: souhrn, žádné dohánění
        Assert.Same(home, session.Active);
        Assert.Equal(WorldScope.HomeId, session.State.ActiveWorldId);
        Assert.Equal(homeTicks + GalaxySession.WarmUpTicks, home.TickCount); // svět za nepřítomnosti netikal
        double expected = woodAtLeaving + 120 * 1.2; // dílna dělá 3 dřeva za 25 tiků = 1,2/s
        Assert.InRange(home.GetResource(Wood), expected * 0.9, expected * 1.15);
    }

    [Fact]
    public void ALongAbsenceIsCaughtUpForReal()
    {
        var session = NewSession();
        Workshop(session.Active);
        RunWithMeter(session, 200);
        BuildShip(session);
        var site = session.LandingSites("dune")[0];
        session.Colonize(site.X, site.Y);

        // Hodina na kolonii — hodiny se posunou, aniž by se musela tikat.
        session.State.ActiveEnteredAtSeconds += 3_600;
        var entry = session.SwitchTo(WorldScope.HomeId);

        Assert.NotNull(entry.CatchUp);
        double before = entry.Simulation.GetResource(Wood);
        while (!entry.CatchUp!.IsDone)
        {
            entry.CatchUp.Advance(50_000);
        }

        entry.Complete();

        Assert.True(entry.Simulation.GetResource(Wood) > before + 1_000, "za hodinu má dílna něco vyrobit");
        Assert.Equal(WorldScope.HomeId, session.State.ActiveWorldId);
    }

    [Fact]
    public void GoodsDeliveredWhileAwayArriveOnReturn()
    {
        var session = NewSession();
        BuildShip(session);
        var site = session.LandingSites("dune")[0];
        session.Colonize(site.X, site.Y);
        session.State.Records[WorldScope.HomeId].PendingDelta["wood"] = 77;
        double before = session.State.Records[WorldScope.HomeId].Summary!.StockOf("wood");

        var home = session.SwitchTo(WorldScope.HomeId).Simulation;

        Assert.InRange(home.GetResource(Wood), before + 77 - 1, before + 77 + 5);
        Assert.Empty(session.State.Records[WorldScope.HomeId].PendingDelta);
    }

    [Fact]
    public void TheLegacyTravelsWithThePlayer()
    {
        var session = NewSession();
        session.Active.DebugGrantLegacyPoints(42);
        BuildShip(session);
        var site = session.LandingSites("dune")[0];

        var colony = session.Colonize(site.X, site.Y);
        Assert.Equal(42, colony.LegacyPoints);

        colony.DebugGrantLegacyPoints(8);
        var home = session.SwitchTo(WorldScope.HomeId).Simulation;

        Assert.Equal(50, home.LegacyPoints); // body z kolonie platí i doma
    }

    [Fact]
    public void EachColonyMakesTheNextShipDearer()
    {
        var session = NewSession();
        var dune = session.Catalog.Find("dune")!;
        Assert.Equal(100, session.ShipStageCost(dune, 0).Single().Amount);

        BuildShip(session);
        var site = session.LandingSites("dune")[0];
        session.Colonize(site.X, site.Y);

        Assert.Equal(200, session.ShipStageCost(dune, 0).Single().Amount); // růst ×2 za kolonii
    }

    [Fact]
    public void AGalaxySavedOnAColonyLoadsBackAndSwitchesHome()
    {
        var session = NewSession();
        Workshop(session.Active);
        BuildShip(session);
        var site = session.LandingSites("dune")[0];
        session.Colonize(site.X, site.Y);
        RunWithMeter(session, 100);

        var stream = new MemoryStream();
        var colony = session.Active;
        new SaveGameSerializer().Write(stream, colony, new SaveMetadata(colony.Seed, "s", "test", DateTime.UtcNow), session.State);
        stream.Position = 0;
        var loaded = new SaveGameSerializer().Read(stream, session.Contents);
        var resumed = GalaxySession.Resume(session.Contents, loaded);

        Assert.Equal("dune", resumed.Active.Content.World.Id);
        Assert.Equal((site.X, site.Y), (resumed.Active.LandingX, resumed.Active.LandingY));
        var home = resumed.SwitchTo(WorldScope.HomeId).Simulation;
        Assert.True(home.Content.World.IsHome);
        Assert.Contains(Enumerable.Range(0, home.Buildings.Length), i => home.Buildings[i].DefIndex == 1); // dílna přežila cestu savem
    }

    [Fact]
    public void AColonysAscensionLandsTheModuleAgain()
    {
        var session = NewSession();
        BuildShip(session);
        var site = session.LandingSites("dune")[0];
        var colony = session.Colonize(site.X, site.Y);
        for (int i = 0; i < 10; i++)
        {
            colony.Tick();
        }

        Assert.Equal(PlacementResult.Ok, colony.TryAscend());

        Assert.Contains(Enumerable.Range(0, colony.Buildings.Length), i => colony.Buildings[i].DefIndex == 0);
    }

    // ----- obchod -----

    private const int Food = 1;

    [Fact]
    public void ARouteNeedsAnExportTheDestinationKnows()
    {
        var session = ColonyWithTrade();

        Assert.Equal(TradeBlocker.None, session.CanOpenRoute(WorldScope.HomeId, "dune", "food"));
        Assert.Equal(TradeBlocker.NotExported, session.CanOpenRoute(WorldScope.HomeId, "dune", "wood"));
        Assert.Equal(TradeBlocker.UnknownAtDestination, session.CanOpenRoute("dune", WorldScope.HomeId, "adobe"));
        Assert.Equal(TradeBlocker.SameWorld, session.CanOpenRoute("dune", "dune", "adobe"));
        Assert.Equal(TradeBlocker.NotColony, session.CanOpenRoute(WorldScope.HomeId, "frost", "food"));

        session.OpenRoute(WorldScope.HomeId, "dune", "food");
        Assert.Equal(TradeBlocker.Duplicate, session.CanOpenRoute(WorldScope.HomeId, "dune", "food"));
    }

    [Fact]
    public void HomeShipsToTheColonyWhileThePlayerIsThere()
    {
        // Hráč je na Duně, Domovina běží souhrnně: jídlo odtéká z jejího
        // souhrnu (nevyrovnaný obchod) a v kolonii přibývá jako dovoz.
        var session = ColonyWithTrade();
        var route = session.OpenRoute(WorldScope.HomeId, "dune", "food");

        RunWithMeter(session, 600); // minuta: cesta 20 s, dávky plují

        var home = session.State.Records[WorldScope.HomeId];
        Assert.True(route.TotalShipped > 50, $"odplulo jen {route.TotalShipped}");
        Assert.Equal(-route.TotalShipped, home.PendingDelta["food"], 6);
        Assert.True(session.Active.Ledger.ImportedPerSecond(Food) > 0, "kolonie má vidět dovoz");
    }

    [Fact]
    public void WhatHomeSentIsMissingThereOnReturn()
    {
        var session = ColonyWithTrade();
        var route = session.OpenRoute(WorldScope.HomeId, "dune", "food");
        RunWithMeter(session, 300);
        double stockAway = session.State.Records[WorldScope.HomeId].EstimatedStock("food", session.NowSeconds);

        var home = session.SwitchTo(WorldScope.HomeId).Simulation;

        Assert.True(route.TotalShipped > 0);
        Assert.InRange(home.GetResource(Food), stockAway - 1, stockAway + 5); // + chvíle dotikání
        Assert.Empty(session.State.Records[WorldScope.HomeId].PendingDelta);
    }

    [Fact]
    public void RoutesAndCargoSurviveASave()
    {
        var session = ColonyWithTrade();
        var route = session.OpenRoute(WorldScope.HomeId, "dune", "food");
        RunWithMeter(session, 100);
        double inTransit = session.State.Trade.InTransitOn(route.Id);
        Assert.True(inTransit > 0);

        var stream = new MemoryStream();
        var colony = session.Active;
        new SaveGameSerializer().Write(stream, colony, new SaveMetadata(colony.Seed, "s", "test", DateTime.UtcNow), session.State);
        stream.Position = 0;
        var resumed = GalaxySession.Resume(session.Contents, new SaveGameSerializer().Read(stream, session.Contents));

        var loaded = resumed.State.Trade.Routes.Single();
        Assert.Equal((route.Id, "food", route.TotalShipped), (loaded.Id, loaded.ResourceId, loaded.TotalShipped));
        Assert.Equal(inTransit, resumed.State.Trade.InTransitOn(loaded.Id), 6);
        Assert.Equal(session.State.Records[WorldScope.HomeId].PortCapacity, resumed.State.Records[WorldScope.HomeId].PortCapacity);
        Assert.Equal(session.State.Trade.LastAdvancedAt, resumed.State.Trade.LastAdvancedAt);
    }

    /// <summary>Domovina s přístavem a vývozem jídla, Duna založená a aktivní.</summary>
    private static GalaxySession ColonyWithTrade()
    {
        var session = NewSession(trade: true);
        Assert.Equal(PlacementResult.Ok, session.Active.TryPlaceBuildingFree(2, 10, 10)); // přístav
        session.Active.DebugCompleteConstruction();
        session.Active.DebugSetResource(Food, 800);
        BuildShip(session);
        var site = session.LandingSites("dune")[0];
        session.Colonize(site.X, site.Y);
        return session;
    }

    // ----- galaxie -----

    private static GalaxySession NewSession(bool gateOpen = true, bool trade = false)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var mask = new[] { false, true };
        var tech = new TechDef("storage", Array.Empty<ResourceAmount>(), Array.Empty<int>(), Array.Empty<int>());
        var gameplay = TestContent.DefaultGameplay with { FoodPerPersonPerSecond = 0 };
        var prestige = new PrestigeConfig(new GoalCondition(MetricKind.Population, -1, 1), MetricKind.Population, -1, 1);

        var homeResources = new[]
        {
            new Resource("wood", new RgbColor(140, 90, 40), 0, BaseStorage: 1_000_000),
            new Resource("food", new RgbColor(200, 180, 60), 10, BaseStorage: 1_000),
        };
        var kiln = TestContent.SimpleBuilding("kiln", biomes.Length) with
        {
            BuildCost = Array.Empty<ResourceAmount>(),
            Recipe = new Recipe(Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(Wood, 3) }, 25),
        };
        var worlds = new WorldCatalog(new[]
        {
            new WorldDef(WorldScope.HomeId, 0, 0, false, Array.Empty<ProjectStage>(), 1, "home", Planet()),
            new WorldDef("dune", 1, 0, true,
                new[] { new ProjectStage(new[] { new ResourceAmount(Wood, 100) }), new ProjectStage(new[] { new ResourceAmount(Wood, 100) }) },
                2, "dune", Planet()),
        }, new TradeConfig(TravelSecondsPerStep: 20, DispatchSeconds: 5));
        var port = TestContent.SimpleBuilding("port", biomes.Length) with
        {
            BuildCost = Array.Empty<ResourceAmount>(),
            TradeCapacity = 5,
        };
        var homeBuildings = trade
            ? new[] { TestContent.SimpleBuilding("hut", biomes.Length, housing: 10), kiln, port }
            : new[] { TestContent.SimpleBuilding("hut", biomes.Length, housing: 10), kiln };
        var home = TestContent.Build(biomes, 1, homeResources, homeBuildings,
            gameplay, techs: new[] { tech }, prestige: prestige).WithGalaxy(worlds);
        if (trade)
        {
            home = home.WithWorld(WorldProfile.Home with { ExportIndices = new[] { Food } });
        }

        var duneResources = new[]
        {
            new Resource("adobe", new RgbColor(190, 130, 80), 10, BaseStorage: 1_000),
            new Resource("food", new RgbColor(200, 180, 60), 10, BaseStorage: 1_000),
        };
        var module = new BuildingDef(
            "landing", "housing", new RgbColor(200, 200, 200), 2, 2,
            WorkerSlots: 0, HousingCapacity: 30, BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: new Recipe(Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(1, 1) }, 20),
            AllowedBiomes: mask, StorageBonus: Array.Empty<ResourceAmount>(), AutoBuild: false, Buildable: false,
            UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
            TradeCapacity: trade ? 2 : 0);
        var dune = TestContent.Build(biomes, 1, duneResources, new[] { module, TestContent.SimpleBuilding("hut", biomes.Length, housing: 4) },
            gameplay, techs: new[] { tech }, prestige: prestige)
            .WithWorld(WorldProfile.Home with
            {
                Id = "dune",
                PresetIndex = 0,
                LandingModuleIndex = 0,
                StartingKit = new[] { new ResourceAmount(0, 50) },
                ExportIndices = trade ? new[] { 0 } : Array.Empty<int>(),
            });

        var contents = GalaxyContent.HomeOnly(home);
        contents.Preload(dune);

        var sim = new Simulation(home, new UniformTerrain(1), 1234);
        var state = GalaxyState.NewWithHome(sim);
        state.Active.PresetId = "test";
        state.Active.SizeId = "s";
        state.GateOpened = gateOpen;
        return new GalaxySession(contents, state, sim);
    }

    private static PlanetLook Planet() => new(new RgbColor(1, 1, 1), new RgbColor(1, 1, 1), 1, false, false, false);

    private static void Workshop(Simulation home)
    {
        Assert.Equal(PlacementResult.Ok, home.TryPlaceBuildingFree(1, 0, 0));
        home.DebugCompleteConstruction();
    }

    private static void BuildShip(GalaxySession session)
    {
        session.Active.AddResource(Wood, 10_000);
        session.StartShip("dune");
        while (!session.IsShipReady)
        {
            Assert.True(session.InvestInShip() > 0);
        }
    }

    private static void RunWithMeter(GalaxySession session, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            session.Active.Tick();
            session.Update();
        }
    }
}
