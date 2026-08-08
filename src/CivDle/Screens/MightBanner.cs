using CivDle.Core;
using CivDle.Core.Content;
using CivDle.Core.Sim;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Screens;

/// <summary>
/// Velké „×N" — kolikrát je civilizace silnější, než byla na začátku.
///
/// <para>Tohle číslo ve hře chybělo a je to přesně to, kvůli čemu se žánr hraje.
/// Hráč kupoval upgrady, zkoumal technologie a musel <b>věřit</b>, že to k něčemu
/// je: nikde nebyl jeden ukazatel, na který se dá koukat a sledovat, jak roste.
/// Rozpis pod ním říká, odkud ta síla je, takže je i vidět, co se vyplatí.</para>
///
/// <para>Kreslí se ručně přes <c>DrawString</c> se škálováním, protože widgety
/// Myry velikost písma měnit neumí — a „velké" je tady celý smysl.</para>
///
/// <para>Vrstva: čte jen ze simulace, nezapisuje.</para>
/// </summary>
public sealed class MightBanner
{
    /// <summary>Kolikrát větší než běžný text je hlavní číslo.</summary>
    private const float HeadlineScale = 2.6f;

    /// <summary>Jak dlouho trvá zablikání po nárůstu.</summary>
    private const float PulseSeconds = 1.2f;

    private readonly Texture2D _pixel;
    private readonly SpriteFontBase _font;
    private readonly Localization _loc;

    private double _shown = 1.0;
    private double _lastTotal = 1.0;
    private float _pulse;

    public MightBanner(Texture2D whitePixel, SpriteFontBase font, Localization loc)
    {
        _pixel = whitePixel;
        _font = font;
        _loc = loc;
    }

    /// <summary>
    /// Dojede zobrazené číslo k tomu skutečnému a nastartuje záblesk, když
    /// vyskočilo. Číslo se dojíždí schválně: skok z ×12 na ×48 je informace,
    /// kterou oko nestihne — plynulý rozjezd je vidět.
    /// </summary>
    public void Update(float dt, Simulation simulation)
    {
        double total = simulation.TotalPower();
        if (total > _lastTotal * 1.001)
        {
            _pulse = PulseSeconds;
        }

        _lastTotal = total;
        _pulse = MathF.Max(0f, _pulse - dt);

        // Exponenciální dojezd — rychlý zezačátku, měkký na konci.
        _shown += (total - _shown) * Math.Min(1.0, dt * 3.0);
    }

    /// <summary>
    /// Krátký zlatý záblesk u horního okraje, když síla vyskočí.
    ///
    /// <para>Velké ×N uprostřed obrazovky se neosvědčilo — sedělo přes mapu.
    /// Číslo teď bydlí v panelu statů vpravo nahoře, kde ho hráč hledá; tady
    /// zbyl jen ten okamžik, kdy se něco povedlo, a ten musí být vidět
    /// i koutkem oka.</para>
    /// </summary>
    public void DrawPulse(SpriteBatch spriteBatch, Viewport viewport)
    {
        if (_pulse <= 0f)
        {
            return;
        }

        float glow = _pulse / PulseSeconds;
        string headline = "×" + Numbers.Format(_shown);
        var size = _font.MeasureString(headline) * HeadlineScale;

        float x = viewport.Width * 0.5f - size.X * 0.5f;
        float y = 96f;

        spriteBatch.Begin();
        spriteBatch.DrawString(
            _font, headline, new Vector2(x, y),
            new Color(255, 226, 150) * glow, 0f, Vector2.Zero,
            new Vector2(HeadlineScale, HeadlineScale));
        spriteBatch.End();
    }

    public void Draw(SpriteBatch spriteBatch, Viewport viewport, Simulation simulation)
    {
        string headline = "×" + Numbers.Format(_shown);
        var headlineSize = _font.MeasureString(headline) * HeadlineScale;

        var sources = simulation.PowerBreakdown();
        float x = viewport.Width * 0.5f;
        float y = 52f;

        spriteBatch.Begin();

        // Podklad, ať je číslo čitelné i nad světlou krajinou.
        int boxWidth = (int)headlineSize.X + 48;
        int boxHeight = (int)headlineSize.Y + 26;
        spriteBatch.Draw(
            _pixel,
            new Rectangle((int)(x - boxWidth * 0.5f), (int)y - 6, boxWidth, boxHeight),
            new Color(10, 14, 22) * 0.55f);

        // Záblesk po nárůstu: zlatý nádech, který během chvíle vyprchá.
        float glow = _pulse / PulseSeconds;
        var color = Color.Lerp(new Color(255, 226, 150), Color.White, glow * 0.6f);

        spriteBatch.DrawString(
            _font, headline, new Vector2(x - headlineSize.X * 0.5f, y),
            color, 0f, Vector2.Zero, new Vector2(HeadlineScale, HeadlineScale));

        // Popisek a rozpis drobným písmem pod číslem — „odkud to je".
        float lineY = y + headlineSize.Y + 2f;
        DrawCentered(spriteBatch, _loc["power.title"], x, lineY, new Color(170, 180, 196));

        lineY += _font.MeasureString("X").Y + 2f;
        for (int i = 0; i < sources.Count; i++)
        {
            // Zdroj, který nic nedělá, do rozpisu nepatří — jen by ho ředil.
            if (sources[i].Multiplier <= 1.0001)
            {
                continue;
            }

            string line = $"{_loc[sources[i].LabelKey]} ×{Numbers.Format(sources[i].Multiplier)}";
            DrawCentered(spriteBatch, line, x, lineY, new Color(140, 200, 235));
            lineY += _font.MeasureString("X").Y + 1f;
        }

        spriteBatch.End();
    }

    private void DrawCentered(SpriteBatch spriteBatch, string text, float centerX, float y, Color color)
    {
        var size = _font.MeasureString(text);
        spriteBatch.DrawString(_font, text, new Vector2(centerX - size.X * 0.5f, y), color);
    }
}
