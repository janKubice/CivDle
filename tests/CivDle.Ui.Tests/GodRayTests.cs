using CivDle.Rendering;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Kdy se kreslí sluneční paprsky.
///
/// <para>Nízké slunce prosvítá mezerami v mracích a kreslí do vzduchu pruhy.
/// Je to nejnápadnější věc, kterou umí světlo udělat, a odehraje se jen
/// dvakrát za den — což je přesně to, co scéna potřebovala: chvíli, kdy je
/// nejhezčí.</para>
///
/// <para>Řídí to <see cref="DayNightCycle.DuskFactor"/>, tedy tentýž signál
/// jako zlatavý nádech. Kdyby se paprsky řídily něčím vlastním, svítilo by
/// slunce šikmo i v poledne.</para>
/// </summary>
public class GodRayTests
{
    [Fact]
    public void RaysComeOutAtDawnAndDusk()
    {
        Assert.True(DayNightCycle.DuskFactor(0.25) > 0.9f, "za svítání nejsou paprsky");
        Assert.True(DayNightCycle.DuskFactor(0.79) > 0.9f, "za soumraku nejsou paprsky");
    }

    [Fact]
    public void NoonHasNone()
    {
        // Slunce v nadhlavníku nesvítí šikmo. Paprsky přes poledne by vypadaly
        // jako vada obrazu.
        Assert.Equal(0f, DayNightCycle.DuskFactor(0.5));
    }

    [Fact]
    public void DeepNightHasNone()
    {
        Assert.Equal(0f, DayNightCycle.DuskFactor(0.0));
    }

    [Fact]
    public void TheMistAndTheRaysDoNotPeakTogether()
    {
        // Mlha vrcholí těsně PŘED rozbřeskem, paprsky až při něm. Kdyby oboje
        // vrcholilo naráz, mlha by paprsky rozpustila a z nejhezčí chvíle dne
        // by byla bílá plocha.
        double mistPeak = 0.22;
        double rayPeak = 0.25;

        Assert.True(ValleyMistRenderer.Density(mistPeak) > ValleyMistRenderer.Density(rayPeak));
        Assert.True(DayNightCycle.DuskFactor(rayPeak) > DayNightCycle.DuskFactor(mistPeak));
    }
}
