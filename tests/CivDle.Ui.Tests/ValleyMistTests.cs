using CivDle.Rendering;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Mlha v nížinách.
///
/// <para>Reliéf terénu ukáže, kde je svah, ale nížina a náhorní plošina
/// vypadají pořád stejně. Mlha leží tam, kde je nízko — a tím dá krajině
/// třetí rozměr, který jinak z pohledu shora nejde ukázat.</para>
///
/// <para>Testují se obě pravidla, podle kterých se rozhoduje: kde mlha je
/// (podle výšky) a kdy (podle denní doby). Prahy jsou změřené proti
/// skutečnému generátoru, takže špatná hodnota by mlhu buď rozlila přes celý
/// kontinent, nebo by nebyla vidět vůbec.</para>
/// </summary>
public class ValleyMistTests
{
    [Fact]
    public void HighGroundHasNoMist()
    {
        // Souš má medián výšky 0,54. Kdyby mlha sahala tak vysoko, ležela by
        // na většině kontinentu a přestala by cokoli říkat.
        Assert.Equal(0f, ValleyMistRenderer.StrengthAt(0.54f));
        Assert.Equal(0f, ValleyMistRenderer.StrengthAt(0.9f));
    }

    [Fact]
    public void TheLowestGroundIsFullyCovered()
    {
        Assert.Equal(1f, ValleyMistRenderer.StrengthAt(0.40f));
    }

    [Fact]
    public void MistThinsOutAsTheGroundRises()
    {
        // Tvrdá hranice by kolem nížin udělala obrys jako z mapy.
        float low = ValleyMistRenderer.StrengthAt(0.46f);
        float higher = ValleyMistRenderer.StrengthAt(0.50f);

        Assert.True(low > higher, "mlha neřídne se stoupající zemí");
        Assert.InRange(higher, 0.01f, 0.99f);
    }

    [Fact]
    public void MistIsThickestAroundDawn()
    {
        // Ráno má vypadat jinak než odpoledne — to je celý smysl denního cyklu.
        //
        // Práh býval 0,9, což byla prostě tehdejší hodnota, ne záměr. Ranní
        // vrchol je teď 0,8: na hotovém snímku přidávala mlha za svítání
        // 63 bodů jasu a rozbřesk vycházel světlejší než poledne. Tvrzení
        // testu zůstává totéž — ráno mlha je, v poledne ne.
        float dawn = ValleyMistRenderer.Density(0.22);
        float noon = ValleyMistRenderer.Density(0.5);

        Assert.True(dawn > 0.5f, $"za rozbřesku není mlha ({dawn:0.00})");
        Assert.True(noon < 0.01f, $"v poledne mlha zůstala ({noon:0.00})");
    }

    [Fact]
    public void TheEveningMistIsWeakerThanTheMorningOne()
    {
        Assert.True(ValleyMistRenderer.Density(0.84) < ValleyMistRenderer.Density(0.22));
        Assert.True(ValleyMistRenderer.Density(0.84) > 0f);
    }

    [Fact]
    public void MistFadesOutWhenTheCameraPullsBack()
    {
        // Tohle je ta oprava. Mlha je detail zblízka: leží v údolích a dává
        // terénu třetí rozměr. Při oddálení je ale nížina většina obrazu, takže
        // z místního jevu byl závoj přes celou obrazovku.
        //
        // Naměřeno na hotovém snímku: při oddálení přidala mlha za svítání
        // 63 bodů jasu (medián 54 → 117) a rozbřesk tím vyšel SVĚTLEJŠÍ než
        // poledne (106).
        Assert.Equal(1f, ValleyMistRenderer.ZoomFade(3.6f));
        Assert.Equal(0f, ValleyMistRenderer.ZoomFade(0.75f));
    }

    [Fact]
    public void TheMistDoesNotVanishWithASnap()
    {
        // Skokového zmizení by si oko všimlo víc než mlhy samotné.
        float previous = 0f;
        for (float zoom = 1.0f; zoom <= 2.2f; zoom += 0.05f)
        {
            float fade = ValleyMistRenderer.ZoomFade(zoom);

            Assert.InRange(fade, 0f, 1f);
            Assert.True(fade >= previous - 0.001f, $"mlha při zoomu {zoom:0.00} couvla");
            Assert.True(fade - previous < 0.25f, $"mlha při zoomu {zoom:0.00} skočila o {fade - previous:0.00}");
            previous = fade;
        }
    }

    [Fact]
    public void ASingleMistPatchStaysSeeThrough()
    {
        // Krytí si samo předepisovalo patnáct procent a mělo nastaveno 0,34 —
        // víc než dvojnásobek. Přes to přestane být vidět, co pod tím leží,
        // a z mlhy je bílá deka.
        Assert.True(
            ValleyMistRenderer.MaxPatchOpacity <= 0.15f,
            $"jedna skvrna mlhy kryje {ValleyMistRenderer.MaxPatchOpacity:0.00}");
    }

    [Fact]
    public void DeepNightIsClear()
    {
        // Mlha přes noční město by zhasla všechna okna, kvůli kterým noc je.
        Assert.Equal(0f, ValleyMistRenderer.Density(0.02));
    }

    [Fact]
    public void TheDayWrapsAroundMidnight()
    {
        // Denní doba je zlomek a přetéká. Kdyby se to nepočítalo, skočila by
        // hustota o půlnoci skokem.
        Assert.Equal(ValleyMistRenderer.Density(0.22), ValleyMistRenderer.Density(3.22), precision: 5);
    }
}
