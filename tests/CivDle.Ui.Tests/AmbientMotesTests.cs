using CivDle.Rendering.Effects;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Drobnosti poletující vzduchem — plátky, pyl, listí, sníh.
///
/// <para>Nádech přes obraz řekne „je podzim" jen tomu, kdo si toho všimne.
/// Listí padající přes obraz to řekne i tomu, kdo se dívá na jeden dům. A idle
/// hra potřebuje pohyb i ve chvíli, kdy se ve městě neděje nic.</para>
///
/// <para>Testuje se to, co by efekt zabilo: že hustota z dat opravdu
/// rozhoduje a že se částice po odletu z výřezu vrátí na protější straně.
/// Kdyby odlétaly pryč, obraz by se po pár vteřinách vyprázdnil — a přesně
/// tehdy, když si jich hráč začne všímat.</para>
/// </summary>
public class AmbientMotesTests
{
    private static readonly Vector2 Min = new(0, 0);
    private static readonly Vector2 Max = new(800, 600);

    [Fact]
    public void DensityFromDataDecidesHowManyFly()
    {
        var motes = new AmbientMotes(seed: 1);

        motes.Update(0.1f, Min, Max, density: 0.5f, fall: 0.2f, windX: 1f, windY: 0f);

        Assert.Equal(AmbientMotes.Capacity / 2, motes.ActiveCount);
    }

    [Fact]
    public void ASeasonWithoutMotesDrawsNothing()
    {
        // Nulová hustota musí znamenat opravdu nic, ne jednu zapomenutou
        // částici uprostřed obrazovky.
        var motes = new AmbientMotes(seed: 1);

        motes.Update(0.1f, Min, Max, density: 0f, fall: 0f, windX: 1f, windY: 0f);

        Assert.Equal(0, motes.ActiveCount);
    }

    [Fact]
    public void AMoteThatFliesOffOneEdgeComesBackOnTheOther()
    {
        // Kdyby částice odlétaly pryč, obraz by se po pár vteřinách vyprázdnil
        // a efekt by zmizel přesně ve chvíli, kdy si ho hráč začne všímat.
        float x = 850f;
        AmbientMotes.Wrap(ref x, 0f, 800f);

        Assert.Equal(50f, x, precision: 3);
    }

    [Fact]
    public void ItComesBackFromTheFarSideToo()
    {
        float x = -30f;
        AmbientMotes.Wrap(ref x, 0f, 800f);

        Assert.Equal(770f, x, precision: 3);
    }

    [Fact]
    public void AMoteFarOutsideIsBroughtAllTheWayBack()
    {
        // Při rychlém posunu kamery může být částice o několik obrazovek
        // vedle. Jediný skok na okraj by ji nedohnal — proto modulo.
        float x = 800f * 5 + 120f;
        AmbientMotes.Wrap(ref x, 0f, 800f);

        Assert.InRange(x, 0f, 800f);
        Assert.Equal(120f, x, precision: 3);
    }

    [Fact]
    public void AMoteAlreadyInsideIsLeftAlone()
    {
        float x = 300f;
        AmbientMotes.Wrap(ref x, 0f, 800f);

        Assert.Equal(300f, x, precision: 3);
    }

    [Fact]
    public void ADegenerateViewportDoesNotHang()
    {
        // Nulový výřez nastane při minimalizaci okna. Dělení rozpětím by v něm
        // skončilo nekonečnem.
        float x = 42f;
        AmbientMotes.Wrap(ref x, 0f, 0f);

        Assert.Equal(42f, x, precision: 3);

        var motes = new AmbientMotes(seed: 1);
        motes.Update(0.1f, Vector2.Zero, Vector2.Zero, density: 1f, fall: 1f, windX: 1f, windY: 1f);

        Assert.Equal(AmbientMotes.Capacity, motes.ActiveCount);
    }

    [Fact]
    public void ASeasonComingBackScattersThemAfresh()
    {
        // Když období skončí a za čas se vrátí, nesmí se částice objevit tam,
        // kde je nechalo minule — kamera je mezitím jinde a vysypaly by se
        // v jednom rohu.
        var motes = new AmbientMotes(seed: 5);
        motes.Update(0.1f, Min, Max, density: 1f, fall: 0.2f, windX: 1f, windY: 0f);
        motes.Update(0.1f, Min, Max, density: 0f, fall: 0f, windX: 1f, windY: 0f);
        motes.Update(0.1f, new Vector2(5000, 5000), new Vector2(5800, 5600), 1f, 0.2f, 1f, 0f);

        Assert.Equal(AmbientMotes.Capacity, motes.ActiveCount);
    }
}
