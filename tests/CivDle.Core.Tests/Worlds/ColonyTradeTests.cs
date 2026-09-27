using CivDle.Core.Content;
using CivDle.Core.Galaxy;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Worlds;

/// <summary>
/// Obchod mezi koloniemi se skutečnými daty (svety-design.md 1.2 pilíř 4):
/// sklo z Duny jede na lagunové vily Souostroví. Domovina při tom nemusí
/// být ani jedním koncem trasy.
/// </summary>
public sealed class ColonyTradeTests
{
    [Fact]
    public void DuneShipsGlassToTheArchipelago()
    {
        var session = TwoColonies(duneGlass: 500);

        Assert.Equal(TradeBlocker.None, session.CanOpenRoute("dune", "archipelago", "glass"));
        Assert.Equal(TradeBlocker.UnknownAtDestination, session.CanOpenRoute("archipelago", "dune", "pearls"));

        var route = session.OpenRoute("dune", "archipelago", "glass");
        int glass = session.Active.Content.Resources.IndexOf("glass");
        for (int i = 0; i < 6_000; i++) // deset minut: cesta, dávky, vykládka
        {
            session.Active.Tick();
            session.Update();
        }

        Assert.True(route.TotalShipped > 0, "z Duny nic neodplulo");
        Assert.True(session.Active.GetResource(glass) > 0, "na Souostroví sklo nedorazilo");
        Assert.Equal(-route.TotalShipped, session.State.Records["dune"].PendingDelta["glass"], 6);
    }

    /// <summary>Domovina s otevřenou bránou, kolonie na Duně (se skladem skla) a teď aktivní Souostroví.</summary>
    private static GalaxySession TwoColonies(double duneGlass)
    {
        var contents = new GalaxyContent(TestData.RealDataDirectory, Array.Empty<CivDle.Core.Content.Mods.ModPackage>(), TestData.LoadRealContent());
        var homeContent = contents.Home;
        var preset = homeContent.WorldGen.Presets[homeContent.WorldGen.DefaultPresetIndex];
        var home = new Simulation(homeContent, WorldTerrain.Create(homeContent, preset, 5), 5);

        var state = GalaxyState.NewWithHome(home);
        state.Active.PresetId = preset.Id; // návrat domů staví terén Domoviny podle předvolby
        state.Active.SizeId = homeContent.WorldGen.Sizes[homeContent.WorldGen.DefaultSizeIndex].Id;
        state.GateOpened = true;
        var session = new GalaxySession(contents, state, home);

        Colonize(session, "dune");
        session.Active.DebugSetResource(session.Active.Content.Resources.IndexOf("glass"), duneGlass);
        session.SwitchTo(WorldScope.HomeId);
        Colonize(session, "archipelago");
        return session;
    }

    private static void Colonize(GalaxySession session, string worldId)
    {
        var world = session.Catalog.Find(worldId)!;
        session.State.Ship = new ColonyShipState(world.Id) { StageIndex = world.ColonyCost.Count };
        var site = session.LandingSites(world.Id)[0];
        session.Colonize(site.X, site.Y);
    }
}
