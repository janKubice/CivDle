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
/// mechaniky. Proto renderer kreslí přímo do světa.</para>
///
/// <para>Objevené město se kreslí <b>svými skutečnými budovami</b> (týmiž sprity,
/// jaké staví hráč) a <b>skutečnými silnicemi</b>. Dřív to byly barevné obdélníky
/// a čáry — vypadalo to jako cedule „tady je město", ne jako město, a po pohlcení
/// se nedalo předat nic, protože tam nic nestálo.</para>
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

    /// <summary>Jak dlouho trvá povozu projet ulici cizího města.</summary>
    private const float TownCartSeconds = 14f;

    private static readonly Color RoadColor = new(126, 106, 78);
    private static readonly Color CaravanColor = new(214, 186, 132);

    private readonly Texture2D _pixel;
    private readonly GameContent _content;
    private readonly Localization _loc;
    private readonly SpriteFontBase _font;
    private readonly Sprites.SpriteLibrary _sprites;
    private float _time;

    public NpcCityRenderer(
        Texture2D whitePixel, GameContent content, Localization loc, SpriteFontBase font,
        Sprites.SpriteLibrary sprites)
    {
        _pixel = whitePixel;
        _content = content;
        _loc = loc;
        _font = font;
        _sprites = sprites;
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

    /// <summary>
    /// Samotná města. Zblízka se kreslí jejich skutečné budovy a ulice, z dálky
    /// jen značka v barvě druhu — na tu vzdálenost by z domků byla stejně kaše.
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

            var town = simulation.TownOf(city);
            if (!detailed || town is null)
            {
                // Z výšky (nebo za mlhou) stačí kostka v barvě druhu.
                spriteBatch.Draw(_pixel, new Rectangle(cx - 20, cy - 20, 40, 40), color * 0.9f);
                continue;
            }

            DrawTownRoads(spriteBatch, town);
            DrawTownBuildings(spriteBatch, town, color);
            DrawTownCarts(spriteBatch, town);

            // Vlastní města dostanou zlatý prstenec — hráč pozná, co už je jeho.
            if (simulation.NpcStateOf(city.Key).Absorbed)
            {
                DrawRing(spriteBatch, cx, cy, 3 * tileSize, new Color(240, 205, 110));
            }
        }
    }

    /// <summary>
    /// Povozy v ulici cizího města. Objevené město, ve kterém se nic nehýbe,
    /// vypadá jako makety — pár vozíků na hlavní ulici stačí, aby žilo.
    /// </summary>
    private void DrawTownCarts(SpriteBatch spriteBatch, NpcTown town)
    {
        if (town.Roads.Count < 4)
        {
            return;
        }

        const int tileSize = TerrainRenderer.TileSize;
        int carts = 1 + (int)((ulong)town.Key & 1);

        for (int i = 0; i < carts; i++)
        {
            // Každý vozík má vlastní fázi z klíče města, ať nejedou v zákrytu.
            float offset = ((town.Key >> (i * 5)) & 0x3F) / 64f;
            float phase = (_time / TownCartSeconds + offset) % 2f;
            float t = phase < 1f ? phase : 2f - phase;

            var from = town.Roads[0];
            var to = town.Roads[town.Roads.Count - 1];
            float x = (from.X + (to.X - from.X) * t + 0.5f) * tileSize;
            float y = (from.Y + (to.Y - from.Y) * t + 0.5f) * tileSize;

            spriteBatch.Draw(_pixel, new Rectangle((int)x - 4, (int)y - 2, 8, 5), CaravanColor);
            spriteBatch.Draw(_pixel, new Rectangle((int)x - 4, (int)y + 3, 8, 1), Color.Black * 0.3f);
        }
    }

    /// <summary>Ulice cizího města — týmiž dlaždicemi, jaké staví hráč.</summary>
    private void DrawTownRoads(SpriteBatch spriteBatch, NpcTown town)
    {
        const int tileSize = TerrainRenderer.TileSize;
        var roadColor = _content.Gameplay.Roads.MapColor.ToXna();

        for (int i = 0; i < town.Roads.Count; i++)
        {
            var road = town.Roads[i];
            spriteBatch.Draw(
                _pixel,
                new Rectangle(road.X * tileSize + 4, road.Y * tileSize + 4, tileSize - 8, tileSize - 8),
                roadColor);
        }
    }

    /// <summary>
    /// Budovy cizího města. Kreslí se týmiž sprity jako hráčovy, jen s lehkým
    /// nádechem barvy druhu — aby bylo poznat, čí to je, a přitom to vypadalo
    /// jako opravdové město, ne jako jiná hra.
    /// </summary>
    private void DrawTownBuildings(SpriteBatch spriteBatch, NpcTown town, Color tint)
    {
        const int tileSize = TerrainRenderer.TileSize;
        var shade = Color.Lerp(Color.White, tint, 0.25f);

        for (int i = 0; i < town.Buildings.Count; i++)
        {
            var planned = town.Buildings[i];
            var def = _content.Buildings[planned.DefIndex];
            int x = planned.X * tileSize;
            int y = planned.Y * tileSize;
            int width = def.FootprintWidth * tileSize;
            int height = def.FootprintHeight * tileSize;

            var sprite = _sprites.Get($"building.{def.Id}");
            if (sprite is not null)
            {
                spriteBatch.Draw(_pixel, new Rectangle(x + 2, y + height - 3, width - 2, 3), Color.Black * 0.25f);
                spriteBatch.Draw(sprite, new Rectangle(x, y, width, height), shade);
            }
            else
            {
                spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), Color.Black * 0.6f);
                spriteBatch.Draw(
                    _pixel,
                    new Rectangle(x + 2, y + 2, width - 4, height - 4),
                    def.MapColor.ToXna());
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
            string name = _content.SettlementNames[city.NameIndex % _content.SettlementNames.Count];
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
