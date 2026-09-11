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
        float dawn = ValleyMistRenderer.Density(0.22);
        float noon = ValleyMistRenderer.Density(0.5);

        Assert.True(dawn > 0.9f, $"za rozbřesku není mlha ({dawn:0.00})");
        Assert.True(noon < 0.01f, $"v poledne mlha zůstala ({noon:0.00})");
    }

    [Fact]
    public void TheEveningMistIsWeakerThanTheMorningOne()
    {
        Assert.True(ValleyMistRenderer.Density(0.84) < ValleyMistRenderer.Density(0.22));
        Assert.True(ValleyMistRenderer.Density(0.84) > 0f);
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
