using CivDle.Core.Content;
using CivDle.Core.Galaxy;
using CivDle.Core.World;
using CivDle.Rendering;
using CivDle.Screens;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Mapa galaxie bez okna: povrch planety se peče z mapy světa (planeta je
/// doopravdy tvoje mapa) a všechny texty galaxie existují ve všech jazycích.
/// </summary>
public sealed class GalaxyUiTests
{
    private static readonly string[] WorldIds = { "home", "dune", "frost", "archipelago", "forge", "gas_giant", "xeno" };

    [Fact]
    public void EveryGalaxyText_ExistsInEveryLanguage()
    {
        var content = LoadContent();
        var keys = new List<string>
        {
            "hud.galaxy", "tip.galaxy", "galaxy.title", "galaxy.here", "galaxy.youAreHere", "galaxy.travel",
            "galaxy.population", "galaxy.stars", "galaxy.totalStars", "galaxy.exports", "galaxy.locked",
            "galaxy.opened.title", "galaxy.opened", "galaxy.ship.stages", "galaxy.ship.start", "galaxy.ship.stage",
            "galaxy.ship.invest", "galaxy.ship.ready", "galaxy.ship.land", "galaxy.ship.elsewhere",
            "landing.title", "landing.site", "landing.terrain", "landing.land", "landing.none", "loading.travel",
            "panel.back",
            "galaxy.trade.title", "galaxy.trade.none", "galaxy.trade.route", "galaxy.trade.rate",
            "galaxy.trade.open", "galaxy.trade.close", "galaxy.trade.port", "galaxy.trade.hint",
            "tip.resource.imported", "tip.resource.use.export",
        };
        keys.AddRange(Enum.GetValues<ShipBlocker>().Where(b => b != ShipBlocker.None).Select(GalaxyScreen.BlockerKey));
        keys.AddRange(Enum.GetValues<TradeRouteStatus>().Select(GalaxyScreen.RouteStatusKey));
        foreach (string world in WorldIds)
        {
            keys.Add($"world.{world}");
            keys.Add($"world.{world}.rule");
            keys.Add($"world.{world}.desc");
        }

        foreach (var language in content.Languages.All)
        {
            var loc = new Localization(content.Languages, language.Id);
            foreach (string key in keys)
            {
                Assert.True(language.Strings.ContainsKey(key), $"jazyk '{language.Id}' nemá '{key}'");
                Assert.DoesNotContain("~", loc[key]);
            }
        }
    }

    [Fact]
    public void ThePlanetIsReallyTheWorldsMap()
    {
        var content = LoadContent();
        var preset = content.WorldGen.Presets[content.WorldGen.DefaultPresetIndex];
        var terrain = new ProceduralTerrain(content.Biomes, preset, 42);
        var look = new PlanetLook(new RgbColor(70, 120, 60), new RgbColor(40, 90, 140), 1, false, false, false);

        var surface = PlanetSurface.FromTerrain(content, terrain, look, 0, 0, population: 50_000);

        Assert.Equal(PlanetSurface.Width * PlanetSurface.Height, surface.Colors.Length);
        // Střed mapy je biom pod městem.
        var center = content.Biomes[terrain.BiomeAt(0, 0)].MapColor;
        Assert.Equal(new Color(center.R, center.G, center.B), surface.Colors[PlanetSurface.Height / 2 * PlanetSurface.Width + PlanetSurface.Width / 2]);
        // Světla města svítí u středu, ne na druhé straně planety.
        Assert.True(surface.Lights.Skip(PlanetSurface.Height / 2 * PlanetSurface.Width).Take(PlanetSurface.Width).Max() > 0.3f);
        Assert.Equal(0f, surface.Lights[0]);
    }

    [Fact]
    public void AnEmptyWorldHasNoCityLights()
    {
        var content = LoadContent();
        var preset = content.WorldGen.Presets[content.WorldGen.DefaultPresetIndex];
        var terrain = new ProceduralTerrain(content.Biomes, preset, 7);
        var look = new PlanetLook(new RgbColor(70, 120, 60), new RgbColor(40, 90, 140), 1, false, false, false);

        var surface = PlanetSurface.FromTerrain(content, terrain, look, 0, 0, population: 0);

        Assert.All(surface.Lights, light => Assert.Equal(0f, light));
    }

    [Fact]
    public void AGasGiantHasBandsAndAnIceWorldHasCaps()
    {
        var giant = PlanetSurface.FromLook(
            new PlanetLook(new RgbColor(230, 200, 150), new RgbColor(170, 100, 60), 1.4, true, true, false), 3);
        var ice = PlanetSurface.FromLook(
            new PlanetLook(new RgbColor(220, 230, 240), new RgbColor(90, 120, 150), 0.9, false, false, true), 3);

        var column = Enumerable.Range(0, PlanetSurface.Height).Select(v => giant.Colors[v * PlanetSurface.Width]).ToList();
        Assert.True(column.Distinct().Count() >= 2, "plynný obr má mít pásy");
        Assert.True(ice.Colors[0].R > 225 && ice.Colors[0].B > 225, "ledový svět má bílé póly");
    }

    [Fact]
    public void TheStormForecastAndABuriedBuildingSpeakEveryLanguage()
    {
        var content = LoadContent();
        Assert.Equal("stall.buried", GameplayScreen.StallText(Core.Sim.BuildingStall.Buried));
        foreach (var language in content.Languages.All)
        {
            var loc = new Localization(content.Languages, language.Id);
            string warning = loc.Format("hud.hazard.warning", "Bouře", loc["hazard.from.6"], 42);
            Assert.Contains("42", warning);
            Assert.DoesNotContain("{", warning);
            Assert.DoesNotContain("{", loc.Format("hud.hazard.active", "Bouře"));
            Assert.True(language.Strings.ContainsKey("stall.buried"), $"jazyk '{language.Id}' nemá 'stall.buried'");
        }
    }

    [Fact]
    public void StarsAreShownAsFilledAndEmpty()
    {
        Assert.Equal("☆☆☆", GalaxyScreen.Stars(0));
        Assert.Equal("★★☆", GalaxyScreen.Stars(2));
        Assert.Equal("★★★★", GalaxyScreen.Stars(4)); // mistrovská navíc
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
