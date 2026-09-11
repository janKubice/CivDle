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
/// k nerozeznání a síť ztrácela čitelnost přesně tam, kde je nejhustší.</para>
///
/// <para><b>Povrch se mění s érou</b> (<see cref="RoadSurface"/> z dat):
/// hliněná cesta má dvě vyjeté koleje a rozdrbaný okraj, dlážděná nepravidelný
/// kámen, asfalt hladký povrch a vodorovné značení. Silnice je nejdelší
/// souvislá čára na obrazovce; kdyby vypadala v pravěku stejně jako
/// v orbitální civilizaci, nezměnil by se dojem z města ani po tisíci letech
/// vývoje.</para>
///
/// <para>Barva z gameplay dat, culling podle výřezu. Čte jen ze simulace
/// (nekonečná mapa — silnice jsou souřadnice).</para>
/// </summary>
public sealed class RoadRenderer
{
    private const int Pad = 5;      // odsazení středového polštářku
    private const int Thickness = 6; // šířka pěšiny

    /// <summary>
    /// Od téhle éry se kreslí vodorovné značení, když data povrchy nemají.
    /// S povrchy rozhoduje druh: značení patří k asfaltu, ne k roku.
    /// </summary>
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
        var surfaceDef = _content.Gameplay.Roads.SurfaceForEra(simulation.CurrentEraIndex);
        var kind = surfaceDef?.Kind ?? RoadSurfaceKind.Paved;
        var roadColor = (surfaceDef?.Color ?? _content.Gameplay.Roads.MapColor).ToXna();
        var curb = Shade(roadColor, 0.62f);      // obrubník: tmavší lem po stranách
        var crown = Shade(roadColor, 1.22f);     // vyjetý střed vozovky
        // Most = silnice po vodě. Dřevěná deska pod cestou ho odliší od běžné pěšiny.
        var bridgeColor = new Color(122, 88, 56);

        // Značení patří k asfaltu. Bez povrchů v datech zůstává původní pravidlo
        // podle éry, aby hra s neúplným obsahem vypadala jako dřív, ne hůř.
        bool markings = detailed && (surfaceDef is null
            ? simulation.CurrentEraIndex >= MarkingsEra
            : kind == RoadSurfaceKind.Paved);

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
            bool bridge = simulation.IsBridge(tileX, tileY);
            if (bridge)
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

            // Povrch. Most má vlastní prkna a druh povrchu se na něj nevztahuje —
            // dřevěná lávka je dřevěná lávka i ve věku asfaltu.
            if (!bridge)
            {
                DrawSurface(spriteBatch, kind, tileX, tileY, x, y, east, west, south, north, surface);
            }

            bool crossing = IsCrossing(east, west, south, north);

            // Vyjetý střed. U křižovatky se vynechá — tam se místo něj kreslí
            // značka. Hliněná cesta ho nemá vůbec: místo hřebene má dvě koleje
            // od kol (kreslí je DrawSurface).
            //
            // POZOR na pořadí podmínek: dokud tohle bylo jedno if/else, spadla
            // do větve „křižovatka" každá dlaždice hliněné cesty a celá síť
            // byla posetá světlými čtverci.
            if (!crossing && (bridge || kind != RoadSurfaceKind.Dirt))
            {
                DrawCrown(spriteBatch, x, y, east, west, south, north, middle);
            }
            else if (crossing)
            {
                DrawCrossing(spriteBatch, x, y, surface);
            }

