using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Screens;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Capture;

/// <summary>
/// Stránka kroniky: půdorys města z časosběru a pod ním pár vět o tom, jak
/// k němu došlo — jeden obrázek, který se dá ukázat i tomu, kdo hru nehraje.
///
/// <para>Proč obrázek a ne jen text na obrazovce: hráč staví hodiny, pak
/// Vzestupem všechno smaže a nezbude mu z toho <b>nic</b>, co by šlo poslat
/// dál. Stránka je ta jediná věc, která z dlouhé tiché práce udělá něco
/// přenosného.</para>
///
/// <para>Kreslí se z <see cref="CityHistory"/>, ne ze scény: časosběr drží
/// půdorys i barvy, takže stránka nezávisí na tom, kam se hráč zrovna dívá,
/// ani na tom, jak velké má okno.</para>
///
/// <para>Vrstva: render nad simulací. Nic nemění.</para>
/// </summary>
public sealed class ChroniclePage
{
    /// <summary>Šířka stránky v pixelech — pevná, aby obrázek vypadal vždycky stejně.</summary>
    public const int Width = 1200;

    /// <summary>Výška stránky v pixelech.</summary>
    public const int Height = 1500;

    /// <summary>Kolik místa nahoře zabere plán města.</summary>
    private const int PlanHeight = 760;

    private const int Padding = 64;

    private static readonly Color Paper = new(28, 26, 22);
    private static readonly Color Ink = new(228, 222, 206);
    private static readonly Color InkDim = new(168, 162, 148);
    private static readonly Color Gold = new(255, 226, 150);
    private static readonly Color PlanBackground = new(18, 20, 24);

    private readonly ScreenManager _screens;

    public ChroniclePage(ScreenManager screens) => _screens = screens;

    /// <summary>
    /// Vykreslí stránku a uloží ji do složky. Vrací cestu k souboru — bez ní
    /// by obrázek vznikl někde, kde ho nikdo nenajde.
    /// </summary>
    /// <param name="history">Časosběr, ze kterého se stránka skládá.</param>
    /// <param name="lines">Hotové věty kroniky (viz <see cref="ChronicleWriter"/>).</param>
    /// <param name="cityName">Jméno města do nadpisu.</param>
    /// <param name="directory">Kam soubor uložit.</param>
    public string Save(
        CityHistory history, IReadOnlyList<ChronicleLine> lines, string cityName, string directory)
    {
        var device = _screens.GraphicsDevice;
        using var target = new RenderTarget2D(device, Width, Height);

        device.SetRenderTarget(target);
        device.Clear(Paper);
        Draw(history, lines, cityName);
        device.SetRenderTarget(null);

        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"civdle-kronika-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        using var stream = File.Create(path);
        target.SaveAsPng(stream, Width, Height);
        return path;
    }

    private void Draw(CityHistory history, IReadOnlyList<ChronicleLine> lines, string cityName)
    {
        var spriteBatch = _screens.SpriteBatch;
        var pixel = _screens.WhitePixel;
        var font = Myra.Graphics2D.UI.Styles.Stylesheet.Current.LabelStyle.Font;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        spriteBatch.DrawString(font, cityName, new Vector2(Padding, 48), Gold, scale: new Vector2(2.4f, 2.4f));
        spriteBatch.DrawString(
            font, _screens.Loc["chronicle.page.title"], new Vector2(Padding, 112), InkDim,
            scale: new Vector2(1.2f, 1.2f));

        DrawPlan(history, spriteBatch, pixel);
        DrawLines(lines, spriteBatch, pixel, font);

        // Podpis vpravo dole, ať je z obrázku poznat, odkud je.
        var brand = font.MeasureString("CivDle") * 1.2f;
        spriteBatch.DrawString(
            font, "CivDle", new Vector2(Width - Padding - brand.X, Height - 56), InkDim,
            scale: new Vector2(1.2f, 1.2f));

        spriteBatch.End();
    }

