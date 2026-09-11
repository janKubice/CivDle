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
    /// <summary>
    /// Kolikrát větší se kreslí sprite oproti vylosované velikosti.
    ///
    /// <para>Velikosti v datech byly vymyšlené pro barevný čtvereček (1–4 px).
    /// Trs trávy o čtyřech pixelech by byl neviditelný, takže se sprite
    /// natáhne — data se přepisovat nemusí a čtvereček jako záloha zůstává
    /// ve své původní velikosti.</para>
    /// </summary>
    private const int SpriteScale = 3;

    /// <summary>Jak velká jsou oka shluků v dlaždicích.</summary>
    private const int ClumpCell = 7;

    /// <summary>Pod tímhle zoomem jsou dekorace menší než pixel — nekreslí se.</summary>

    private readonly Texture2D _pixel;
    private readonly GameContent _content;
    private readonly long _seed;
    private readonly Sprites.SpriteLibrary _sprites;

    /// <summary>Předpočítané indexy dekorací pro každý biom — vnitřní smyčka jen prochází pole.</summary>
    private readonly int[][] _decorationsByBiome;

    public DecorationRenderer(Texture2D whitePixel, GameContent content, long seed, Sprites.SpriteLibrary sprites)
    {
        _pixel = whitePixel;
        _sprites = sprites;
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

    /// <summary>
    /// Jak moc se na tomhle místě drobnostem daří (0 = holo, ~1,8 = houští).
    ///
    /// <para>Bez tohohle byla hustota v celém biomu konstantní, takže kvítí
    /// leželo po louce jako rovnoměrně rozsypané konfety. Příroda ale roste
    /// v trsech: někde je holá zem, o kus dál houští. Plynulé oko shluků to
    /// napraví a přitom nestojí nic — je to týž hash jako všechno ostatní,
    /// jen na hrubší mřížce.</para>
    /// </summary>
    private float Clump(int x, int y, int decoration)
    {
        int cx = (int)MathF.Floor(x / (float)ClumpCell);
        int cy = (int)MathF.Floor(y / (float)ClumpCell);
        float fx = Smooth((x - cx * ClumpCell) / (float)ClumpCell);
        float fy = Smooth((y - cy * ClumpCell) / (float)ClumpCell);

        float a = Corner(cx, cy, decoration);
        float b = Corner(cx + 1, cy, decoration);
        float c = Corner(cx, cy + 1, decoration);
        float d = Corner(cx + 1, cy + 1, decoration);

        float value = MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, d, fx), fy);

        // Rozsah 0–1,8: holá místa jsou opravdu holá a houští znatelně hustší
        // než rovnoměrné rozsypání, ale průměr zůstane kolem původní hustoty.
        return value * 1.8f;
    }

    private float Corner(int cx, int cy, int decoration) =>
        (Hash(cx * 7919, cy * 104729, decoration) & 0xFFFF) / 65535f;

    private static float Smooth(float t) => t * t * (3f - 2f * t);

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
                    // Hustota se násobí shlukem: příroda neroste rovnoměrně.
                    if ((hash & 0xFFFFFF) / (float)0x1000000 >= def.Density * Clump(x, y, defs[d]))
                    {
                        continue;
                    }

                    int size = def.MinSize + (int)((hash >> 24) % (ulong)(def.MaxSize - def.MinSize + 1));
                    var color = def.Colors[(int)((hash >> 32) % (ulong)def.Colors.Count)];
                    int offsetX = (int)((hash >> 40) % (ulong)Math.Max(1, tileSize - size));
                    int offsetY = (int)((hash >> 50) % (ulong)Math.Max(1, tileSize - size));

                    // Obrázek, pokud ho data mají. Čtvereček zůstává jako
                    // záloha — barevná tečka je šum, ne porost, ale pořád je
                    // lepší než prázdné místo tam, kde sprite chybí.
                    if (def.HasSprite && _sprites.Get(def.Sprite!) is { } sprite)
                    {
                        // Sprite je vyšší než široký (tráva roste nahoru), takže
                        // se posadí patou na místo, které vyšlo z hashe.
                        int drawn = size * SpriteScale;
                        spriteBatch.Draw(
                            sprite,
                            new Rectangle(
                                x * tileSize + offsetX - drawn / 3,
                                y * tileSize + offsetY - drawn + size,
                                drawn,
                                drawn),
                            color.ToXna());
                        continue;
                    }

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
