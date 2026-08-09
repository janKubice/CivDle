using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Vykreslení silnic. Každá dlaždice má středový polštářek a ramena k sousedním
/// silnicím či budovám — síť tak vypadá jako spojité pěšiny, ne šachovnice.
///
/// <para>Silnice se nekreslí jednou barvou, ale <b>třemi vrstvami</b>:
/// obrubník (tmavý lem), vozovka a světlejší střed. Je to tentýž trik, kterým
/// se v pixel artu kreslí cokoli oblého — sama plná barva vypadá jako
/// vyplněný obdélník, tři odstíny přes sebe jako cesta. Stojí to dvě kresby
/// navíc na dlaždici, ale silnice je to, po čem hráč vodí oči nejvíc, protože
/// mu drží tvar města.</para>
///
/// <para>Křižovatky dostanou vlastní značku: bez ní byly křížení a rovný úsek
/// k nerozeznání a síť ztrácela čitelnost přesně tam, kde je nejhustší.
/// V pozdějších érách přibude vodorovné značení — dlážděná cesta a dálnice
/// mají vypadat jinak.</para>
///
/// <para>Barva z gameplay dat, culling podle výřezu. Čte jen ze simulace
/// (nekonečná mapa — silnice jsou souřadnice).</para>
/// </summary>
public sealed class RoadRenderer
{
    private const int Pad = 5;      // odsazení středového polštářku
    private const int Thickness = 6; // šířka pěšiny

    /// <summary>Od téhle éry se na silnice kreslí vodorovné značení.</summary>
    public const int MarkingsEra = 4;

    /// <summary>Pod tímhle přiblížením se kreslí jen holá vozovka bez detailů.</summary>
    public const float DetailZoom = 0.8f;

    /// <summary>
    /// Pod tímhle přiblížením se ze silnic stane <b>kresba sítě</b>: každá
    /// dlaždice se vyplní celá.
    ///
    /// <para>Šestipixelový polštářek má z výšky pod jeden pixel na obrazovce
    /// a síť se z něj rozpadne na tečky. Přitom právě silniční síť je to
    /// jediné, podle čeho jde z výšky poznat tvar města — takže se z ní radši
    /// stane plná čára. Není to zjednodušení kvůli výkonu, ale <b>jiná kresba
    /// pro jinou vzdálenost</b>.</para>
    /// </summary>
    public const float NetworkZoom = CityScaleRenderer.ThresholdZoom;

    private readonly Texture2D _pixel;
    private readonly GameContent _content;

