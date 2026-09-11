using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;

namespace CivDle.Screens;

/// <summary>
/// Panel, který vypadá jako panel, ne jako obarvený obdélník.
///
/// <para><b>Proč to vzniklo:</b> celé rozhraní kreslilo panely jedním plným
/// odstínem — sedmdesát míst, sedmdesátkrát <c>SolidBrush</c>. Plná plocha
/// nemá povrch: nedá se z ní poznat, co je nahoře a co dole, ani kde jeden
/// panel končí a druhý začíná. HUD je přitom v každém screenshotu, takže je to
/// jediná věc, kterou divák uvidí i tehdy, když se na hru dívá pět vteřin.</para>
///
/// <para>Tři tahy, z nichž je povrch: <b>svislý přechod</b> (nahoře světleji,
/// dole tmavěji — ke světlu je obrácená horní hrana, stejně jako všude jinde
/// ve hře), <b>světlá linka nahoře</b> a <b>tmavá dole</b>. Je to tentýž trik
/// jako u silnic a u brázd na poli: tři odstíny místo jednoho.</para>
///
/// <para>Kreslí se po vodorovných pruzích, protože Myra umí vyplnit jen
/// jednolitý obdélník. Pruhů je tak akorát: při méně je vidět schodiště, při
/// více se platí draw cally za rozdíl, který nikdo nepozná.</para>
///
/// <para>Vrstva: UI. O hře neví nic.</para>
/// </summary>
internal sealed class PanelBrush : IBrush
{
    /// <summary>Na kolik pruhů se přechod skládá.</summary>
    private const int Bands = 12;

    /// <summary>O kolik je horní okraj světlejší a dolní tmavší než zadaná barva.</summary>
    private const float Range = 0.14f;

    private readonly Color _base;
    private readonly Color _top;
    private readonly Color _bottom;
    private readonly Color _highlight;
    private readonly Color _shadow;

    public PanelBrush(Color color)
    {
        _base = color;
        _top = Scale(color, 1f + Range);
        _bottom = Scale(color, 1f - Range);

        // Linky jdou dál než přechod: bez nich by byl panel měkký a rozplynul
        // by se do scény. Tohle je ta hrana, za kterou oko panel chytí.
        _highlight = Scale(color, 1f + Range * 2.4f);
        _shadow = Scale(color, 1f - Range * 2.2f);
    }

    /// <summary>Barva, ze které panel vychází. Kvůli testům a odvozeným odstínům.</summary>
    public Color BaseColor => _base;

    public void Draw(RenderContext context, Rectangle destination, Color color)
    {
        if (destination.Width <= 0 || destination.Height <= 0)
        {
            return;
        }

        // Přechod. Pruhy se počítají z celé výšky, ne po jednom pixelu, takže
        // vysoký panel nestojí víc než nízký.
        for (int i = 0; i < Bands; i++)
        {
            var (top, bottom) = BandBounds(i, Bands, destination.Y, destination.Height);
            if (bottom <= top)
            {
                continue;
            }

            var shade = Color.Lerp(_top, _bottom, i / (Bands - 1f));
            context.FillRectangle(
                new Rectangle(destination.X, top, destination.Width, bottom - top), Modulate(shade, color));
        }

        // Horní světlo a spodní stín — hrana, kterou panel drží tvar.
        context.FillRectangle(
            new Rectangle(destination.X, destination.Y, destination.Width, 1), Modulate(_highlight, color));
        context.FillRectangle(
            new Rectangle(destination.X, destination.Bottom - 1, destination.Width, 1), Modulate(_shadow, color));
    }

    /// <summary>
    /// Kde leží <paramref name="index"/>-tý pruh přechodu.
    ///
    /// <para>Počítá se z celé výšky (<c>height * i / count</c>), ne přičítáním
    /// pevné tloušťky: při dělení se zbytkem by poslední pruh nedosáhl na
    /// spodní okraj a pod panelem by prosvítal proužek scény. Zároveň tím
    /// pruhy nikdy nepřetečou ven.</para>
    ///
    /// <para>Veřejné, protože je to jediná počítaná část štětce — kreslení
    /// samo potřebuje Myru a grafické zařízení, tohle ne.</para>
    /// </summary>
    public static (int Top, int Bottom) BandBounds(int index, int count, int top, int height)
    {
        int from = top + height * index / count;
        int to = top + height * (index + 1) / count;
        return (from, to);
    }

    /// <summary>
    /// Myra předává barvu, kterou se má štětec zabarvit (třeba při zeslabení
    /// neaktivního prvku). Bílá znamená „nech být", takže se násobí.
    /// </summary>
    private static Color Modulate(Color shade, Color tint) => tint == Color.White
        ? shade
        : new Color(shade.R * tint.R / 255, shade.G * tint.G / 255, shade.B * tint.B / 255, shade.A * tint.A / 255);

    /// <summary>Světlejší nebo tmavší odstín téže barvy. Alfa se nemění — panel má zůstat stejně krytý.</summary>
    private static Color Scale(Color color, float factor) => new(
        (int)Math.Clamp(color.R * factor, 0, 255),
        (int)Math.Clamp(color.G * factor, 0, 255),
        (int)Math.Clamp(color.B * factor, 0, 255),
        color.A);
}
