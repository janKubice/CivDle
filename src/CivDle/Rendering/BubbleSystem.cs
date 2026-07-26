using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Sběrné bubliny nad vyrábějícími budovami: čas od času se objeví bublina s dávkou
/// suroviny, kterou hráč posbírá klikem (aktivní vrstva nad idle jádrem — netrestá,
/// jen odměňuje). Sběr je příkaz hráče (píše do simulace přes <c>AddResource</c>);
/// zbytek je čistě render.
/// </summary>
public sealed class BubbleSystem
{
    // Bublin bylo na obrazovce moc a klikání se změnilo v otravu. Řídší spawn +
    // méně bublin naráz, ale každá nese víc — čistý příjem zůstává, šum mizí.
    private const float SpawnIntervalSeconds = 26f;
    private const int MaxBubbles = 2;
    private const float LifeSeconds = 16f;
    private const int OutputCyclesPerBubble = 34; // kolik cyklů výstupu bublina nese
    private const float CollectRadiusTiles = 1.0f;
    private const float MinZoom = 0.5f;

    private sealed class Bubble
    {
        public int ResourceIndex;
        public int Amount;
        public float WorldX;
        public float WorldY;
        public float Age;
        public string IconKey = string.Empty;
    }

    private readonly SpriteLibrary _sprites;
    private readonly GameContent _content;
    private readonly List<Bubble> _bubbles = new();
    private readonly Random _rng = new();
    private float _spawnTimer = SpawnIntervalSeconds;

    public BubbleSystem(SpriteLibrary sprites, GameContent content)
    {
        _sprites = sprites;
        _content = content;
    }

    public void Update(float dt, Simulation simulation)
    {
        for (int i = _bubbles.Count - 1; i >= 0; i--)
        {
            _bubbles[i].Age += dt;
            if (_bubbles[i].Age >= LifeSeconds)
            {
                _bubbles.RemoveAt(i);
            }
        }

        _spawnTimer -= dt;
        if (_spawnTimer <= 0f)
        {
            _spawnTimer = SpawnIntervalSeconds;
            if (_bubbles.Count < MaxBubbles)
            {
                TrySpawn(simulation);
            }
        }
    }

    /// <summary>Posbírá bublinu poblíž bodu (příkaz hráče). Vrací odměnu k zobrazení.</summary>
    public bool TryCollect(Vector2 world, Simulation simulation, out int resourceIndex, out int amount, out Vector2 position)
    {
        float radius = CollectRadiusTiles * TerrainRenderer.TileSize;
        for (int i = 0; i < _bubbles.Count; i++)
        {
            var bubble = _bubbles[i];
            float dx = world.X - bubble.WorldX, dy = world.Y - bubble.WorldY;
            if (dx * dx + dy * dy <= radius * radius)
            {
                simulation.AddResource(bubble.ResourceIndex, bubble.Amount);
                resourceIndex = bubble.ResourceIndex;
                amount = bubble.Amount;
                position = new Vector2(bubble.WorldX, bubble.WorldY);
                _bubbles.RemoveAt(i);
                return true;
            }
        }

        resourceIndex = 0;
        amount = 0;
        position = Vector2.Zero;
        return false;
    }

    public void Draw(SpriteBatch spriteBatch, Camera2D camera)
    {
        if (camera.Zoom < MinZoom || _bubbles.Count == 0)
        {
            return;
        }

        var bubbleSprite = _sprites.Get("fx.bubble");
        int tileSize = TerrainRenderer.TileSize;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        foreach (var bubble in _bubbles)
        {
            float wobble = MathF.Sin(bubble.Age * 3f) * 3f;
            float fade = bubble.Age > LifeSeconds - 2f ? (LifeSeconds - bubble.Age) / 2f : 1f;
            var center = new Vector2(bubble.WorldX, bubble.WorldY + wobble);

            if (bubbleSprite is not null)
            {
                DrawCentered(spriteBatch, bubbleSprite, center, tileSize * 1.3f, Color.White * fade);
            }

            var icon = _sprites.Get(bubble.IconKey);
            if (icon is not null)
            {
                DrawCentered(spriteBatch, icon, center, tileSize * 0.8f, Color.White * fade);
            }
        }

        spriteBatch.End();
    }

    private static void DrawCentered(SpriteBatch spriteBatch, Texture2D texture, Vector2 center, float size, Color color)
    {
        spriteBatch.Draw(texture, center, null, color, 0f,
            new Vector2(texture.Width * 0.5f, texture.Height * 0.5f), size / texture.Width, SpriteEffects.None, 0f);
    }

    private void TrySpawn(Simulation simulation)
    {
        var buildings = simulation.Buildings;
        if (buildings.Length == 0)
        {
            return;
        }

        // Pár pokusů najít vyrábějící budovu (s receptem).
        for (int attempt = 0; attempt < 5; attempt++)
        {
            int index = _rng.Next(buildings.Length);
            var building = buildings[index];
            var def = _content.Buildings[building.DefIndex];
            if (def.Recipe is null || def.Recipe.Outputs.Count == 0)
            {
                continue;
            }

            var output = def.Recipe.Outputs[0];
            _bubbles.Add(new Bubble
            {
                ResourceIndex = output.ResourceIndex,
                Amount = output.Amount * OutputCyclesPerBubble,
                WorldX = (building.X + def.FootprintWidth * 0.5f) * TerrainRenderer.TileSize,
                WorldY = building.Y * TerrainRenderer.TileSize - TerrainRenderer.TileSize * 0.4f,
                IconKey = $"icon.{_content.Resources[output.ResourceIndex].Id}",
            });
            return;
        }
    }
}
