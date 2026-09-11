using CivDle.Core.Content;
using CivDle.Core.Sim;
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
    /// <summary>Jak velká jsou oka shluků v dlaždicích.</summary>
    private const int ClumpCell = 7;

    /// <summary>Pod tímhle zoomem jsou dekorace menší než pixel — nekreslí se.</summary>

    private readonly Texture2D _pixel;
    private readonly GameContent _content;
    private readonly long _seed;
    private readonly Sprites.SpriteLibrary _sprites;

    /// <summary>
    /// Čas pro vítr. Jediný stav rendereru — dekorace se jinak celé odvozují
    /// z hashe a nic si nepamatují.
    /// </summary>
    private float _time;

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

    /// <summary>Posune vítr. Bez toho by porost stál jako vylisovaný v herbáři.</summary>
    public void Update(float dt) => _time += dt;

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

    /// <param name="occupancy">
    /// Co na dlaždici stojí. Bez toho rostly stromy skrz střechy a přes
    /// silnice: dekorace se kreslí podle hashe dlaždice, a ten o zástavbě
    /// neví. Hráč pak měl v lese strom, který nešlo pokácet ani přestavět —
    /// protože to nebyl strom, ale kulisa nalepená přes město.
    /// </param>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, ITerrain terrain, Simulation? occupancy = null)
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
                // Na zastavěné dlaždici neroste nic. Dotaz je O(1) a ptá se
                // jednou za dlaždici, ne jednou za dekoraci.
                if (occupancy is not null
                    && (occupancy.IsOccupied(x, y) || occupancy.HasRoadAt(x, y) || occupancy.IsNpcOccupied(x, y)))
                {
                    continue;
                }

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
                        // se posadí patou na místo, které vyšlo z hashe. Zvětšení
                        // je z dat: trs trávy a strom nemají být stejně velké.
                        int drawn = size * def.Scale;
                        var target = new Rectangle(
                            x * tileSize + offsetX - drawn / 3,
                            y * tileSize + offsetY - drawn + size,
                            drawn,
                            drawn);

                        // Rostliny se kolébají ve stejném větru jako kouř nad
                        // komíny a stromy v hájích. Kdyby měl každý svůj, viděl
                        // by hráč tři nezávislé animace místo jednoho počasí.
                        if (def.SwaysInWind)
                        {
                            DrawSwaying(spriteBatch, sprite, target, SpriteTint(color), x, y);
                            continue;
                        }

                        spriteBatch.Draw(sprite, target, SpriteTint(color));
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

    /// <summary>
    /// Nakreslí porost nakloněný větrem.
    ///
    /// <para>Otáčí se kolem <b>paty</b>, ne kolem středu: rostlina je
    /// zakořeněná a kymácí se jí vrcholek. Otáčení kolem středu by ji nechalo
    /// poskakovat po zemi a vypadalo by to jako chyba, ne jako vítr.</para>
    /// </summary>
    private void DrawSwaying(
        SpriteBatch spriteBatch, Texture2D sprite, Rectangle target, Color tint, int tileX, int tileY)
    {
        float angle = AmbientWind.Sway(tileX, tileY, _time);

        // Počátek v dolním středu obrázku, cíl posunutý na totéž místo —
        // tím se osa otáčení přesune ke kořenům.
        var origin = new Vector2(sprite.Width * 0.5f, sprite.Height);
        float scale = target.Width / (float)sprite.Width;

        spriteBatch.Draw(
            sprite,
            new Vector2(target.X + target.Width * 0.5f, target.Bottom),
            null,
            tint,
            angle,
            origin,
            scale,
            SpriteEffects.None,
            0f);
    }

    /// <summary>
    /// Barva z dat přepočtená na nádech pro sprite.
    ///
    /// <para><b>Proč to nejde vzít rovnou:</b> tint se <b>násobí</b>. Barvy
    /// v datech byly vymyšlené jako výsledná barva čtverečku, takže jsou tmavé —
    /// les má v datech <c>#2E5C26</c>. Vynásobit tmavě zelený sprite tmavě
    /// zelenou barvou dá skoro černou: porost, který se obrázkem konečně dal
    /// poznat, by zmizel v tmavé kaši.</para>
    ///
    /// <para>Řešení je vzít z dat <b>odstín</b> a jas nechat spritu: barva se
    /// roztáhne tak, aby nejsilnější složka byla na maximu. Rozdíl mezi tajgou
    /// a džunglí tím zůstane, ale kresba si udrží vlastní světla a stíny.</para>
    /// </summary>
    private static Color SpriteTint(RgbColor rgb)
    {
        var color = rgb.ToXna();
        int peak = Math.Max(color.R, Math.Max(color.G, color.B));
        if (peak == 0)
        {
            return Color.White; // černá v datech není přání, aby sprite zmizel
        }

        float boost = 255f / peak;
        return new Color(
            (int)MathF.Min(255f, color.R * boost),
            (int)MathF.Min(255f, color.G * boost),
            (int)MathF.Min(255f, color.B * boost));
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
