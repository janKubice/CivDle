using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Core.WorldGen;

namespace CivDle.Screens;

/// <summary>
/// Založení nového světa — jedna cesta pro rychlý start z menu i pro
/// „Vlastní svět" s volbami. Dřív to uměla jen obrazovka s volbami, takže
/// nová hra vždycky začínala otázkami na seed a typ světa.
/// </summary>
internal static class WorldLauncher
{
    /// <summary>
    /// Rychlý start: prověřený svět z dat (<c>onboarding.quickStartSeeds</c> —
    /// z každého vyroste fungující město), výchozí typ světa, žádné volby.
    /// Bez seznamu v datech náhodný svět.
    /// </summary>
    public static void QuickStart(ScreenManager screens)
    {
        var seeds = screens.Content.Gameplay.Onboarding.QuickStartSeeds;
        long seed = seeds.Count > 0 ? seeds[Random.Shared.Next(seeds.Count)] : SeedUtil.NewRandom();
        Start(screens, seed, screens.Content.WorldGen.DefaultPresetIndex, sandbox: false, frontier: false);
    }

    /// <summary>Založí svět podle voleb a přes načítací obrazovku do něj přepne.</summary>
    public static void Start(ScreenManager screens, long seed, int presetIndex, bool sandbox, bool frontier)
    {
        var content = screens.Content;
        var preset = content.WorldGen.Presets[presetIndex];

        // Nekonečný terén: žádné generování mapy dopředu — počítá se on-demand,
        // takže „velikost světa" už nemá smysl. Do savu ukládáme jen ID pro
        // zpětnou kompatibilitu (výchozí velikost z katalogu).
        var terrain = new ProceduralTerrain(content.Biomes, preset, seed);
        var simulation = new Simulation(content, terrain, seed);

        // Volí se jen tady. Rozehraná hra režim nemění — viz Simulation.Sandbox.
        if (sandbox)
        {
            simulation.MarkAsSandbox();
        }

        // Pojistka, ne jen skrytý přepínač: kdyby se volba někdy nastavila
        // jinudy (načtené nastavení, klávesa, budoucí obrazovka), demo by se
        // rozjelo s vlnami, které v něm být nemají.
        if (frontier && !Edition.IsDemo)
        {
            simulation.EnableFrontierDefense();
        }

        string sizeId = content.WorldGen.Sizes[content.WorldGen.DefaultSizeIndex].Id;
        var info = new WorldInfo(seed, sizeId, preset.Id);

        // Přes načítací obrazovku: skok z menu rovnou do rozehrané mapy působil
        // jako záseknutí, hráč nestihl přepnout pozornost.
        screens.ReplaceAll(new LoadingScreen(
            screens, "loading.newWorld", _ => new GameplayScreen(screens, simulation, info)));
    }
}
