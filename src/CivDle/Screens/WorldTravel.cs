using CivDle.Core.Galaxy;

namespace CivDle.Screens;

/// <summary>
/// Cesta mezi světy z pohledu obrazovek (svety-design.md 2.4, 5.5): přepne
/// svět v galaxii, přepne obsah, uloží a postaví novou herní obrazovku —
/// přes krátký přelet, nebo přes načítání, když se dohání dlouhá nepřítomnost.
///
/// <para>Jedno místo pro „odlet", ať karta světa, přepínač v HUD i přistání
/// kolonie dělají totéž ve stejném pořadí: nejdřív svět, pak obsah, pak save,
/// pak obrazovka. Kdyby obrazovka vznikla dřív než obsah, renderery by si
/// vzaly budovy Domoviny a kreslily kolonii jejími sprity.</para>
/// </summary>
internal static class WorldTravel
{
    /// <summary>Odletí na založený svět.</summary>
    public static void Go(ScreenManager screens, GalaxySession session, string worldId)
    {
        var entry = session.SwitchTo(worldId);
        screens.BeginSession(session);
        var info = InfoOf(session);
        var target = session.Catalog.Find(worldId)!;

        if (entry.CatchUp is null)
        {
            screens.SaveGame(entry.Simulation, info);
            screens.ReplaceAll(new WarpScreen(
                screens, target, () => new GameplayScreen(screens, entry.Simulation, info, null, session)));
            return;
        }

        // Hodiny pryč: poctivé dohánění s ukazatelem. Až doběhne, připíše se
        // obchod za nepřítomnost a teprve pak se ukládá.
        screens.ReplaceAll(new LoadingScreen(
            screens, "loading.travel",
            offline =>
            {
                entry.Complete();
                screens.SaveGame(entry.Simulation, info);
                return new GameplayScreen(screens, entry.Simulation, info, offline, session);
            },
            entry.CatchUp,
            entry.Simulation));
    }

    /// <summary>Přistane s hotovou lodí na zvoleném místě a začne hrát novou kolonii.</summary>
    public static void Land(ScreenManager screens, GalaxySession session, int x, int y)
    {
        var target = session.ShipTarget!;
        var colony = session.Colonize(x, y);
        screens.BeginSession(session);
        var info = InfoOf(session);
        screens.SaveGame(colony, info);
        screens.ReplaceAll(new WarpScreen(
            screens, target, () => new GameplayScreen(screens, colony, info, null, session)));
    }

    /// <summary>Metadata aktivního světa (seed, velikost, předvolba) pro save a render.</summary>
    public static WorldInfo InfoOf(GalaxySession session)
    {
        var record = session.State.Active;
        return new WorldInfo(session.Active.Seed, record.SizeId, record.PresetId);
    }
}
