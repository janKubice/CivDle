using CivDle.Rendering;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Kdy je silniční dlaždice křižovatka.
///
/// <para>Silnice se kreslí ve třech vrstvách (obrubník, vozovka, vyjetý střed)
/// a křižovatka dostane vlastní značku. Rozhodnutí „je tohle křižovatka?" je
/// jediná netriviální část toho vzhledu — kdyby vyšlo špatně, byla by značka
/// na rovném úseku a hráč by na hustém předměstí přestal síť číst.</para>
/// </summary>
public sealed class RoadLookTests
{
    [Fact]
    public void AStraightRoadIsNotACrossing()
    {
        Assert.False(RoadRenderer.IsCrossing(east: true, west: true, south: false, north: false));
        Assert.False(RoadRenderer.IsCrossing(east: false, west: false, south: true, north: true));
    }

    [Fact]
    public void ADeadEndIsNotACrossing()
    {
        Assert.False(RoadRenderer.IsCrossing(east: true, west: false, south: false, north: false));
        Assert.False(RoadRenderer.IsCrossing(east: false, west: false, south: false, north: false));
    }

    [Fact]
    public void ATJunctionIsACrossing()
    {
        Assert.True(RoadRenderer.IsCrossing(east: true, west: true, south: true, north: false));
    }

    [Fact]
    public void AFourWayIsACrossing()
    {
        Assert.True(RoadRenderer.IsCrossing(east: true, west: true, south: true, north: true));
    }

    [Fact]
    public void ACornerCountsAsACrossing()
    {
        // Zatáčka potřebuje značku ze stejného důvodu jako křížení: bez ní
        // vypadá roh jako dvě useknuté cesty.
        Assert.True(RoadRenderer.IsCrossing(east: true, west: false, south: true, north: false));
        Assert.True(RoadRenderer.IsCrossing(east: false, west: true, south: false, north: true));
    }

    [Fact]
    public void MarkingsBelongToLaterEras()
    {
        // Dlážděná cesta ve starověku nemá mít vodorovné značení.
        Assert.True(RoadRenderer.MarkingsEra > 0);
    }
}
