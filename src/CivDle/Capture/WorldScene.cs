using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Rendering;
using CivDle.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Capture;

/// <summary>
/// Svět nakreslený <b>bez rozhraní</b> — terén, voda, cesty, budovy, kouř,
/// denní světlo. Přesně to, co má být na fotce i ve videu a co na obrazovce
/// zakrývá HUD.
///
/// <para>Proč vlastní třída: fotka i video potřebují tutéž kulisu, jen ji
/// posílají jinam (jednou do PNG, jindy do sekvence snímků). Kdyby si každá
/// z nich seznam vrstev držela sama, rozešly by se hned po první změně
/// rendereru a hráč by na fotce viděl jinou hru než na videu.</para>
///
/// <para>Není to totéž jako herní obrazovka: ta navíc kreslí ducha budovy pod
/// kurzorem, zvýraznění nástrojů, cedule a HUD. Tady schválně nic z toho není
/// — je to pohled na svět, ne na hru.</para>
///
/// <para>Vrstva: čistý render nad simulací. Vlastní instance rendererů, aby
/// focení nesahalo do těch, které patří obrazovce.</para>
/// </summary>
public sealed class WorldScene : IDisposable
{
    private readonly ScreenManager _screens;
    private readonly TerrainRenderer _terrain;
    private readonly WaterRenderer _water;
    private readonly DecorationRenderer _decorations;
    private readonly UrbanGroundRenderer _urbanGround;
    private readonly RoadRenderer _roads;
    private readonly BuildingRenderer _buildings;
    private readonly AmbientLifeRenderer _ambient;
    private readonly LightsRenderer _lights;

    // Atmosféra. Bez ní vypadá fotka a video jako jiná hra než ta, ze které
    // vznikly — a přitom je to přesně to, kvůli čemu se fotí.
    private readonly ValleyMistRenderer _mist;
    private readonly CloudShadowRenderer _cloudShadows;
    private readonly CloudLayerRenderer _cloudLayer;
    private readonly GodRayRenderer _godRays;
    private readonly Rendering.Effects.AmbientMotes _motes;

    public WorldScene(ScreenManager screens, GameContent content, long seed)
    {
        _screens = screens;
        var device = screens.GraphicsDevice;
        var pixel = screens.WhitePixel;

        _terrain = new TerrainRenderer(device, content.Biomes, seed);
        _water = new WaterRenderer(pixel);
        _decorations = new DecorationRenderer(pixel, content, seed, screens.Sprites);
        _urbanGround = new UrbanGroundRenderer(screens.SoftShadow, content);
        _roads = new RoadRenderer(pixel, content);
        _buildings = new BuildingRenderer(pixel, content, screens.Sprites, screens.SoftShadow);
        _ambient = new AmbientLifeRenderer(pixel, content);
        _lights = new LightsRenderer(pixel, content);
        _mist = new ValleyMistRenderer(device);
        _cloudShadows = new CloudShadowRenderer(device);
        _cloudLayer = new CloudLayerRenderer(device);
        _godRays = new GodRayRenderer(device);
        _motes = new Rendering.Effects.AmbientMotes(seed);
    }

    /// <summary>
    /// Posune animované vrstvy. Bez tohohle by na videu stál kouř na místě
    /// a hladina by byla ze skla — tedy přesně ty věci, kvůli kterým se
    /// natáčí video a ne screenshot.
    /// </summary>
    /// <param name="camera">
    /// Odkud se právě dívá — poletující částice žijí ve světových
    /// souřadnicích a zabalují se kolem výřezu, takže musí vědět, kde ten
    /// výřez je. Bez kamery by se rozsypaly kolem počátku a u města postaveného
    /// jinde by na snímku nebyly.
    /// </param>
    public void Update(float dt, Simulation simulation, Camera2D camera)
    {
        _water.Update(dt);
        _urbanGround.Update(dt, simulation);
        _ambient.Update(dt);
        _lights.Update(dt);
        _buildings.Update(dt);
        _decorations.Update(dt);
        _mist.Update(dt);
        _cloudShadows.Update(dt);
        _cloudLayer.Update(dt);
        _godRays.Update(dt);

        var season = simulation.CurrentSeason;
        var (min, max) = camera.VisibleWorldBounds();
        _motes.Update(
            dt, min, max,
            (float)(season?.MoteDensity ?? 0.0), (float)(season?.MoteFall ?? 0.0),
            WindX, WindY);
    }

