using CivDle.Core.Content;
using CivDle.Core.Sim;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Cizí města na mapě — a to, co se mezi nimi děje.
///
/// <para>Do téhle chvíle byla cizí města jen položkou v seznamu sídel. Sídlo,
/// které není vidět, se ale nedá „objevit" — a objevování je celý smysl té
/// mechaniky. Proto renderer kreslí přímo do světa: shluk domků v barvě druhu
/// města, prapor se jménem a cesty ke spřáteleným sousedům.</para>
///
/// <para>Mezi cizími městy vedou vlastní cesty a po nich chodí karavany. Nemá
/// to žádný mechanický dopad na hráče a je to schválně: svět má vypadat, že
/// existoval dřív, než tam hráč přišel.</para>
///
/// <para>Vrstva: čte jen ze simulace (poloha měst je čistá funkce seedu),
/// nikdy do ní nezapisuje. Mlha se kreslí až nad tímhle, takže neobjevené
/// město zůstane schované samo od sebe.</para>
/// </summary>
public sealed class NpcCityRenderer
{
    /// <summary>Pod tímhle přiblížením se města kreslí jen jako značka bez domků.</summary>
    private const float DetailZoom = 0.7f;

    /// <summary>Pod tímhle přiblížením se jméno města nevypisuje — stejně by bylo nečitelné.</summary>
    private const float LabelZoom = 0.55f;

    /// <summary>Jak daleko od kamery se ještě hledají města (v dlaždicích).</summary>
    private const int ScanRadiusTiles = NpcCityMap.CellTiles * 2;

    /// <summary>Jak dlouho trvá karavaně cesta z jednoho města do druhého (v sekundách).</summary>
    private const float CaravanTripSeconds = 26f;

    private static readonly Color RoadColor = new(126, 106, 78);
    private static readonly Color CaravanColor = new(214, 186, 132);

    /// <summary>Rozmístění domků ve shluku — pevné, ať město nepoletuje mezi snímky.</summary>
    private static readonly (int Dx, int Dy, int W, int H)[] Houses =
    {
        (-2, -1, 2, 2), (1, -2, 2, 2), (0, 1, 2, 2), (-3, 1, 1, 1), (2, 1, 1, 1), (-1, -3, 1, 1),
    };

    private readonly Texture2D _pixel;
    private readonly GameContent _content;
    private readonly Localization _loc;
    private readonly SpriteFontBase _font;
    private float _time;

    public NpcCityRenderer(Texture2D whitePixel, GameContent content, Localization loc, SpriteFontBase font)
    {
        _pixel = whitePixel;
        _content = content;
        _loc = loc;
        _font = font;
    }

    /// <summary>Posun karavan. Jediný stav rendereru — simulace o nich neví.</summary>
    public void Update(float dt) => _time += dt;

    /// <summary>
    /// Vykreslí cesty mezi cizími městy, karavany na nich a samotná města.
    /// Volá se nad terénem a pod hráčovými budovami — cizí město nemá zakrýt to,
    /// co hráč postavil.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        if (!simulation.NpcCitiesEnabled)
        {
            return;
        }

        int centerTileX = (int)(camera.Position.X / TerrainRenderer.TileSize);
        int centerTileY = (int)(camera.Position.Y / TerrainRenderer.TileSize);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        DrawLinks(spriteBatch, simulation, centerTileX, centerTileY);
        DrawCities(spriteBatch, camera, simulation, centerTileX, centerTileY);
        spriteBatch.End();

