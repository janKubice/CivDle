using CivDle.Core.Content;
using CivDle.Core.Sim;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Čtvrti na mapě: jemné zabarvení země pod shlukem, orámování a cedule se
/// jménem uprostřed.
///
/// <para>Proč to stojí za vlastní vrstvu: bez ní je oddálené město anonymní kaše
/// budov. S cedulemi se hráč dívá na <b>místa</b> — „támhle je průmyslová čtvrť,
/// tady rezidenční" — a to je přesně ten obraz civilizace rozprostřené po mapě,
/// kvůli kterému se v idle hře oddaluje.</para>
///
/// <para>Kreslí se pod budovy (je to barva země, ne přes ně) a jen ve výřezu
/// kamery. Čte jen ze simulace — render do ní nikdy nepíše (CLAUDE.md).</para>
/// </summary>
public sealed class DistrictRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    /// <summary>Síla zabarvení země zblízka. Záměrně sotva znatelná — je to kulisa, ne UI.</summary>
    private const float FillAlpha = 0.13f;

    /// <summary>
    /// Síla zabarvení z výšky.
    ///
    /// <para>Zblízka se hráč dívá na domy a barevná plocha pod nimi má jen
    /// šeptat. Z výšky ale domy zmizí a zůstane hnědozelená kaše — a přesně
    /// tam se z barevných ploch čtvrtí stává <b>hlavní kresba</b>: mapa, na
    /// které je vidět, že tady je průmysl a tamhle se bydlí. Proto síla
    /// s oddálením roste, místo aby jako všechno ostatní klesala.</para>
    /// </summary>
    private const float FarFillAlpha = 0.42f;

    private const int Border = 2;

    /// <summary>Pod tímhle přiblížením se cedule nekreslí — text by byl stejně nečitelný.</summary>
    private const float MinLabelZoom = 0.5f;

    /// <summary>Pod tímhle přiblížením se drobnosti čtvrtí nekreslí.</summary>
    private const float MinPropZoom = 0.9f;

    private readonly Texture2D _pixel;
    private readonly GameContent _content;
    private readonly Localization _loc;
    private readonly SpriteFontBase _font;
    private readonly Sprites.SpriteLibrary _sprites;

    public DistrictRenderer(
        Texture2D whitePixel, GameContent content, Localization loc, SpriteFontBase font,
        Sprites.SpriteLibrary sprites)
    {
        _pixel = whitePixel;
        _content = content;
        _loc = loc;
        _font = font;
        _sprites = sprites;
    }

    /// <summary>Vykreslí zabarvení a rámy čtvrtí (pod budovami).</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        var districts = simulation.Districts;
        if (districts.Count == 0)
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        float fill = FillFor(camera.Zoom);

        // Drobnosti jen zblízka. Z výšky mají šestnáct pixelů pod rozlišením
        // a stálo by to stovky kreseb za obraz, který nikdo neuvidí.
        bool props = camera.Zoom >= DetailLevel.Scale(MinPropZoom);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int i = 0; i < districts.Count; i++)
        {
            var district = districts[i];
            var (px, py, pw, ph) = PixelBounds(district);
            if (px + pw < min.X || px > max.X || py + ph < min.Y || py > max.Y)
            {
                continue;
            }

            var type = _content.Districts.Types[district.TypeIndex];
            var color = type.MapColor.ToXna();
            spriteBatch.Draw(_pixel, new Rectangle(px, py, pw, ph), color * fill);
            spriteBatch.Draw(_pixel, new Rectangle(px, py, pw, Border), color * 0.5f);
            spriteBatch.Draw(_pixel, new Rectangle(px, py + ph - Border, pw, Border), color * 0.5f);
            spriteBatch.Draw(_pixel, new Rectangle(px, py, Border, ph), color * 0.5f);
            spriteBatch.Draw(_pixel, new Rectangle(px + pw - Border, py, Border, ph), color * 0.5f);

            if (props)
            {
                DrawProps(spriteBatch, simulation, type, district, min, max);
            }
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Drobnosti, které dají čtvrti tvář: bedny a trubky u průmyslu, plot
    /// a lavička u obytné.
    ///
    /// <para><b>Proč nestačí barva:</b> čtvrti se od sebe lišily jen jemným
    /// nádechem země. Nádech je ale <i>značka</i>, ne <i>místo</i> — hráč se
    /// podle něj dozví, jak se čtvrť jmenuje, ale ulice pořád vypadá jako
    /// každá jiná. Trubky mezi halami poznají průmysl i tomu, kdo se na cedule
    /// nedívá.</para>
    ///
    /// <para>Pozice jsou z hashe dlaždice, takže se nic neukládá a mezi snímky
    /// se drobnosti nehýbou. Na zastavěnou dlaždici se nic nesype — prkna
    /// prorůstající halou by vypadala jako chyba vykreslování.</para>
    /// </summary>
    private void DrawProps(
        SpriteBatch spriteBatch, Simulation simulation, DistrictTypeDef type, District district,
        Vector2 min, Vector2 max)
    {
        if (!type.HasProps || _sprites.Get(type.Prop!) is not { } sprite)
        {
            return;
        }

        // Jen průnik čtvrti s výřezem. Velkoměstská čtvrť má klidně dvě stě
        // dlaždic na stranu — projít ji celou by znamenalo desítky tisíc
        // dotazů za snímek kvůli hrstce bedýnek, které jsou vidět.
        int fromX = Math.Max(district.MinX, (int)MathF.Floor(min.X / TileSize));
        int toX = Math.Min(district.MaxX, (int)MathF.Ceiling(max.X / TileSize));
        int fromY = Math.Max(district.MinY, (int)MathF.Floor(min.Y / TileSize));
        int toY = Math.Min(district.MaxY, (int)MathF.Ceiling(max.Y / TileSize));

        for (int ty = fromY; ty <= toY; ty++)
        {
            for (int tx = fromX; tx <= toX; tx++)
            {
                uint hash = Hash(tx, ty, district.TypeIndex);
                if ((hash & 0xFFFF) / 65535f >= type.PropDensity)
                {
                    continue;
                }

                // Volná zem, ne střecha. A ne silnice: co leží na cestě, tam
                // by překáželo i ve skutečnosti.
                if (simulation.IsOccupied(tx, ty) || simulation.HasRoadAt(tx, ty))
                {
                    continue;
                }

                int size = TileSize - 2;
                int offsetX = (int)((hash >> 16) % 5) - 2;
                int offsetY = (int)((hash >> 20) % 5) - 2;
                spriteBatch.Draw(
                    sprite,
                    new Rectangle(tx * TileSize + offsetX, ty * TileSize + offsetY, size, size),
                    Color.White);
            }
        }
    }

    /// <summary>Deterministický hash dlaždice — drobnosti se nesmí mezi snímky hýbat.</summary>
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
    /// Cedule se jmény. Kreslí se zvlášť a nad budovami — jméno místa má být
    /// čitelné, i když je pod ním hustá zástavba.
    /// </summary>
    public void DrawLabels(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        var districts = simulation.Districts;
        if (districts.Count == 0 || camera.Zoom < DetailLevel.Scale(MinLabelZoom))
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();

        spriteBatch.Begin();
        for (int i = 0; i < districts.Count; i++)
        {
            var district = districts[i];
            var (px, py, pw, _) = PixelBounds(district);
            if (px + pw < min.X || px > max.X || py < min.Y || py > max.Y)
            {
                continue;
            }

            string text = _loc.Format(
                "building.inDistrict",
                _loc[_content.Districts.Types[district.TypeIndex].NameKey],
                district.BuildingCount);

            var screen = camera.WorldToScreen(new Vector2(district.CenterX * TileSize, py));
            var size = _font.MeasureString(text);
            var at = new Vector2(screen.X - size.X * 0.5f, screen.Y - size.Y - 4);

            // Stín pod textem: jméno musí být čitelné nad světlým i tmavým terénem.
            spriteBatch.DrawString(_font, text, at + new Vector2(1f, 1f), Color.Black * 0.6f);
            spriteBatch.DrawString(_font, text, at, new Color(236, 228, 206));
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Jak silně se čtvrť obarví při daném přiblížení. Plynule, ne skokem —
    /// zlom uprostřed otáčení kolečkem by byl vidět jako bliknutí.
    /// </summary>
    public static float FillFor(float zoom)
    {
        const float near = 1.0f;
        float t = Math.Clamp((near - zoom) / (near - CityScaleRenderer.ThresholdZoom), 0f, 1f);
        return FillAlpha + (FarFillAlpha - FillAlpha) * t;
    }

    private static (int X, int Y, int Width, int Height) PixelBounds(in District district) => (
        district.MinX * TileSize,
        district.MinY * TileSize,
        (district.MaxX - district.MinX + 1) * TileSize,
        (district.MaxY - district.MinY + 1) * TileSize);
}
