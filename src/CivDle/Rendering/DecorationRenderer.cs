using CivDle.Core.Content;
using CivDle.Core.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Biomové dekorace (living-map.md: anti-repetice) — kytky, keře, drobnosti…
/// Nic se neukládá: výskyt, pozice, barva i velikost se určují deterministickým
/// hashem dlaždice a seedu, takže stejný svět vypadá vždy stejně.
/// LOD: při oddálení pod práh se drobnosti nekreslí (z dálky je nikdo nevidí).
/// Nekonečný terén — kreslí se přes viditelné dlaždice (i záporné).
/// </summary>
public sealed class DecorationRenderer
{
    /// <summary>Pod tímhle zoomem jsou dekorace menší než pixel — nekreslí se.</summary>

    private readonly Texture2D _pixel;
    private readonly GameContent _content;
    private readonly long _seed;

    /// <summary>Předpočítané indexy dekorací pro každý biom — vnitřní smyčka jen prochází pole.</summary>
    private readonly int[][] _decorationsByBiome;

    public DecorationRenderer(Texture2D whitePixel, GameContent content, long seed)
    {
        _pixel = whitePixel;
        _content = content;
        _seed = seed;

        _decorationsByBiome = new int[content.Biomes.Count][];
        for (int biome = 0; biome < content.Biomes.Count; biome++)
        {
            var list = new List<int>();
            for (int i = 0; i < content.Decorations.Count; i++)
            {
                if (content.Decorations[i].BiomeMask[biome])
                {
                    list.Add(i);
                }
            }

            _decorationsByBiome[biome] = list.ToArray();
        }
    }

    public void Draw(SpriteBatch spriteBatch, Camera2D camera, ITerrain terrain)
    {
        if (camera.Zoom < DetailLevel.Decorations || _content.Decorations.Count == 0)
        {
            return;
        }

        const int tileSize = TerrainRenderer.TileSize;
        var (min, max) = camera.VisibleWorldBounds();
        int startX = (int)MathF.Floor(min.X / tileSize);
        int startY = (int)MathF.Floor(min.Y / tileSize);
        int endX = (int)MathF.Ceiling(max.X / tileSize);
        int endY = (int)MathF.Ceiling(max.Y / tileSize);
        if (!DetailLevel.FitsBudget(startX, startY, endX, endY))
        {
            return;
        }

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                var defs = _decorationsByBiome[terrain.BiomeAt(x, y)];
                for (int d = 0; d < defs.Length; d++)
                {
                    var def = _content.Decorations[defs[d]];
                    ulong hash = Hash(x, y, defs[d]);

                    // Spodních 24 bitů rozhoduje o výskytu, zbytek o vzhledu.
                    if ((hash & 0xFFFFFF) / (float)0x1000000 >= def.Density)
                    {
                        continue;
                    }

                    int size = def.MinSize + (int)((hash >> 24) % (ulong)(def.MaxSize - def.MinSize + 1));
                    var color = def.Colors[(int)((hash >> 32) % (ulong)def.Colors.Count)];
                    int offsetX = (int)((hash >> 40) % (ulong)Math.Max(1, tileSize - size));
                    int offsetY = (int)((hash >> 50) % (ulong)Math.Max(1, tileSize - size));

                    spriteBatch.Draw(
                        _pixel,
                        new Rectangle(x * tileSize + offsetX, y * tileSize + offsetY, size, size),
                        color.ToXna());
                }
            }
        }

        spriteBatch.End();
    }

    private ulong Hash(int x, int y, int defIndex)
    {
        ulong h = unchecked((ulong)_seed);
        h ^= (uint)x * 0x9E3779B97F4A7C15UL;
        h ^= (uint)y * 0xBF58476D1CE4E5B9UL;
        h ^= ((ulong)defIndex + 1) * 0x94D049BB133111EBUL;
        h = (h ^ (h >> 30)) * 0xBF58476D1CE4E5B9UL;
        h = (h ^ (h >> 27)) * 0x94D049BB133111EBUL;
        return h ^ (h >> 31);
    }
}