        if (camera.Zoom >= LabelZoom)
        {
            DrawLabels(spriteBatch, camera, simulation, centerTileX, centerTileY);
        }
    }

    /// <summary>Cesty mezi cizími městy a karavany, které po nich jezdí.</summary>
    private void DrawLinks(SpriteBatch spriteBatch, Simulation simulation, int centerTileX, int centerTileY)
    {
        foreach (var link in simulation.CityLinksNear(centerTileX, centerTileY, ScanRadiusTiles))
        {
            var from = Center(link.From);
            var to = Center(link.To);
            DrawLine(spriteBatch, from, to, RoadColor * 0.75f, 4f);

            // Karavana jede tam a zpátky. Fáze se počítá z klíčů měst, takže
            // každá cesta má vlastní rytmus a nejedou všechny v zákrytu.
            float offset = ((link.From.Key ^ link.To.Key) & 0xFF) / 255f;
            float phase = (_time / CaravanTripSeconds + offset) % 2f;
            float t = phase < 1f ? phase : 2f - phase;
            var at = Vector2.Lerp(from, to, t);
            spriteBatch.Draw(_pixel, new Rectangle((int)at.X - 3, (int)at.Y - 3, 6, 6), CaravanColor);
        }
    }

    /// <summary>Samotná města: shluk domků v barvě druhu, nebo jen značka z dálky.</summary>
    private void DrawCities(
        SpriteBatch spriteBatch, Camera2D camera, Simulation simulation, int centerTileX, int centerTileY)
    {
        const int tileSize = TerrainRenderer.TileSize;
        bool detailed = camera.Zoom >= DetailZoom;

        foreach (var city in simulation.CitiesNear(centerTileX, centerTileY, ScanRadiusTiles))
        {
            var archetype = _content.NpcCities.Archetypes[city.ArchetypeIndex];
            var color = archetype.MapColor.ToXna();
            int cx = city.X * tileSize + tileSize / 2;
            int cy = city.Y * tileSize + tileSize / 2;

            if (!detailed)
            {
                // Z výšky stačí kostka v barvě druhu — hráč má vidět, že tam něco je.
                spriteBatch.Draw(_pixel, new Rectangle(cx - 20, cy - 20, 40, 40), color * 0.9f);
                continue;
            }

            // Půda pod městem: sešlapaná plocha odliší cizí sídlo od louky.
            spriteBatch.Draw(_pixel, new Rectangle(cx - 5 * tileSize / 2, cy - 5 * tileSize / 2,
                5 * tileSize, 5 * tileSize), new Color(96, 84, 62) * 0.35f);

            foreach (var (dx, dy, w, h) in Houses)
            {
                var rect = new Rectangle(
                    cx + dx * tileSize / 2, cy + dy * tileSize / 2, w * tileSize / 2, h * tileSize / 2);
                spriteBatch.Draw(_pixel, new Rectangle(rect.X + 2, rect.Y + 3, rect.Width, rect.Height),
                    Color.Black * 0.25f); // stín, ať domky sedí na zemi
                spriteBatch.Draw(_pixel, rect, color);
                spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, Math.Max(2, rect.Height / 3)),
                    Color.Lerp(color, Color.White, 0.35f)); // střecha
            }

            // Vlastní města dostanou zlatý prstenec — hráč pozná, co už je jeho.
            if (simulation.NpcStateOf(city.Key).Absorbed)
            {
                DrawRing(spriteBatch, cx, cy, 3 * tileSize, new Color(240, 205, 110));
            }
        }
    }

    /// <summary>Jména objevených měst. Neobjevené zůstávají bezejmenné — jinak by mlha nic neskrývala.</summary>
    private void DrawLabels(
        SpriteBatch spriteBatch, Camera2D camera, Simulation simulation, int centerTileX, int centerTileY)
    {
        const int tileSize = TerrainRenderer.TileSize;
        var (min, max) = camera.VisibleWorldBounds();

        spriteBatch.Begin();
        foreach (var city in simulation.CitiesNear(centerTileX, centerTileY, ScanRadiusTiles))
        {
            if (!simulation.IsCityDiscovered(city))
            {
                continue;
            }

            float wx = city.X * tileSize;
            float wy = city.Y * tileSize;
            if (wx < min.X || wx > max.X || wy < min.Y || wy > max.Y)
            {
                continue;
            }

            var state = simulation.NpcStateOf(city.Key);
            string name = _content.NpcCities.Names[city.NameIndex];
            string text = state.Absorbed ? _loc.Format("npc.mineLabel", name) : name;

            var screen = camera.WorldToScreen(new Vector2(wx, wy - 3f * tileSize));
            var size = _font.MeasureString(text);
            var at = new Vector2(screen.X - size.X * 0.5f, screen.Y - size.Y);
            spriteBatch.DrawString(_font, text, at + new Vector2(1f, 1f), Color.Black * 0.65f);
            spriteBatch.DrawString(_font, text, at,
                state.Absorbed ? new Color(240, 205, 110) : new Color(232, 226, 208));
        }

        spriteBatch.End();
    }

    private static Vector2 Center(in NpcCity city) => new(
        city.X * TerrainRenderer.TileSize + TerrainRenderer.TileSize / 2f,
        city.Y * TerrainRenderer.TileSize + TerrainRenderer.TileSize / 2f);

    /// <summary>Úsečka z jednoho bílého pixelu — natažený a otočený obdélník.</summary>
    private void DrawLine(SpriteBatch spriteBatch, Vector2 from, Vector2 to, Color color, float thickness)
    {
        var delta = to - from;
        float length = delta.Length();
        if (length < 1f)
        {
            return;
        }

        spriteBatch.Draw(
            _pixel, from, null, color, MathF.Atan2(delta.Y, delta.X),
            new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    private void DrawRing(SpriteBatch spriteBatch, int cx, int cy, int radius, Color color)
    {
        const int width = 3;
        spriteBatch.Draw(_pixel, new Rectangle(cx - radius, cy - radius, radius * 2, width), color);
        spriteBatch.Draw(_pixel, new Rectangle(cx - radius, cy + radius - width, radius * 2, width), color);
        spriteBatch.Draw(_pixel, new Rectangle(cx - radius, cy - radius, width, radius * 2), color);
        spriteBatch.Draw(_pixel, new Rectangle(cx + radius - width, cy - radius, width, radius * 2), color);
    }
}
