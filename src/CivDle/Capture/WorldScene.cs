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

    public WorldScene(ScreenManager screens, GameContent content, long seed)
    {
        _screens = screens;
        var device = screens.GraphicsDevice;
        var pixel = screens.WhitePixel;

        _terrain = new TerrainRenderer(device, content.Biomes, seed);
        _water = new WaterRenderer(pixel);
        _decorations = new DecorationRenderer(pixel, content, seed);
        _urbanGround = new UrbanGroundRenderer(screens.SoftShadow, content);
        _roads = new RoadRenderer(pixel, content);
        _buildings = new BuildingRenderer(pixel, content, screens.Sprites, screens.SoftShadow);
        _ambient = new AmbientLifeRenderer(pixel, content);
        _lights = new LightsRenderer(pixel, content);
    }

    /// <summary>
    /// Posune animované vrstvy. Bez tohohle by na videu stál kouř na místě
    /// a hladina by byla ze skla — tedy přesně ty věci, kvůli kterým se
    /// natáčí video a ne screenshot.
    /// </summary>
    public void Update(float dt, Simulation simulation)
    {
        _water.Update(dt);
        _urbanGround.Update(dt, simulation);
        _ambient.Update(dt);
        _lights.Update(dt);
        _buildings.Update(dt);
    }

    /// <summary>Vykreslí svět. Volající si řídí render target i kameru.</summary>
    public void Draw(Camera2D camera, Simulation simulation, Viewport viewport)
    {
        var spriteBatch = _screens.SpriteBatch;

        _terrain.Draw(
            spriteBatch, camera, simulation.Terrain,
            simulation.BiomeOverrideMap, simulation.TerrainRevision);
        _water.Draw(spriteBatch, camera, simulation);
        _decorations.Draw(spriteBatch, camera, simulation.Terrain);
        _urbanGround.Draw(spriteBatch, camera);
        _roads.Draw(spriteBatch, camera, simulation);
        _buildings.Draw(spriteBatch, camera, simulation);
        _ambient.Draw(spriteBatch, camera, simulation);

        // Světlo až nakonec, přes hotovou scénu — stejné pořadí jako ve hře.
        double timeOfDay = simulation.TimeOfDay01;
        DayNightCycle.DrawSeasonTint(spriteBatch, _screens.WhitePixel, viewport, simulation.CurrentSeason);
        DayNightCycle.DrawGrade(spriteBatch, _screens.WhitePixel, viewport, timeOfDay);
        DayNightCycle.DrawOverlay(
            spriteBatch, _screens.WhitePixel, viewport,
            _screens.Content.Gameplay.DayNight, timeOfDay);
        _lights.Draw(spriteBatch, camera, simulation, DayNightCycle.NightFactor(timeOfDay));
    }

    public void Dispose() => _terrain.Dispose();
}
