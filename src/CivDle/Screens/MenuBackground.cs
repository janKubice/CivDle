using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Core.WorldGen;
using CivDle.Rendering;
using CivDle.Rendering.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Screens;

/// <summary>
/// Živé město na pozadí menu: vlastní ukázková simulace nad náhodným seedem,
/// kterou „režisér" postupně zastavuje budovami, takže vesnice před očima roste,
/// napojuje se cestami, rozsvěcí se v noci a chodí v ní lidičky. Kamera nad ní
/// klidně pluje. Sdílí se mezi obrazovkami menu (roste dál, nerestartuje se).
/// Používá stejné renderery jako hra — jen bez HUD a bez vstupu.
/// </summary>
public sealed class MenuBackground : IDisposable
{
    private const int MaxTownBuildings = 160;
    private const float PlaceIntervalSeconds = 0.35f;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly Camera2D _camera = new();
    private readonly TerrainRenderer _terrain;
    private readonly DecorationRenderer _decorations;
    private readonly HarvestableRenderer _harvestables;
    private readonly RoadRenderer _roads;
    private readonly BuildingRenderer _buildings;
    private readonly LightsRenderer _lights;
    private readonly FaunaSystem _fauna;
    private readonly AgentSystem _agents;
    private readonly FixedStepLoop _simLoop = new(Simulation.TicksPerSecond);
    private readonly int[] _buildWeights;
    private readonly int _weightSum;

    private Vector2 _focus;
    private float _placeTimer;
    private float _time;

    public MenuBackground(ScreenManager screens)
    {
        _screens = screens;
        var content = screens.Content;
        long seed = SeedUtil.NewRandom();

        var preset = content.WorldGen.Presets[content.WorldGen.DefaultPresetIndex];
        var terrain = new ProceduralTerrain(content.Biomes, preset, seed);
        _simulation = new Simulation(content, terrain, seed);

        _terrain = new TerrainRenderer(screens.GraphicsDevice, content.Biomes, seed);
        _decorations = new DecorationRenderer(screens.WhitePixel, content, seed);
        _harvestables = new HarvestableRenderer(screens.Sprites, content);
        _roads = new RoadRenderer(screens.WhitePixel, content);
        _buildings = new BuildingRenderer(screens.WhitePixel, content, screens.Sprites, screens.SoftShadow);
        _lights = new LightsRenderer(screens.WhitePixel, content);
        _fauna = new FaunaSystem(content);
        _agents = new AgentSystem(content, screens.Sprites);

        // Váhy typů: hodně domů, sem tam produkce a sklad → vyváženě rostoucí město.
        _buildWeights = new int[content.Buildings.Count];
        for (int i = 0; i < content.Buildings.Count; i++)
        {
            _buildWeights[i] = content.Buildings[i].Id switch
            {
                "house" => 6,
                "farm" => 2,
                "warehouse" => 1,
                _ => 2,
            };
        }

        _weightSum = _buildWeights.Sum();
        _focus = SeedTown();
        _camera.SetViewport(screens.GraphicsDevice.Viewport.Width, screens.GraphicsDevice.Viewport.Height);
        _camera.CenterOn(_focus, zoom: 1.9f);
    }

    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _time += dt;
        _camera.SetViewport(_screens.GraphicsDevice.Viewport.Width, _screens.GraphicsDevice.Viewport.Height);

        // Simulace ať běží (den/noc, výroba, auto-stavba, osady).
        TopUpResources();
        int ticks = _simLoop.Advance(gameTime.ElapsedGameTime.TotalSeconds);
        for (int i = 0; i < ticks; i++)
        {
            _simulation.Tick();
        }

        GrowTown(dt);

        // Klidný nálet kamery nad centrum města (jemné Lissajous kolébání).
        var centroid = TownCentroid();
        var drift = new Vector2(MathF.Sin(_time * 0.11f) * 90f, MathF.Cos(_time * 0.08f) * 70f);
        _camera.CenterOn(Vector2.Lerp(_camera.Position, centroid + drift, dt * 0.6f), 1.9f);

