using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Zlaté spawny („golden cookie"): občas se někde ve viditelném výřezu na pár vteřin
/// zatřpytí zlatý objekt; klik na něj dá velkou dávku náhodné suroviny. Nutí hráče
/// koukat na svět a lovit klikem. Sběr je příkaz hráče (píše do simulace); zbytek
/// je čistě render. Vždy nanejvýš jeden aktivní.
/// </summary>
public sealed class GoldenSpawnSystem
{
    private const float MinSpawnGap = 28f;
    private const float MaxSpawnGap = 55f;
    private const float LifeSeconds = 7f;
    private const float CollectRadiusTiles = 1.1f;
    private const float MinZoom = 0.5f;

    private readonly SpriteLibrary _sprites;
    private readonly GameContent _content;
    private readonly Random _rng = new();

    private bool _active;
    private float _worldX;
    private float _worldY;
    private float _age;
    private float _nextSpawnTimer;

    public GoldenSpawnSystem(SpriteLibrary sprites, GameContent content)
    {
        _sprites = sprites;
        _content = content;
        _nextSpawnTimer = MaxSpawnGap;
    }

    public void Update(float dt, Camera2D camera, Simulation simulation)
    {
        if (_active)
        {
            _age += dt;
            if (_age >= LifeSeconds)
            {
                _active = false;
                _nextSpawnTimer = NextGap();
            }

            return;
        }

        _nextSpawnTimer -= dt;
        if (_nextSpawnTimer <= 0f && camera.Zoom >= MinZoom)
        {
            Spawn(camera, simulation);
        }
    }

    /// <summary>Posbírá zlatý spawn poblíž bodu (příkaz hráče) → velká dávka náhodné suroviny.</summary>
    public bool TryCollect(Vector2 world, Simulation simulation, out int resourceIndex, out int amount, out Vector2 position)
    {
        resourceIndex = 0;
        amount = 0;
        position = Vector2.Zero;
        if (!_active)
        {
            return false;
        }

        float radius = CollectRadiusTiles * TerrainRenderer.TileSize;
        float dx = world.X - _worldX, dy = world.Y - _worldY;
        if (dx * dx + dy * dy > radius * radius)
        {
            return false;
        }

        resourceIndex = _rng.Next(simulation.ResourceCount);
        amount = (int)Math.Max(15, simulation.GetStorageCap(resourceIndex) * 0.08);
        simulation.AddResource(resourceIndex, amount);
        position = new Vector2(_worldX, _worldY);
        _active = false;
        _nextSpawnTimer = NextGap();
        return true;
    }

    public void Draw(SpriteBatch spriteBatch, Camera2D camera)
    {
        if (!_active || camera.Zoom < MinZoom)
        {
            return;
        }

        var sprite = _sprites.Get("fx.golden");
        if (sprite is null)
        {
            return;
        }

        float pulse = 1f + 0.15f * MathF.Sin(_age * 6f);
        float fade = _age > LifeSeconds - 1.5f ? (LifeSeconds - _age) / 1.5f : 1f;
        float size = TerrainRenderer.TileSize * 1.3f * pulse;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        spriteBatch.Draw(sprite, new Vector2(_worldX, _worldY), null, Color.White * fade, 0f,
            new Vector2(sprite.Width * 0.5f, sprite.Height * 0.5f), size / sprite.Width, SpriteEffects.None, 0f);
        spriteBatch.End();
    }

    private void Spawn(Camera2D camera, Simulation simulation)
    {
        var (min, max) = camera.VisibleWorldBounds();
        int tileSize = TerrainRenderer.TileSize;
        int minX = (int)(min.X / tileSize) + 1, maxX = (int)(max.X / tileSize) - 1;
        int minY = (int)(min.Y / tileSize) + 1, maxY = (int)(max.Y / tileSize) - 1;
        if (maxX <= minX || maxY <= minY)
        {
            _nextSpawnTimer = 2f;
            return;
        }

        // Pár pokusů najít suchou dlaždici, ať spawn nesedí na moři.
        for (int attempt = 0; attempt < 8; attempt++)
        {
            int tileX = _rng.Next(minX, maxX);
            int tileY = _rng.Next(minY, maxY);
            if (!_content.Biomes[simulation.BiomeAt(tileX, tileY)].IsWater)
            {
                _worldX = (tileX + 0.5f) * tileSize;
                _worldY = (tileY + 0.5f) * tileSize;
                _age = 0f;
                _active = true;
                return;
            }
        }

        _nextSpawnTimer = 3f; // samá voda — zkus to za chvíli znovu
    }

    private float NextGap() => MinSpawnGap + (float)_rng.NextDouble() * (MaxSpawnGap - MinSpawnGap);
}
