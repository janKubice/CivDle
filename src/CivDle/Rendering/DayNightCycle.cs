using CivDle.Core.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Denní/noční cyklus jako levný overlay (living-map.md: jedna barevná vrstva
/// přes scénu, žádné přepočítávání spritů). Čas dodává simulace, barvy a síly
/// jsou v datech; tady je jen křivka dne — kód = jak, data = co.
/// </summary>
public static class DayNightCycle
{
    // Svítání ~06:00 (t 0.25), soumrak ~19:00 (t 0.79); rampy ±7 % dne.
    private const float SunriseStart = 0.18f;
    private const float SunriseEnd = 0.32f;
    private const float SunsetStart = 0.72f;
    private const float SunsetEnd = 0.86f;

    /// <summary>Síla noci 0–1 (1 = hluboká noc). Plynulé rampy při svítání a soumraku.</summary>
    public static float NightFactor(double timeOfDay01)
    {
        float t = (float)timeOfDay01;
        if (t < SunriseStart || t >= SunsetEnd)
        {
            return 1f;
        }

        if (t < SunriseEnd)
        {
            return 1f - SmoothStep((t - SunriseStart) / (SunriseEnd - SunriseStart));
        }

        if (t < SunsetStart)
        {
            return 0f;
        }

        return SmoothStep((t - SunsetStart) / (SunsetEnd - SunsetStart));
    }

    /// <summary>Síla zlatavého nádechu 0–1 — vrcholí při svítání a soumraku.</summary>
    public static float DuskFactor(double timeOfDay01)
    {
        float t = (float)timeOfDay01;
        return MathF.Max(Bump(t, center: 0.25f, width: 0.09f), Bump(t, center: 0.79f, width: 0.09f));
    }

    /// <summary>Nakreslí barevný overlay podle času — přes svět, pod UI.</summary>
    public static void DrawOverlay(
        SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, DayNightConfig config, double timeOfDay01)
    {
        float night = NightFactor(timeOfDay01);
        float dusk = DuskFactor(timeOfDay01);
        if (night <= 0.001f && dusk <= 0.001f)
        {
            return;
        }

        var bounds = new Rectangle(0, 0, viewport.Width, viewport.Height);
        spriteBatch.Begin();
        if (dusk > 0.001f)
        {
            spriteBatch.Draw(pixel, bounds, config.DuskColor.ToXna() * (float)(config.DuskAlpha * dusk));
        }

        if (night > 0.001f)
        {
            spriteBatch.Draw(pixel, bounds, config.NightColor.ToXna() * (float)(config.NightAlpha * night));
        }

        spriteBatch.End();
    }

    private static float SmoothStep(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Bump(float t, float center, float width) =>
        MathF.Max(0f, 1f - MathF.Abs(t - center) / width);
}
