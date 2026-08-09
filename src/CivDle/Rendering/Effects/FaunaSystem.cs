using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Effects;

/// <summary>
/// Ambientní fauna (living-map.md): tvorové existují JEN u kamery — render je
/// spawnuje v viditelném výřezu, hýbe jimi a mimo obraz je ruší (LOD). Kulisa,
/// ne simulace: náhoda tady nevadí, determinismus světa se jí nedotýká.
/// Pevný pool bez alokací za běhu; denní/noční druhy podle času simulace.
/// </summary>
public sealed class FaunaSystem
{
    private const int MaxCritters = 18;
    private const float MinZoom = 0.55f;
    private const float SpawnCooldownSeconds = 0.5f;
    private const float DespawnMargin = 96f;

    private struct Critter
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public int DefIndex;
        public float DirectionTimer;
        public float Phase;
    }

    private readonly GameContent _content;
    private readonly Critter[] _critters = new Critter[MaxCritters];
    private readonly List<int> _eligibleDefs = new();
    private int _count;
    private float _spawnTimer;

    public FaunaSystem(GameContent content)
    {
        _content = content;
    }

    public void Update(float dt, Camera2D camera, Simulation simulation)
    {
        if (camera.Zoom < DetailLevel.Scale(MinZoom) || _content.Fauna.Count == 0)
        {
            _count = 0; // oddáleno → fauna zmizí (z dálky ji nikdo nevidí)
            return;
        }

        bool isNight = DayNightCycle.NightFactor(simulation.TimeOfDay01) > 0.5f;
        var (min, max) = camera.VisibleWorldBounds();

        UpdateCritters(dt, simulation, isNight, min, max);
        TrySpawn(dt, simulation, isNight, min, max);
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
            ref readonly var critter = ref _critters[i];
            var def = _content.Fauna[critter.DefIndex];

            // Světlušky pulzují; ostatní tvorové jsou plné tečky.
            float alpha = def.Glow ? 0.45f + 0.55f * MathF.Abs(MathF.Sin(critter.Phase * 3f)) : 1f;
            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    (int)(critter.Position.X - def.Size * 0.5f),
                    (int)(critter.Position.Y - def.Size * 0.5f),
                    def.Size,
                    def.Size),
                def.Color.ToXna() * alpha);
        }

        spriteBatch.End();
    }

    private void UpdateCritters(float dt, Simulation simulation, bool isNight, Vector2 min, Vector2 max)
    {
        for (int i = _count - 1; i >= 0; i--)
        {
            ref var critter = ref _critters[i];
            var def = _content.Fauna[critter.DefIndex];

            critter.Position += critter.Velocity * dt;
            critter.Phase += dt;
            critter.DirectionTimer -= dt;
            if (critter.DirectionTimer <= 0f)
            {
                critter.Velocity = RandomDirection() * def.Speed;
                critter.DirectionTimer = 1.5f + Random.Shared.NextSingle() * 3f;
            }

            int tileX = (int)MathF.Floor(critter.Position.X / TerrainRenderer.TileSize);
            int tileY = (int)MathF.Floor(critter.Position.Y / TerrainRenderer.TileSize);
            bool outOfView = critter.Position.X < min.X - DespawnMargin || critter.Position.X > max.X + DespawnMargin
                || critter.Position.Y < min.Y - DespawnMargin || critter.Position.Y > max.Y + DespawnMargin;
            bool wrongTime = def.Time == FaunaTime.Day && isNight || def.Time == FaunaTime.Night && !isNight;
            bool badTile = !def.BiomeMask[simulation.BiomeAt(tileX, tileY)]
                || simulation.IsOccupied(tileX, tileY);

            if (outOfView || wrongTime || badTile)
            {
                _critters[i] = _critters[--_count];
            }
        }
    }

    private void TrySpawn(float dt, Simulation simulation, bool isNight, Vector2 min, Vector2 max)
    {
        _spawnTimer -= dt;
        if (_spawnTimer > 0f || _count >= MaxCritters)
        {
            return;
        }

        _spawnTimer = SpawnCooldownSeconds;

        float x = min.X + Random.Shared.NextSingle() * (max.X - min.X);
        float y = min.Y + Random.Shared.NextSingle() * (max.Y - min.Y);
        int tileX = (int)MathF.Floor(x / TerrainRenderer.TileSize);
        int tileY = (int)MathF.Floor(y / TerrainRenderer.TileSize);
        if (simulation.IsOccupied(tileX, tileY))
        {
            return;
        }

        byte biome = simulation.BiomeAt(tileX, tileY);
        _eligibleDefs.Clear();
        for (int i = 0; i < _content.Fauna.Count; i++)
        {
            var def = _content.Fauna[i];
            bool timeOk = def.Time == FaunaTime.Any || (def.Time == FaunaTime.Night) == isNight;
            if (timeOk && def.BiomeMask[biome])
            {
                _eligibleDefs.Add(i);
            }
        }

        if (_eligibleDefs.Count == 0)
        {
            return;
        }

        int defIndex = _eligibleDefs[Random.Shared.Next(_eligibleDefs.Count)];
        _critters[_count++] = new Critter
        {
            Position = new Vector2(x, y),
            Velocity = RandomDirection() * _content.Fauna[defIndex].Speed,
            DefIndex = defIndex,
            DirectionTimer = 1f + Random.Shared.NextSingle() * 2f,
            Phase = Random.Shared.NextSingle() * 10f,
        };
    }

    private static Vector2 RandomDirection()
    {
        float angle = Random.Shared.NextSingle() * MathF.Tau;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }
}
