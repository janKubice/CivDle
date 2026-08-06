using CivDle.Core.Content;
using CivDle.Core.Sim;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Značky a jména cizích měst.
///
/// <para><b>Samotné město tenhle renderer nekreslí.</b> Objevené cizí město je
/// v simulaci postavené ze skutečných budov a skutečných silnic, takže ho kreslí
/// <see cref="BuildingRenderer"/> a <see cref="RoadRenderer"/> — týmž kódem jako
/// hráčovu zástavbu. Dřív mělo cizí město vlastní kreslení z obdélníků a čar
/// a bylo to na první pohled poznat: vypadalo jako cedule „tady je město".</para>
///
/// <para>Zbývá tedy to, co ze zástavby poznat není: kostka na místě města, když
/// je hráč daleko nebo ho ještě neobjevil, prstenec kolem hranice a jméno.</para>
///
/// <para>Vrstva: čte jen ze simulace (poloha měst je čistá funkce seedu),
/// nikdy do ní nezapisuje. Mlha se kreslí až nad tímhle, takže neobjevené
/// město zůstane schované samo od sebe.</para>
/// </summary>
public sealed class NpcCityRenderer
{
    /// <summary>Pod tímhle přiblížením se města kreslí jen jako značka bez domků.</summary>
    private const float DetailZoom = 0.7f;

    /// <summary>
    /// Pod tímhle přiblížením se jméno města nevypisuje.
    ///
    /// <para>Bývalo to 0,55 a byla to chyba: při oddálení, kdy je jméno
    /// <b>nejužitečnější</b> (hledáš, kam jít), zmizelo. Teď mizí až úplně
    /// vzadu, kde by se popisky slily do řádku.</para>
    /// </summary>
    private const float LabelZoom = 0.12f;

    /// <summary>Jak daleko od kamery se ještě hledají města (v dlaždicích).</summary>
    private const int ScanRadiusTiles = NpcCityMap.CellTiles * 2;

    private readonly Texture2D _pixel;
    private readonly GameContent _content;
    private readonly Localization _loc;
    private readonly SpriteFontBase _font;

    public NpcCityRenderer(
        Texture2D whitePixel, GameContent content, Localization loc, SpriteFontBase font)
    {
        _pixel = whitePixel;
        _content = content;
        _loc = loc;
        _font = font;
    }

    /// <summary>
    /// Vykreslí značky a jména cizích měst. Volá se nad terénem a pod budovami —
    /// prstenec ani kostka nemají zakrýt zástavbu.
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
        DrawCities(spriteBatch, camera, simulation, centerTileX, centerTileY);
        spriteBatch.End();

        if (camera.Zoom >= LabelZoom)
        {
            DrawLabels(spriteBatch, camera, simulation, centerTileX, centerTileY);
        }
    }

    /// <summary>
    /// Značka města. Zblízka je vidět jeho skutečná zástavba (kreslí ji
    /// <c>BuildingRenderer</c> a <c>RoadRenderer</c> týmž kódem jako hráčovu),
    /// takže sem patří už jen to, co ze zástavby poznat není: kde město leží,
    /// když je hráč daleko, a čí je.
    /// </summary>
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

            // Pohlcené město hranici nedostane: je to hráčova čtvrť jako každá
            // jiná a rámeček kolem vlastní zástavby nedává smysl.
            if (simulation.NpcStateOf(city.Key).Absorbed)
            {
                continue;
            }

            if (!detailed || !simulation.IsCityDiscovered(city))
            {
                // Z výšky (nebo za mlhou) stačí kostka v barvě druhu — z domků by
                // na tu vzdálenost byla stejně jen kaše.
                spriteBatch.Draw(_pixel, new Rectangle(cx - 20, cy - 20, 40, 40), color * 0.9f);
                continue;
            }

            // Hranice po skutečném okraji zástavby. Pevný poloměr znamenal, že
            // u malého města visela v poli a u velkého vedla jeho středem —
            // a protože se střed města posouvá na suchou zem, byla i mimo.
            if (simulation.TryNpcTownBounds(city.Key, out var bounds))
            {
                DrawBorder(spriteBatch, bounds, color * 0.8f);
            }
        }
    }

    /// <summary>Rámeček kolem zástavby města, s dlaždicí vůle na každé straně.</summary>
    private void DrawBorder(SpriteBatch spriteBatch, NpcTownSystem.TownBounds bounds, Color color)
    {
        const int tileSize = TerrainRenderer.TileSize;
        const int width = 3;

        int left = (bounds.MinX - 1) * tileSize;
        int top = (bounds.MinY - 1) * tileSize;
        int right = (bounds.MaxX + 2) * tileSize;
        int bottom = (bounds.MaxY + 2) * tileSize;
        int w = right - left;
        int h = bottom - top;

        spriteBatch.Draw(_pixel, new Rectangle(left, top, w, width), color);
        spriteBatch.Draw(_pixel, new Rectangle(left, bottom - width, w, width), color);
        spriteBatch.Draw(_pixel, new Rectangle(left, top, width, h), color);
        spriteBatch.Draw(_pixel, new Rectangle(right - width, top, width, h), color);
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

            // Popisek patří nad zástavbu, ne nad bod z mřížky — město se při
            // stavbě posouvá na suchou zem, takže jeho střed jinde být může.
            float wx = city.X * tileSize;
            float wy = city.Y * tileSize;
            if (simulation.TryNpcTownBounds(city.Key, out var bounds))
            {
                wx = (bounds.MinX + bounds.MaxX + 1) * 0.5f * tileSize;
                wy = (bounds.MinY - 1) * tileSize;
            }

            if (wx < min.X || wx > max.X || wy < min.Y || wy > max.Y)
            {
                continue;
            }

            var state = simulation.NpcStateOf(city.Key);
            string name = _content.SettlementNames[city.NameIndex % _content.SettlementNames.Count];
            string text = state.Absorbed ? _loc.Format("npc.mineLabel", name) : name;

            var screen = camera.WorldToScreen(new Vector2(wx, wy - 1.5f * tileSize));
            var size = _font.MeasureString(text);
            var at = new Vector2(screen.X - size.X * 0.5f, screen.Y - size.Y);
            spriteBatch.DrawString(_font, text, at + new Vector2(1f, 1f), Color.Black * 0.65f);
            spriteBatch.DrawString(_font, text, at,
                state.Absorbed ? new Color(240, 205, 110) : new Color(232, 226, 208));
        }

        spriteBatch.End();
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
