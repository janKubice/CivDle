using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Kam sedá odznak „tahle budova stojí".
///
/// <para>Je to jediný prvek, který se kreslí <b>přes</b> hotovou zástavbu,
/// takže se pozná okamžitě, když je špatně umístěný: v půdorysu z něj byla
/// barevná záplata na střeše, uprostřed nad budovou dosedl v souvislé zástavbě
/// na střechu souseda nad sebou.</para>
///
/// <para>Testuje se pravidlo, ne kresba — proto je poloha vytažená do
/// <see cref="BuildingRenderer.BadgeRect"/> a dá se ověřit bez okna.</para>
/// </summary>
public class StallBadgeTests
{
    private static readonly Rectangle Cottage = new(64, 96, 32, 32);

    [Fact]
    public void TheBadgeSitsAboveTheFootprint()
    {
        // Ani pixel do půdorysu: tam je kresba budovy, kvůli které to celé je.
        var badge = BuildingRenderer.BadgeRect(Cottage);

        Assert.True(badge.Bottom <= Cottage.Y, $"odznak zasahuje do půdorysu ({badge.Bottom} vs {Cottage.Y})");
    }

    [Fact]
    public void TheBadgeSitsInTheRightHandCorner()
    {
        // Roh je v pravidelném rastru místo, kde se potkávají mezery mezi
        // parcelami — tam odznak leží na zemi, ne na cizí střeše.
        var badge = BuildingRenderer.BadgeRect(Cottage);

        Assert.Equal(Cottage.Right, badge.Right);
    }

    [Fact]
    public void TheBadgeNeverCoversMoreThanACornerOfTheRoof()
    {
        // Tohle byla ta původní stížnost: odznak přebíjel sprite. Hlídá se
        // plochou, protože právě ta rozhoduje, jestli to čte jako upozornění,
        // nebo jako flek.
        var badge = BuildingRenderer.BadgeRect(Cottage);

        float share = badge.Width * badge.Height / (float)(Cottage.Width * Cottage.Height);

        Assert.True(share < 0.05f, $"odznak zabírá {share:P0} budovy");
    }

    [Theory]
    [InlineData(16)]  // půl dlaždice
    [InlineData(32)]  // domek
    [InlineData(64)]  // dvě dlaždice
    [InlineData(256)] // megastruktura
    public void TheBadgeStaysReadableAtEverySize(int width)
    {
        // Odznak roste s budovou, ale jen mezi mantinely: pod tři pixely by
        // zmizel, nad šest by na velkém bloku zase byl cedule.
        var badge = BuildingRenderer.BadgeRect(new Rectangle(0, 100, width, width));

        Assert.InRange(badge.Width, 3, 6);
        Assert.Equal(badge.Width, badge.Height);
    }

    [Fact]
    public void ATinyBuildingStillGetsAVisibleBadge()
    {
        // Clamp zdola: u nejmenší budovy by z podílu vyšel nula pixelů a odznak
        // by se nenakreslil vůbec — chyba, kterou nikdo nenahlásí, jen mu bude
        // připadat, že inspektor nefunguje.
        var badge = BuildingRenderer.BadgeRect(new Rectangle(0, 0, 8, 8));

        Assert.True(badge.Width >= 3);
    }
}