            if (markings && !crossing)
            {
                DrawMarkings(spriteBatch, x, y, east, west, south, north);
            }
        }
    }

    /// <summary>
    /// Kresba podle druhu povrchu. Tohle je ten „jak" k „co" z dat: JSON řekne
    /// <c>dirt</c>, tady se rozhodne, že to znamená koleje a hrudky.
    /// </summary>
    private void DrawSurface(
        SpriteBatch spriteBatch, RoadSurfaceKind kind, int tileX, int tileY, int x, int y,
        bool east, bool west, bool south, bool north, Color surface)
    {
        switch (kind)
        {
            case RoadSurfaceKind.Dirt:
                DrawRuts(spriteBatch, x, y, east, west, south, north, surface);
                break;
            case RoadSurfaceKind.Cobble:
                DrawCobbles(spriteBatch, tileX, tileY, x, y, surface);
                break;
            case RoadSurfaceKind.Paved:
                break; // asfalt je hladký — o jeho vzhled se stará hřeben a značení
        }
    }

    /// <summary>
    /// Dvě vyjeté koleje místo jednoho hřebene uprostřed.
    ///
    /// <para>Polní cesta nemá klenbu — má dvě rýhy od kol a mezi nimi
    /// vyšlapaný pruh. Je to jediná věc, kterou se hliněná cesta pozná od
    /// dlážděné na první pohled, a stojí dvě kresby na dlaždici.</para>
    /// </summary>
    private void DrawRuts(
        SpriteBatch spriteBatch, int x, int y,
        bool east, bool west, bool south, bool north, Color surface)
    {
        var rut = Shade(surface, 0.78f);
        const int tileSize = TerrainRenderer.TileSize;
        int center = Pad + Thickness / 2;

        if (east || west)
        {
            int from = west ? 0 : Pad;
            int to = east ? tileSize : Pad + Thickness;
            spriteBatch.Draw(_pixel, new Rectangle(x + from, y + center - 2, to - from, 1), rut);
            spriteBatch.Draw(_pixel, new Rectangle(x + from, y + center + 1, to - from, 1), rut);
        }

        if (south || north)
        {
            int from = north ? 0 : Pad;
            int to = south ? tileSize : Pad + Thickness;
            spriteBatch.Draw(_pixel, new Rectangle(x + center - 2, y + from, 1, to - from), rut);
            spriteBatch.Draw(_pixel, new Rectangle(x + center + 1, y + from, 1, to - from), rut);
        }
    }

    /// <summary>
    /// Nepravidelný kámen: pár tmavších a světlejších čtverečků podle polohy.
    ///
    /// <para>Vzorek je z hashe souřadnic, ne z náhody — dlažba se tak mezi
    /// snímky nehýbe a po znovunačtení hry vypadá ulice stejně. Kdyby byla
    /// dlažba pravidelná mřížka, četlo by se to jako textura z jiné hry;
    /// nepravidelnost je celý smysl.</para>
    /// </summary>
    private void DrawCobbles(SpriteBatch spriteBatch, int tileX, int tileY, int x, int y, Color surface)
    {
        var dark = Shade(surface, 0.84f);
        var light = Shade(surface, 1.12f);

        // Čtyři kameny na dlaždici stačí: víc už z výšky splyne v šum.
        for (int i = 0; i < 4; i++)
        {
            uint h = Hash(tileX, tileY, i);
            int px = Pad + (int)(h % (uint)Thickness);
            int py = Pad + (int)((h >> 8) % (uint)Thickness);
            spriteBatch.Draw(
                _pixel, new Rectangle(x + px, y + py, 2, 2), (h & 0x10000) == 0 ? dark : light);
        }
    }

    /// <summary>Deterministický hash dlaždice — dlažba se nesmí mezi snímky hýbat.</summary>
    private static uint Hash(int x, int y, int salt)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + salt * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }
    }

    /// <summary>
    /// Značka křižovatky: <b>ošlapaný</b> střed, ne světlá dlaždice.
    ///
    /// <para>Dřív to byl blok o dva odstíny světlejší než vozovka, velký skoro
    /// jako celý polštářek. V husté síti z toho byla mřížka krémových čtverců
    /// nalepených na cestu — čitelnost to přidalo, ale vypadalo to jako chyba.
    /// Na skutečné křižovatce se povrch naopak <i>odírá</i>: je tmavší
    /// a ušlapanější než okolní vozovka, protože po ní jezdí ze všech stran.</para>
    ///
    /// <para>Rohy zůstávají prázdné, takže skvrna čte kulatě — čtverec uprostřed
    /// čtverce je právě to, co vypadalo nalepeně.</para>
    /// </summary>
    private void DrawCrossing(SpriteBatch spriteBatch, int x, int y, Color surface)
    {
        var worn = Shade(surface, 0.86f);
        int inner = Thickness - 2;

        // Kříž místo čtverce: vodorovný a svislý pruh přes střed.
        spriteBatch.Draw(_pixel, new Rectangle(x + Pad + 1, y + Pad + 2, inner, inner - 2), worn);
        spriteBatch.Draw(_pixel, new Rectangle(x + Pad + 2, y + Pad + 1, inner - 2, inner), worn);
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