        _harvestables.Update(dt);
        _fauna.Update(dt, _camera, _simulation);
        _agents.Update(dt, _camera, _simulation);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        _terrain.Draw(spriteBatch, _camera, _simulation.Terrain);
        _decorations.Draw(spriteBatch, _camera, _simulation.Terrain);
        _harvestables.Draw(spriteBatch, _camera, _simulation);
        _roads.Draw(spriteBatch, _camera, _simulation);
        _buildings.Draw(spriteBatch, _camera, _simulation);
        _agents.Draw(spriteBatch, _camera);
        _fauna.Draw(spriteBatch, _screens.WhitePixel, _camera);

        double timeOfDay = _simulation.TimeOfDay01;
        DayNightCycle.DrawOverlay(
            spriteBatch, _screens.WhitePixel, _screens.GraphicsDevice.Viewport,
            _screens.Content.Gameplay.DayNight, timeOfDay);
        _lights.Draw(spriteBatch, _camera, _simulation, DayNightCycle.NightFactor(timeOfDay));
    }

    public void Dispose() => _terrain.Dispose();

    /// <summary>Založí město: doplní suroviny a postaví první budovu na vhodné dlaždici u počátku.</summary>
    private Vector2 SeedTown()
    {
        TopUpResources();
        int house = FindBuildable("house");
        for (int radius = 0; radius < 400; radius++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius)
                    {
                        continue;
                    }

                    if (_simulation.TryPlaceBuilding(house, x, y) == PlacementResult.Ok)
                    {
                        return new Vector2((x + 0.5f) * TerrainRenderer.TileSize, (y + 0.5f) * TerrainRenderer.TileSize);
                    }
                }
            }
        }

        return Vector2.Zero;
    }

    private void GrowTown(float dt)
    {
        if (_simulation.Buildings.Length >= MaxTownBuildings)
        {
            return;
        }

        _placeTimer -= dt;
        if (_placeTimer > 0f)
        {
            return;
        }

        _placeTimer = PlaceIntervalSeconds;
        int defIndex = PickWeightedBuilding();
        var buildings = _simulation.Buildings;
        var anchor = buildings[Random.Shared.Next(buildings.Length)];

        // Zkus pár míst v malém okruhu kolem náhodné budovy → organický shluk.
        for (int attempt = 0; attempt < 24; attempt++)
        {
            int ox = Random.Shared.Next(-7, 8);
            int oy = Random.Shared.Next(-7, 8);
            if (_simulation.TryPlaceBuilding(defIndex, anchor.X + ox, anchor.Y + oy) == PlacementResult.Ok)
            {
                return;
            }
        }
    }

    private int PickWeightedBuilding()
    {
        int roll = Random.Shared.Next(_weightSum);
        for (int i = 0; i < _buildWeights.Length; i++)
        {
            roll -= _buildWeights[i];
            if (roll < 0)
            {
                return i;
            }
        }

        return 0;
    }

    private int FindBuildable(string id)
    {
        for (int i = 0; i < _screens.Content.Buildings.Count; i++)
        {
            if (_screens.Content.Buildings[i].Id == id)
            {
                return i;
            }
        }

        return 0;
    }

    private Vector2 TownCentroid()
    {
        var buildings = _simulation.Buildings;
        if (buildings.Length == 0)
        {
            return _focus;
        }

        float sumX = 0f, sumY = 0f;
        for (int i = 0; i < buildings.Length; i++)
        {
            sumX += buildings[i].X;
            sumY += buildings[i].Y;
        }

        return new Vector2(
            sumX / buildings.Length * TerrainRenderer.TileSize,
            sumY / buildings.Length * TerrainRenderer.TileSize);
    }

    /// <summary>Menu si řídí vlastní simulaci — doplňujeme suroviny, ať město poroste bez limitu.</summary>
    private void TopUpResources()
    {
        for (int i = 0; i < _screens.Content.Resources.Count; i++)
        {
            _simulation.AddResource(i, _simulation.GetStorageCap(i));
        }
    }
}