    /// <summary>Směr větru. Tentýž jako ve hře, jinak by kouř letěl jinam než mraky.</summary>
    private const float WindX = 0.94f;

    private const float WindY = 0.34f;

    /// <summary>Vykreslí svět. Volající si řídí render target i kameru.</summary>
    public void Draw(Camera2D camera, Simulation simulation, Viewport viewport)
    {
        var spriteBatch = _screens.SpriteBatch;

        // Období se předává i sem: fotka s letní zemí pod zimním městem by
        // byla ta nejnápadnější možná neshoda mezi hrou a tím, co si hráč
        // uloží. Chunky jsou přitom tytéž, takže to nic nestojí.
        _terrain.Draw(
            spriteBatch, camera, simulation.Terrain,
            simulation.BiomeOverrideMap, simulation.TerrainRevision,
            Rendering.SeasonGround.From(simulation.CurrentSeason));
        _water.Draw(spriteBatch, camera, simulation);
        _decorations.Draw(spriteBatch, camera, simulation.Terrain);
        _mist.Draw(spriteBatch, camera, simulation.Terrain, ValleyMistRenderer.Density(simulation.TimeOfDay01));
        _urbanGround.Draw(spriteBatch, camera);
        _roads.Draw(spriteBatch, camera, simulation);
        _buildings.Draw(spriteBatch, camera, simulation);
        _ambient.Draw(spriteBatch, camera, simulation);

        // Stíny mraků nad vším, co na zemi stojí, ale pod osvětlením: stín je
        // ubrané světlo, a v noci není co ubírat.
        float coverage = CloudCoverage(simulation);
        _cloudShadows.Draw(spriteBatch, camera, viewport, coverage, WindX, WindY);

        // Světlo až nakonec, přes hotovou scénu — stejným násobičem jako ve hře.
        // Kdyby si focení počítalo vlastní, vypadal by snímek jinak než hra,
        // ze které vznikl.
        double timeOfDay = simulation.TimeOfDay01;
        var light = DayNightCycle.LightColor(
            timeOfDay, _screens.Content.Gameplay.DayNight, simulation.CurrentSeason);
        DayNightCycle.DrawLight(spriteBatch, _screens.WhitePixel, viewport, light);

        // Lampy a okna až nad osvětlením — jsou to zdroje světla, ne plocha.
        _lights.Draw(spriteBatch, camera, simulation, DayNightCycle.NightFactor(timeOfDay));

        // Co letí vzduchem, jde úplně nakonec: je to mezi kamerou a městem,
        // takže to nemá co zastínit.
        if (simulation.CurrentSeason is { HasMotes: true } motesSeason)
        {
            _motes.Draw(spriteBatch, camera, _screens.WhitePixel, motesSeason.MoteColor!.Value.ToXna());
        }

        _cloudLayer.Draw(spriteBatch, camera, viewport, coverage, WindX, WindY, light);
        _godRays.Draw(spriteBatch, viewport, DayNightCycle.DuskFactor(timeOfDay), light);
    }

    /// <summary>
    /// Kolik oblohy zabírají mraky. Odvozeno z počasí stejně jako ve hře —
    /// kdyby si focení počítalo vlastní, byl by na fotce jiný den.
    /// </summary>
    private float CloudCoverage(Simulation simulation)
    {
        var weather = _screens.Content.Weather;
        int index = simulation.CurrentWeatherIndex;
        float overcast = index >= 0 && index < weather.Count ? (float)weather[index].TintAlpha : 0f;
        return Math.Clamp(0.35f + overcast, 0f, 1f);
    }

    public void Dispose()
    {
        _terrain.Dispose();
        _mist.Dispose();
        _cloudShadows.Dispose();
        _cloudLayer.Dispose();
        _godRays.Dispose();
    }
}
