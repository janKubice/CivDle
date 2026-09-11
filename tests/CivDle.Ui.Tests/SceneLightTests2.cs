using CivDle.Core.Content;
using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Barva světla, kterou se násobí hotová scéna.
///
/// <para>Dřív se přes obraz táhly tři průhledné obdélníky (období, grading,
/// tma noci). Průhledný obdélník míchá každý pixel směrem k sobě, takže tmavá
/// místa zesvětlí stejně, jako světlá ztmaví — kontrast plošně klesá a obraz
/// zmléční. Násobení dělá pravý opak. Testuje se hlavně to, co se tím dá
/// snadno rozbít: že poledne nechá obraz být a že noc nespadne na čerň.</para>
/// </summary>
public class SceneLightTests2
{
    [Fact]
    public void NoonLeavesTheImageAlone()
    {
        // Bílá = násobení jedničkou. Kdyby poledne bílé nebylo, měla by hra
        // trvale nádech a nikdo by nevěděl proč.
        var light = DayNightCycle.LightColor(0.5, Config(), season: null);

        Assert.Equal(Color.White, light);
    }

    [Fact]
    public void NightIsDarkButNeverBlack()
    {
        // Z násobení nulou už nic nevytáhne ani pouliční lampa — noc by byla
        // černá plocha, ne noc.
        var night = DayNightCycle.LightColor(0.0, Config(), season: null);

        Assert.True(Brightness(night) < 0.6f, $"noc není tmavá ({Brightness(night):0.00})");
        Assert.True(Brightness(night) > 0.1f, $"noc spadla na čerň ({Brightness(night):0.00})");
    }

    [Fact]
    public void DayIsBrighterThanNight()
    {
        var config = Config();

        float noon = Brightness(DayNightCycle.LightColor(0.5, config, null));
        float midnight = Brightness(DayNightCycle.LightColor(0.0, config, null));

        Assert.True(noon > midnight);
    }

    [Fact]
    public void MorningIsWarmerThanEvening()
    {
        // Celý smysl gradingu: ráno a večer se nemají lišit jen jasem, ale
        // teplotou. Bez toho vypadá hra pořád stejně, jen tmavší.
        var config = Config();

        var morning = DayNightCycle.LightColor(0.28, config, null);
        var evening = DayNightCycle.LightColor(0.72, config, null);

        float morningWarmth = morning.R - morning.B;
        float eveningWarmth = evening.R - evening.B;

        Assert.True(
            morningWarmth > eveningWarmth,
            $"ráno má být teplejší než večer ({morningWarmth} vs {eveningWarmth})");
    }

    [Fact]
    public void BloomIsStrongestAtNight()
    {
        // V noci jsou okna a lampy jediné světlo v obraze a mají zářit.
        // V poledni by z toho byla jen mlha.
        Assert.True(DayNightCycle.BloomStrength(0.0) > DayNightCycle.BloomStrength(0.5));
    }

    private static float Brightness(Color c) => (c.R + c.G + c.B) / (3f * 255f);

    private static DayNightConfig Config() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data")).Gameplay.DayNight;
}
