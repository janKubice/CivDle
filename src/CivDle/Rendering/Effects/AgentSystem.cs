using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Effects;

/// <summary>
/// Živý svět (bod „chci vidět lidičky chodit / vozidla"): chodci a vozíky se
/// pohybují kolem budov a po cestách. Čistě vizuální kulisa — existují jen
/// u kamery (LOD), spawnují se poblíž zástavby, mimo pohled se ruší. Simulace
/// o nich neví (render do ní nezapisuje). Počet škáluje s počtem viditelných
/// budov, ať rušné město opravdu žije.
/// </summary>
public sealed class AgentSystem
{
    private const int MaxAgents = 48;
    private const float MinZoom = 0.6f;
    private const float SpawnCooldownSeconds = 0.25f;
    private const float DespawnMargin = 120f;
    private const float PersonSpeed = 22f;
    private const float CartSpeed = 40f;

    private enum Kind
    {
        Person,
        Cart,
    }

    private struct Agent
    {
        public Vector2 Position;
        public Vector2 Target;
        public Kind Kind;
        public float Speed;
        public float Phase;
        public bool FaceLeft;
    }

    private readonly GameContent _content;
    private readonly SpriteLibrary _sprites;
    private readonly Agent[] _agents = new Agent[MaxAgents];
    private int _count;
    private float _spawnTimer;

    public AgentSystem(GameContent content, SpriteLibrary sprites)
    {
        _content = content;
        _sprites = sprites;
    }

    public void Update(float dt, Camera2D camera, Simulation simulation)
    {
        if (camera.Zoom < MinZoom || simulation.Buildings.Length == 0)
        {
            _count = 0; // oddáleno nebo prázdný svět → nikdo tu není
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        UpdateAgents(dt, simulation, min, max);
        TrySpawn(dt, camera, simulation, min, max);
    }

    /// <summary>Klik na obyvatele poblíž bodu — vrací jeho pozici (herní obrazovka pak ukáže myšlenku).</summary>
    public bool TryPokeAgent(Vector2 world, float radius, out Vector2 position)
    {
        float radiusSquared = radius * radius;
        for (int i = 0; i < _count; i++)
        {
            float dx = _agents[i].Position.X - world.X;
            float dy = _agents[i].Position.Y - world.Y;
            if (dx * dx + dy * dy <= radiusSquared)
            {
                position = _agents[i].Position;
                return true;
            }
        }

        position = Vector2.Zero;
        return false;
    }

    public void Draw(SpriteBatch spriteBatch, Camera2D camera)
    {
        if (_count == 0)
        {
            return;
        }

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int i = 0; i < _count; i++)
        {
            ref readonly var agent = ref _agents[i];
            var sprite = _sprites.Get(agent.Kind == Kind.Person ? "agent.person" : "agent.cart");
            if (sprite is null)
            {
                continue;
            }

            // Chodci lehce poskakují, vozíky drncají.
            float bob = agent.Kind == Kind.Person
                ? MathF.Abs(MathF.Sin(agent.Phase * 8f)) * 1.5f
                : MathF.Sin(agent.Phase * 14f) * 0.6f;
            var origin = new Vector2(sprite.Width * 0.5f, sprite.Height);
            var effect = agent.FaceLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            // Stín + tělo.
            spriteBatch.Draw(sprite, new Vector2(agent.Position.X, agent.Position.Y - bob), null,
                Color.White, 0f, origin, 1f, effect, 0f);
        }

        spriteBatch.End();
    }

    private void UpdateAgents(float dt, Simulation simulation, Vector2 min, Vector2 max)
    {
        for (int i = _count - 1; i >= 0; i--)
        {
            ref var agent = ref _agents[i];
            agent.Phase += dt;

            var toTarget = agent.Target - agent.Position;
            float distance = toTarget.Length();
            if (distance < 3f)
            {
                agent.Target = PickTarget(simulation, agent.Position, agent.Kind);
                toTarget = agent.Target - agent.Position;
                distance = toTarget.Length();
            }

            if (distance > 0.01f)
            {
                var step = toTarget / distance * agent.Speed * dt;
                agent.Position += step;
                agent.FaceLeft = step.X < 0f;
            }

            bool outOfView = agent.Position.X < min.X - DespawnMargin || agent.Position.X > max.X + DespawnMargin
                || agent.Position.Y < min.Y - DespawnMargin || agent.Position.Y > max.Y + DespawnMargin;
            if (outOfView)
            {
                _agents[i] = _agents[--_count];
            }
        }
    }

    private void TrySpawn(float dt, Camera2D camera, Simulation simulation, Vector2 min, Vector2 max)
    {
        _spawnTimer -= dt;
        // Cíl počtu roste s viditelnými budovami; strop drží výkon.
        int desired = Math.Min(MaxAgents, 4 + simulation.Buildings.Length);
        if (_spawnTimer > 0f || _count >= desired)
        {
            return;
        }

        _spawnTimer = SpawnCooldownSeconds;

        // Spawn poblíž náhodné budovy ve výřezu.
        var buildings = simulation.Buildings;
        var anchor = buildings[Random.Shared.Next(buildings.Length)];
        var center = new Vector2(
            (anchor.X + 0.5f) * TerrainRenderer.TileSize,
            (anchor.Y + 0.5f) * TerrainRenderer.TileSize);
        if (center.X < min.X || center.X > max.X || center.Y < min.Y || center.Y > max.Y)
        {
            return; // budova mimo pohled — spawn počká na jinou
        }

        var pos = center + new Vector2(
            (Random.Shared.NextSingle() - 0.5f) * 6f * TerrainRenderer.TileSize,
            (Random.Shared.NextSingle() - 0.5f) * 6f * TerrainRenderer.TileSize);
        if (!IsPassable(simulation, pos))
        {
            return;
        }

        // Vozíky jen když je kam jet (existují cesty); jinak chodci.
        bool cart = simulation.RoadTiles.Count > 0 && Random.Shared.NextSingle() < 0.25f;
        _agents[_count++] = new Agent
        {
            Position = pos,
            Target = PickTarget(simulation, pos, cart ? Kind.Cart : Kind.Person),
            Kind = cart ? Kind.Cart : Kind.Person,
            Speed = cart ? CartSpeed : PersonSpeed,
            Phase = Random.Shared.NextSingle() * 10f,
        };
    }

    /// <summary>Náhodný průchozí cíl poblíž; vozíky preferují cesty.</summary>
    private Vector2 PickTarget(Simulation simulation, Vector2 from, Kind kind)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var candidate = from + new Vector2(
                (Random.Shared.NextSingle() - 0.5f) * 10f * TerrainRenderer.TileSize,
                (Random.Shared.NextSingle() - 0.5f) * 10f * TerrainRenderer.TileSize);

            int tileX = (int)MathF.Floor(candidate.X / TerrainRenderer.TileSize);
            int tileY = (int)MathF.Floor(candidate.Y / TerrainRenderer.TileSize);
            bool ok = IsPassable(simulation, candidate)
                && (kind != Kind.Cart || simulation.IsRoad(tileX, tileY));
            if (ok)
            {
                return candidate;
            }
        }

        return from; // nic vhodného → počkej na místě
    }

    private bool IsPassable(Simulation simulation, Vector2 worldPos)
    {
        int tileX = (int)MathF.Floor(worldPos.X / TerrainRenderer.TileSize);
        int tileY = (int)MathF.Floor(worldPos.Y / TerrainRenderer.TileSize);
        if (simulation.IsOccupied(tileX, tileY))
        {
            return false;
        }

        return !_content.Biomes[simulation.BiomeAt(tileX, tileY)].IsWater;
    }
}