    /// <summary>
    /// Půdorys z posledního snímku časosběru — jeden texel na buňku, zvětšený
    /// na celočíselný násobek.
    ///
    /// <para>Kreslí se jen výřez, ve kterém něco stojí. Celá mřížka je čtverec
    /// o 256 buňkách na stranu a město bývá v jejím rohu — bez oříznutí by na
    /// stránce byla čtvrtina baráků a tři čtvrtiny prázdna.</para>
    /// </summary>
    private void DrawPlan(CityHistory history, SpriteBatch spriteBatch, Texture2D pixel)
    {
        var area = new Rectangle(Padding, 160, Width - 2 * Padding, PlanHeight);
        spriteBatch.Draw(pixel, area, PlanBackground);

        if (history.Count == 0)
        {
            return;
        }

        var cells = history.CellsAt(history.Count - 1);
        var palette = history.Palette;
        if (!TryBounds(cells, out var bounds))
        {
            return;
        }

        // Celočíselné zvětšení: půlpixelové buňky by z rovných ulic udělaly
        // roztřepenou kaši.
        int scale = Math.Max(1, Math.Min(area.Width / bounds.Width, area.Height / bounds.Height));
        int originX = area.X + (area.Width - bounds.Width * scale) / 2;
        int originY = area.Y + (area.Height - bounds.Height * scale) / 2;

        for (int y = 0; y < bounds.Height; y++)
        {
            for (int x = 0; x < bounds.Width; x++)
            {
                byte value = cells[(bounds.Y + y) * CityHistory.GridSize + bounds.X + x];
                if (value == 0 || value > palette.Count)
                {
                    continue;
                }

                var color = palette[value - 1];
                spriteBatch.Draw(
                    pixel,
                    new Rectangle(originX + x * scale, originY + y * scale, scale, scale),
                    new Color(color.R, color.G, color.B));
            }
        }
    }

    /// <summary>Nejmenší obdélník, ve kterém leží celá zástavba; false = prázdný snímek.</summary>
    private static bool TryBounds(ReadOnlySpan<byte> cells, out Rectangle bounds)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int y = 0; y < CityHistory.GridSize; y++)
        {
            for (int x = 0; x < CityHistory.GridSize; x++)
            {
                if (cells[y * CityHistory.GridSize + x] == 0)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (minX > maxX)
        {
            bounds = Rectangle.Empty;
            return false;
        }

        // Okraj kolem zástavby, ať město nesedí těsně u rámu.
        const int Margin = 2;
        minX = Math.Max(0, minX - Margin);
        minY = Math.Max(0, minY - Margin);
        maxX = Math.Min(CityHistory.GridSize - 1, maxX + Margin);
        maxY = Math.Min(CityHistory.GridSize - 1, maxY + Margin);

        bounds = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }

    private void DrawLines(
        IReadOnlyList<ChronicleLine> lines, SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font)
    {
        int y = 160 + PlanHeight + 56;
        spriteBatch.Draw(pixel, new Rectangle(Padding, y - 28, Width - 2 * Padding, 2), Gold * 0.5f);

        for (int i = 0; i < lines.Count && y < Height - 110; i++)
        {
            spriteBatch.DrawString(
                font, ChronicleText.Of(lines[i], _screens.Loc, _screens.Content),
                new Vector2(Padding, y), Ink, scale: new Vector2(1.3f, 1.3f));
            y += 54;
        }
    }
}

/// <summary>
/// Překlad jedné věty kroniky do textu.
///
/// <para>Vlastní třída, protože stejnou větu potřebuje stránka i obrazovka —
/// a kdyby si ji každá skládala po svém, rozešel by se obrázek s tím, co hráč
/// před chvílí četl.</para>
/// </summary>
public static class ChronicleText
{
    /// <summary>Věta se všemi doplněnými čísly a jménem éry.</summary>
    public static string Of(ChronicleLine line, Localization loc, GameContent content)
    {
        string era = line.EraIndex >= 0 && line.EraIndex < content.Eras.Count
            ? loc[content.Eras[line.EraIndex].NameKey]
            : string.Empty;

        return loc.Format(
            line.TextKey,
            CivDle.Core.Numbers.Format(line.Value),
            DurationFormat.Human(line.Seconds),
            era);
    }
}
