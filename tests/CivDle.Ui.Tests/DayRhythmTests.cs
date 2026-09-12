using CivDle.Rendering.Effects;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Denní rytmus města.
///
/// <para>Chodci se dřív pohybovali náhodnou procházkou a na denní dobu se
/// neptali vůbec — město vypadalo ve tři ráno stejně jako v poledne. Testuje
/// se to, co z pohybu dělá chování: že se ráno chodí do práce, večer domů,
/// že v noci ulice zřídnou a že se nevyprázdní úplně.</para>
/// </summary>
public class DayRhythmTests
{
    [Theory]
    [InlineData(0.30)] // ~7:00
    [InlineData(0.38)] // ~9:00
    public void InTheMorningPeopleHeadForWork(double time)
    {
        Assert.Equal(Errand.Work, DayRhythm.ErrandAt(time));
    }

    [Theory]
    [InlineData(0.72)] // ~17:15
    [InlineData(0.85)] // ~20:20
    public void InTheEveningPeopleHeadHome(double time)
    {
        Assert.Equal(Errand.Home, DayRhythm.ErrandAt(time));
    }

    [Fact]
    public void AtMiddayNobodyIsOnASchedule()
    {
        // Kdyby se v poledne taky někam „muselo", byl by z města dopravní pás.
        Assert.Equal(Errand.Anywhere, DayRhythm.ErrandAt(0.5));
    }

    [Fact]
    public void TheStreetsAreBusiestByDay()
    {
        Assert.Equal(1f, DayRhythm.CrowdAt(0.5));
        Assert.True(DayRhythm.CrowdAt(0.02) < DayRhythm.CrowdAt(0.5), "noc není klidnější než poledne");
    }

    [Fact]
    public void TheCityNeverGoesCompletelyEmpty()
    {
        // Prázdná ulice nevypadá jako spící město, ale jako vypnutá hra.
        Assert.True(DayRhythm.CrowdAt(0.0) > 0.05f, "v noci nezůstal venku vůbec nikdo");
    }

    [Fact]
    public void TheCrowdChangesGradually()
    {
        // Kdyby dav skočil, objevily by se postavy naráz jako přepnutím vypínače.
        for (double t = 0; t < 1.0; t += 0.01)
        {
            float here = DayRhythm.CrowdAt(t);
            float next = DayRhythm.CrowdAt(t + 0.01);

            Assert.True(Math.Abs(here - next) < 0.12f,
                $"dav skočil v čase {t:0.00} ({here:0.00} → {next:0.00})");
        }
    }

    [Fact]
    public void TheCrowdStaysAFraction()
    {
        for (double t = 0; t < 1.0; t += 0.013)
        {
            Assert.InRange(DayRhythm.CrowdAt(t), 0f, 1f);
        }
    }

    [Fact]
    public void NightIsNightOnBothSidesOfMidnight()
    {
        Assert.True(DayRhythm.IsNight(0.95), "večer po setmění se nepočítá za noc");
        Assert.True(DayRhythm.IsNight(0.05), "ráno před svítáním se nepočítá za noc");
        Assert.False(DayRhythm.IsNight(0.5), "poledne se počítá za noc");
    }

    [Fact]
    public void TimeThatRanOverMidnightStillWorks()
    {
        // Čas se počítá ze zlomku dne a může přijít přetočený nebo záporný;
        // bez srovnání by se rytmus v ten okamžik rozpadl.
        Assert.Equal(DayRhythm.ErrandAt(0.30), DayRhythm.ErrandAt(1.30));
        Assert.Equal(DayRhythm.ErrandAt(0.30), DayRhythm.ErrandAt(-0.70));
        Assert.Equal(DayRhythm.CrowdAt(0.30), DayRhythm.CrowdAt(2.30));
    }
}