    public RoadRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        _content = content;
    }

    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        var (min, max) = camera.VisibleWorldBounds();
        bool detailed = camera.Zoom >= DetailLevel.Scale(DetailZoom);
        bool network = camera.Zoom < NetworkZoom;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        if (network)
        {
            DrawNetwork(spriteBatch, simulation.RoadTiles, min, max);
            DrawNetwork(spriteBatch, simulation.NpcRoadTiles, min, max);
            spriteBatch.End();
            return;
        }

        DrawTiles(spriteBatch, simulation, simulation.RoadTiles, min, max, detailed);

        // Ulice cizích měst a cesty mezi nimi. Jsou to tytéž silniční dlaždice,
        // takže se kreslí týmž kódem — dřív to byly čáry přes mapu a bylo na
        // první pohled poznat, že to nejsou silnice, po kterých se dá jet.
        DrawTiles(spriteBatch, simulation, simulation.NpcRoadTiles, min, max, detailed);
        spriteBatch.End();
    }

    /// <summary>
    /// Silnice z výšky: plná dlaždice ve světlé barvě, žádná ramena a žádné
    /// dotazy na sousedy. Je to mapa sítě, ne vozovka — a jako mapa se má taky
    /// kreslit.
    /// </summary>
    private void DrawNetwork(
        SpriteBatch spriteBatch, IReadOnlyList<RoadTile> roadTiles, Vector2 min, Vector2 max)
    {
        const int tileSize = TerrainRenderer.TileSize;
        var color = Shade(_content.Gameplay.Roads.MapColor.ToXna(), 1.35f);

        for (int i = 0; i < roadTiles.Count; i++)
        {
            int x = roadTiles[i].X * tileSize;
            int y = roadTiles[i].Y * tileSize;
            if (x + tileSize < min.X || x > max.X || y + tileSize < min.Y || y > max.Y)
            {
                continue;
            }

            spriteBatch.Draw(_pixel, new Rectangle(x, y, tileSize, tileSize), color * 0.85f);
        }
    }

    /// <summary>Vykreslí jeden seznam silničních dlaždic. Vlastník na vzhled nemá vliv.</summary>
    private void DrawTiles(
        SpriteBatch spriteBatch, Simulation simulation, IReadOnlyList<RoadTile> roadTiles,
        Vector2 min, Vector2 max, bool detailed)
    {
        const int tileSize = TerrainRenderer.TileSize;
        var roadColor = _content.Gameplay.Roads.MapColor.ToXna();
        var curb = Shade(roadColor, 0.62f);      // obrubník: tmavší lem po stranách
        var crown = Shade(roadColor, 1.22f);     // vyjetý střed vozovky
        // Most = silnice po vodě. Dřevěná deska pod cestou ho odliší od běžné pěšiny.
        var bridgeColor = new Color(122, 88, 56);
        bool markings = detailed && simulation.CurrentEraIndex >= MarkingsEra;

        for (int i = 0; i < roadTiles.Count; i++)
        {
            int tileX = roadTiles[i].X;
            int tileY = roadTiles[i].Y;
            int x = tileX * tileSize;
            int y = tileY * tileSize;
            if (x + tileSize < min.X || x > max.X || y + tileSize < min.Y || y > max.Y)
            {
                continue;
            }

            var surface = roadColor;
            var edge = curb;
            var middle = crown;
            if (simulation.IsBridge(tileX, tileY))
            {
                // Podklad mostu přes celou dlaždici, ať je nad vodou čitelný.
                spriteBatch.Draw(_pixel, new Rectangle(x, y, tileSize, tileSize), bridgeColor);
                surface = new Color(168, 132, 92);
                edge = Shade(surface, 0.66f);
                middle = Shade(surface, 1.18f);
            }

            bool east = Connects(simulation, tileX + 1, tileY);
            bool west = Connects(simulation, tileX - 1, tileY);
            bool south = Connects(simulation, tileX, tileY + 1);
            bool north = Connects(simulation, tileX, tileY - 1);

            // Obrubník: tatáž ramena o pixel širší a tmavší, kreslená pod vozovku.
            if (detailed)
            {
                DrawShape(spriteBatch, x, y, east, west, south, north, edge, grow: 1);
            }

            DrawShape(spriteBatch, x, y, east, west, south, north, surface, grow: 0);

            if (!detailed)
            {
                continue;
            }

            // Vyjetý střed. U křižovatky se vynechá — tam se místo něj kreslí
            // značka, jinak by z toho byla jen světlejší skvrna.
            bool crossing = IsCrossing(east, west, south, north);
            if (!crossing)
            {
                DrawCrown(spriteBatch, x, y, east, west, south, north, middle);
            }
            else
            {
                spriteBatch.Draw(_pixel, new Rectangle(x + Pad + 1, y + Pad + 1, Thickness - 2, Thickness - 2), middle);
            }

            if (markings && !crossing)
            {
                DrawMarkings(spriteBatch, x, y, east, west, south, north);
            }
        }
    }

    /// <summary>Střed dlaždice a ramena k sousedům, volitelně o <paramref name="grow"/> px širší.</summary>
    private void DrawShape(
        SpriteBatch spriteBatch, int x, int y,
        bool east, bool west, bool south, bool north, Color color, int grow)
    {
        int pad = Pad - grow;
        int thickness = Thickness + 2 * grow;

        spriteBatch.Draw(_pixel, new Rectangle(x + pad, y + pad, thickness, thickness), color);

        const int tileSize = TerrainRenderer.TileSize;
        if (east)
        {
            spriteBatch.Draw(_pixel, new Rectangle(x + pad + thickness, y + pad, tileSize - pad - thickness, thickness), color);
        }

        if (west)
        {
            spriteBatch.Draw(_pixel, new Rectangle(x, y + pad, pad, thickness), color);
        }

        if (south)
        {
            spriteBatch.Draw(_pixel, new Rectangle(x + pad, y + pad + thickness, thickness, tileSize - pad - thickness), color);
        }

        if (north)
        {
            spriteBatch.Draw(_pixel, new Rectangle(x + pad, y, thickness, pad), color);
        }
    }

    /// <summary>Světlejší pruh uprostřed vozovky — jen v ose, kterou cesta vede.</summary>
    private void DrawCrown(
        SpriteBatch spriteBatch, int x, int y,
        bool east, bool west, bool south, bool north, Color color)
    {
        const int tileSize = TerrainRenderer.TileSize;
        int center = Pad + Thickness / 2;

        if (east || west)
        {
            int from = west ? 0 : Pad;
            int to = east ? tileSize : Pad + Thickness;
            spriteBatch.Draw(_pixel, new Rectangle(x + from, y + center - 1, to - from, 2), color);
        }

        if (south || north)
        {
            int from = north ? 0 : Pad;
            int to = south ? tileSize : Pad + Thickness;
            spriteBatch.Draw(_pixel, new Rectangle(x + center - 1, y + from, 2, to - from), color);
        }
    }

    /// <summary>Přerušované vodorovné značení (pozdní éry).</summary>
    private void DrawMarkings(
        SpriteBatch spriteBatch, int x, int y, bool east, bool west, bool south, bool north)
    {
        var paint = new Color(238, 232, 210) * 0.75f;
        int center = Pad + Thickness / 2;

        if (east || west)
        {
            spriteBatch.Draw(_pixel, new Rectangle(x + 3, y + center, 4, 1), paint);
            spriteBatch.Draw(_pixel, new Rectangle(x + 10, y + center, 4, 1), paint);
        }
        else if (south || north)
        {
            spriteBatch.Draw(_pixel, new Rectangle(x + center, y + 3, 1, 4), paint);
            spriteBatch.Draw(_pixel, new Rectangle(x + center, y + 10, 1, 4), paint);
        }
    }

    /// <summary>
    /// Je to křižovatka? Tedy napojení ve <b>třech a víc</b> směrech, nebo
    /// zatáčka. Rovný úsek a slepý konec ne — u těch by značka jen šuměla.
    /// </summary>
    public static bool IsCrossing(bool east, bool west, bool south, bool north)
    {
        int count = (east ? 1 : 0) + (west ? 1 : 0) + (south ? 1 : 0) + (north ? 1 : 0);
        if (count >= 3)
        {
            return true;
        }

        // Zatáčka: dvě ramena, ale ne proti sobě.
        return count == 2 && !(east && west) && !(south && north);
    }

    /// <summary>Ztmavení/zesvětlení barvy po složkách (obrubník, vyjetý střed).</summary>
    private static Color Shade(Color color, float factor) => new(
        (byte)Math.Clamp(color.R * factor, 0f, 255f),
        (byte)Math.Clamp(color.G * factor, 0f, 255f),
        (byte)Math.Clamp(color.B * factor, 0f, 255f));

    /// <summary>
    /// Rameno se kreslí k sousední silnici i k budově (vizuální napojení na vchod).
    /// Cizí ulice a domy se počítají taky — jinak by se hráčova silnice před
    /// cizím městem zastavila a mezi nimi zůstala mezera.
    /// </summary>
    private static bool Connects(Simulation simulation, int x, int y) =>
        simulation.HasRoadAt(x, y) || simulation.IsOccupied(x, y) || simulation.IsNpcOccupied(x, y);
}
