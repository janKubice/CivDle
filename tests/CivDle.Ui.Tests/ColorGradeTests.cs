using CivDle.Rendering;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Barva denního světla a mihotání nočních oken.
///
/// <para>Den a noc se dosud lišily jen jasem, takže hra vypadala celý den
/// stejně. Tady se hlídá to, co z toho dělá tři různé nálady: ráno má být
/// teplé, večer studený, a v noci se barva světla nemá malovat vůbec — přes
/// tmu není co gradovat.</para>
/// </summary>
public sealed class ColorGradeTests
{
    private const double Morning = 0.28;
    private const double Noon = 0.50;
    private const double Evening = 0.78;
    private const double Midnight = 0.0;

    [Fact]
    public void MorningIsWarm()
    {
        var (color, alpha) = DayNightCycle.Grade(Morning);

        Assert.True(color.R > color.B, $"Ranní světlo není teplé: {color}.");
        Assert.True(alpha > 0.01f);
    }

    [Fact]
    public void EveningIsCold()
    {
        var (color, alpha) = DayNightCycle.Grade(Evening);

        Assert.True(color.B > color.R, $"Večerní světlo není studené: {color}.");
        Assert.True(alpha > 0.01f);
    }

    [Fact]
    public void NoonIsNeutral()
    {
        // V poledne se nemá malovat nic — bílé světlo je bílé.
        var (_, alpha) = DayNightCycle.Grade(Noon);

        Assert.True(alpha < 0.02f, $"Poledne má nádech {alpha:F3}, má být bez.");
    }

    [Fact]
    public void NightIsNotGradedAtAll()
    {
        // Teplý nádech o půlnoci by se pral s modrou nocí — a hlavně je přesně
        // o půlnoci v křivce zlom (modrý večer → teplé ráno). Nula na obou
        // stranách je jediné, co ten zlom udělá neviditelným.
        var (_, alpha) = DayNightCycle.Grade(Midnight);

        Assert.Equal(0f, alpha, 5);
    }

    [Fact]
    public void GradingIsAlwaysASuggestion()
    {
        // Nad ~15 % už to není světlo, ale barevný filtr.
        for (double t = 0; t < 1.0; t += 0.01)
        {
            var (_, alpha) = DayNightCycle.Grade(t);
            Assert.InRange(alpha, 0f, 0.15f);
        }
    }

    [Fact]
    public void TheDayLoopsSeamlessly()
    {
        // Čas se počítá odjinud a může přetéct přes 1.0; zlom v barvě přesně
        // o půlnoci by byl vidět jako bliknutí.
        var atMidnight = DayNightCycle.Grade(0.0);
        var afterWrap = DayNightCycle.Grade(1.0);
        var beforeWrap = DayNightCycle.Grade(0.9999);

        Assert.Equal(atMidnight.Color, afterWrap.Color);
        Assert.Equal(atMidnight.Alpha, afterWrap.Alpha, 5);
        Assert.Equal(atMidnight.Alpha, beforeWrap.Alpha, 5);
    }

    [Fact]
    public void WindowsAreNeverOffAndNeverOverdriven()
    {
        for (float time = 0; time < 60f; time += 0.37f)
        {
            float alpha = LightsRenderer.WindowAlpha(1234, time);
            Assert.InRange(alpha, 0.4f, 1.0f);
        }
    }

    [Fact]
    public void WindowsFlickerOverTime()
    {
        float start = LightsRenderer.WindowAlpha(99, 0f);
        bool changed = false;
        for (float time = 0.5f; time < 12f; time += 0.5f)
        {
            if (MathF.Abs(LightsRenderer.WindowAlpha(99, time) - start) > 0.05f)
            {
                changed = true;
                break;
            }
        }

        Assert.True(changed, "Okno vůbec nemihotá — noc je zase statická mapa teček.");
    }

    [Fact]
    public void NeighbouringWindowsDoNotPulseTogether()
    {
        // Kdyby celá ulice pulzovala jednou fází, vypadalo by to jako dýchající
        // vánoční řetěz, ne jako město.
        float a = LightsRenderer.WindowAlpha(1000, 3f);
        float b = LightsRenderer.WindowAlpha(1001, 3f);

        Assert.NotEqual(a, b, 3);
    }

    [Fact]
    public void TheSameWindowAtTheSameMomentLooksTheSame()
    {
        Assert.Equal(
            LightsRenderer.WindowAlpha(7, 4.25f),
            LightsRenderer.WindowAlpha(7, 4.25f));
    }
}
