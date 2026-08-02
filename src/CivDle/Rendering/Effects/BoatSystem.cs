using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Effects;

/// <summary>
/// Lodičky u pobřežních budov: rybárna vyplouvá na vodu a vrací se s úlovkem.
///
/// <para>Proč to vzniklo: rybářská chatrč vyráběla jídlo, ale na vodě se nikdy
/// nic nehnulo. Budova, která má něco lovit, a přitom stojí celou hru
/// nehnutě, čte jako rozbitá — i když počítá správně.</para>
///
/// <para>Je to <b>kulisa</b>, ne simulace: loď nic nepřeváží a na výrobu nemá
/// vliv. Proto smí žít jen u kamery a používat obyčejnou náhodu — determinismus
/// světa se jí netýká (stejný princip jako fauna a provoz na silnicích).</para>
///
/// <para>Vrstva: čte ze simulace, nikdy do ní nezapisuje.</para>
/// </summary>
public sealed class BoatSystem
{
    private const int MaxBoats = 12;
    private const float MinZoom = 0.7f;
    private const float SpawnCooldownSeconds = 1.2f;

    /// <summary>Jak daleko od budovy loď hledá vodu (v dlaždicích).</summary>
    private const int WaterSearchTiles = 6;

    private struct Boat
    {
        public Vector2 Home;
        public Vector2 Target;
        public float Progress;   // 0 → doma, 1 → na místě lovu
        public float Speed;
        public bool Returning;
        public float Bob;
    }

    private readonly GameContent _content;
    private readonly Boat[] _boats = new Boat[MaxBoats];
    private readonly Random _rng = new();
    private int _count;
    private float _spawnTimer;

    public BoatSystem(GameContent content) => _content = content;

    public void Update(float dt, Camera2D camera, Simulation simulation)
    {
        if (camera.Zoom < MinZoom)
        {
            _count = 0; // z výšky je loď pixel — nemá cenu ji počítat
            return;
        }

        for (int i = _count - 1; i >= 0; i--)
        {
            ref var boat = ref _boats[i];
            boat.Bob += dt;
            boat.Progress += (boat.Returning ? -boat.Speed : boat.Speed) * dt;

            if (boat.Progress >= 1f)
            {
                boat.Progress = 1f;
                boat.Returning = true;
            }
            else if (boat.Progress <= 0f)
            {
                // Doplula domů — vyloží úlovek a zmizí; další vyjede za chvíli.
                _boats[i] = _boats[--_count];
            }
        }

        _spawnTimer -= dt;
        if (_spawnTimer <= 0f && _count < MaxBoats)
        {
            _spawnTimer = SpawnCooldownSeconds;
            TrySpawn(camera, simulation);
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Camera2D camera)
    {
        if (_count == 0)
        {
            return;
        }

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int i = 0; i < _count; i++)
        {
            ref readonly var boat = ref _boats[i];
            var at = Vector2.Lerp(boat.Home, boat.Target, boat.Progress);
            at.Y += MathF.Sin(boat.Bob * 2.6f) * 1.5f; // houpání na vlnách

            // Trup, plachta a brázda — tři obdélníky stačí, aby to na dálku
            // četlo jako loď a ne jako tečka.
            spriteBatch.Draw(pixel, new Rectangle((int)at.X - 6, (int)at.Y + 2, 12, 2), new Color(20, 40, 60) * 0.35f);
            spriteBatch.Draw(pixel, new Rectangle((int)at.X - 5, (int)at.Y - 1, 10, 3), new Color(120, 86, 54));
            spriteBatch.Draw(pixel, new Rectangle((int)at.X - 1, (int)at.Y - 7, 2, 6), new Color(200, 196, 180));
        }

        spriteBatch.End();
    }

    /// <summary>Vyšle loď od pobřežní budovy na nejbližší volnou vodu.</summary>
    private void TrySpawn(Camera2D camera, Simulation simulation)
    {
        const int tileSize = TerrainRenderer.TileSize;
        var (min, max) = camera.VisibleWorldBounds();

        var buildings = simulation.Buildings;
        if (buildings.Length == 0)
        {
            return;
        }

        // Náhodný pokus místo procházení všeho: budov můžou být statisíce a tohle
        // je kulisa, ne úkol. Když se netrefí, zkusí to za chvíli znovu.
        for (int attempt = 0; attempt < 8; attempt++)
        {
            int index = _rng.Next(buildings.Length);
            ref readonly var building = ref buildings[index];
            if (!building.IsComplete)
            {
                continue;
            }

            var def = _content.Buildings[building.DefIndex];
            if (!IsFishing(def))
            {
                continue;
            }

            float homeX = (building.X + def.FootprintWidth * 0.5f) * tileSize;
            float homeY = (building.Y + def.FootprintHeight * 0.5f) * tileSize;
            if (homeX < min.X || homeX > max.X || homeY < min.Y || homeY > max.Y)
            {
                continue;
            }

            if (!TryFindWater(simulation, building.X, building.Y, out int waterX, out int waterY))
            {
                continue;
            }

            _boats[_count++] = new Boat
            {
                Home = new Vector2(homeX, homeY),
                Target = new Vector2((waterX + 0.5f) * tileSize, (waterY + 0.5f) * tileSize),
                Speed = 0.12f + (float)_rng.NextDouble() * 0.08f,
                Progress = 0f,
                Returning = false,
                Bob = (float)_rng.NextDouble() * 6f,
            };
            return;
        }
    }

    /// <summary>
    /// Loví tahle budova na vodě? Poznává se to z dat, ne z natvrdo vypsaných ID —
    /// nová rybárna z modu dostane lodě taky.
    ///
    /// <para>Kritérium: buď budova potřebuje břeh (přístav, rybárna), nebo smí
    /// stát jen na pár pobřežních biomech (rybářská chatrč na pláži). Obojí je
    /// v datech a obojí znamená „tahle budova pracuje s vodou".</para>
    /// </summary>
    private bool IsFishing(BuildingDef def)
    {
        if (def.Recipe is null)
        {
            return false;
        }

        if (def.NeedsWaterAccess)
        {
            return true;
        }

        // Pobřežní specialista: skoro nikam nesmí, a to málo je u vody.
        int allowed = 0;
        bool shore = false;
        for (int i = 0; i < _content.Biomes.Count; i++)
        {
            if (!def.IsBiomeAllowed(i))
            {
                continue;
            }

            allowed++;
            shore |= _content.Biomes[i].Id is "beach" or "mangrove";
        }

        return shore && allowed <= 2;
    }

    private static bool TryFindWater(Simulation simulation, int fromX, int fromY, out int x, out int y)
    {
        for (int radius = 1; radius <= WaterSearchTiles; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                    {
                        continue; // jen obvod prstence, vnitřek už se prohledal
                    }

                    if (simulation.IsWaterAt(fromX + dx, fromY + dy))
                    {
                        x = fromX + dx;
                        y = fromY + dy;
                        return true;
                    }
                }
            }
        }

        x = y = 0;
        return false;
    }
}
