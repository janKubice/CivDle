using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Screens;

/// <summary>
/// Bublina s popiskem kreslená <b>u kurzoru</b> — pro prvky, které nejsou Myra
/// widgety (uzly tech stromu, dlaždice mapy). Myra widgety mají vlastní tooltip
/// (<c>Widget.Tooltip</c>), který se také zobrazuje u myši; tahle třída dělá totéž
/// pro věci kreslené přímo SpriteBatchem, ať je chování v celé hře stejné.
///
/// <para>Bublina se sama drží uvnitř okna: když by přetekla vpravo/dolů, překlopí
/// se na druhou stranu kurzoru. Text se láme na slova podle <see cref="MaxWidth"/>.</para>
/// </summary>
public static class HoverTooltip
{
    /// <summary>Maximální šířka textu v pixelech (delší popis se zalomí).</summary>
    public const int MaxWidth = 320;

    private const int PaddingX = 10;
    private const int PaddingY = 8;
    private const int CursorGap = 18;
    private const int LineGap = 2;

    private static readonly Color Fill = new(16, 20, 28, 244);
    private static readonly Color Border = new(90, 110, 135);
    private static readonly Color TitleColor = new(235, 235, 245);
    private static readonly Color BodyColor = new(185, 195, 210);

    /// <summary>
    /// Vykreslí bublinu u kurzoru. Volá se <b>po</b> <c>Desktop.Render()</c>, aby
    /// popisek nezmizel pod panely; otevírá si vlastní <see cref="SpriteBatch"/> dávku.
    /// </summary>
    /// <param name="body">Volitelný delší popis pod nadpisem (může být prázdný).</param>
    /// <param name="accent">Barva nadpisu — nese stav (dostupné / zamčené / hotové).</param>
    public static void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        SpriteFontBase font,
        Viewport viewport,
        Point cursor,
        string title,
        string? body = null,
        Color? accent = null)
    {
        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body))
        {
            return;
        }

        var lines = new List<(string Text, Color Color)>();
        AppendWrapped(lines, font, title, accent ?? TitleColor);
        AppendWrapped(lines, font, body, BodyColor);
        if (lines.Count == 0)
        {
            return;
        }

        float width = 0f, height = 0f;
        foreach (var line in lines)
        {
            var size = font.MeasureString(line.Text);
            width = Math.Max(width, size.X);
            height += size.Y + LineGap;
        }

        var box = new Rectangle(
            cursor.X + CursorGap,
            cursor.Y + CursorGap,
            (int)width + PaddingX * 2,
            (int)height + PaddingY * 2);

        // Překlopení místo oříznutí — u pravého/dolního okraje je bublina vlevo/nad kurzorem.
        if (box.Right > viewport.Width) box.X = Math.Max(0, cursor.X - CursorGap - box.Width);
        if (box.Bottom > viewport.Height) box.Y = Math.Max(0, cursor.Y - CursorGap - box.Height);

        spriteBatch.Begin();
        spriteBatch.Draw(pixel, box, Fill);
        DrawBorder(spriteBatch, pixel, box, Border);

        float y = box.Y + PaddingY;
        foreach (var line in lines)
        {
            spriteBatch.DrawString(font, line.Text, new Vector2(box.X + PaddingX, y), line.Color);
            y += font.MeasureString(line.Text).Y + LineGap;
        }

        spriteBatch.End();
    }

    /// <summary>Hladové lámání na slova — dlouhý popis se vejde do bubliny místo přetečení.</summary>
    private static void AppendWrapped(List<(string, Color)> lines, SpriteFontBase font, string? text, Color color)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string current = string.Empty;
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = current.Length == 0 ? word : current + ' ' + word;
            if (current.Length > 0 && font.MeasureString(candidate).X > MaxWidth)
            {
                lines.Add((current, color));
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        if (current.Length > 0)
        {
            lines.Add((current, color));
        }
    }

    private static void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle box, Color color)
    {
        spriteBatch.Draw(pixel, new Rectangle(box.X, box.Y, box.Width, 1), color);
        spriteBatch.Draw(pixel, new Rectangle(box.X, box.Bottom - 1, box.Width, 1), color);
        spriteBatch.Draw(pixel, new Rectangle(box.X, box.Y, 1, box.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(box.Right - 1, box.Y, 1, box.Height), color);
    }
}
