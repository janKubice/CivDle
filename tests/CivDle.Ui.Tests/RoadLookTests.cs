using CivDle.Core.Content;
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

    [Fact]
    public void TheRoadSurfaceChangesWithTheEra()
    {
        // Silnice je nejdelší souvislá čára na obrazovce. Kdyby vypadala
        // v pravěku stejně jako v orbitální civilizaci, nezměnil by se dojem
        // z města ani po tisíci letech vývoje.
        var roads = LoadContent().Gameplay.Roads;

        Assert.NotNull(roads.Surfaces);
        var kinds = roads.Surfaces!.Select(s => s.Kind).Distinct().ToList();
        Assert.True(kinds.Count > 1, "všechny éry mají tentýž povrch");
    }

    [Fact]
    public void TheFirstEraAlreadyHasASurface()
    {
        // Bez pokrytí nulté éry by první věk neměl povrch žádný a silnice by
        // se kreslila náhradní barvou.
        var roads = LoadContent().Gameplay.Roads;

        Assert.NotNull(roads.SurfaceForEra(0));
    }

    [Fact]
    public void EarlyRoadsAreDirtAndLateOnesArePaved()
    {
        // Tohle je ten rozdíl, kvůli kterému povrchy vznikly: polní cesta na
        // začátku, asfalt na konci.
        var roads = LoadContent().Gameplay.Roads;

        Assert.Equal(RoadSurfaceKind.Dirt, roads.SurfaceForEra(0)!.Kind);
        Assert.Equal(RoadSurfaceKind.Paved, roads.SurfaceForEra(99)!.Kind);
    }

    [Fact]
    public void AnEraBetweenTwoSurfacesKeepsTheEarlierOne()
    {
        // Povrch platí OD éry, ne v ní: mezi dvěma zápisy se drží ten starší,
        // jinak by v půlce hry silnice zmizela.
        var roads = new RoadConfig(
            new RgbColor(1, 2, 3), 10, Surfaces: new[]
            {
                new RoadSurface(0, RoadSurfaceKind.Dirt, new RgbColor(1, 1, 1)),
                new RoadSurface(4, RoadSurfaceKind.Paved, new RgbColor(2, 2, 2)),
            });

        Assert.Equal(RoadSurfaceKind.Dirt, roads.SurfaceForEra(3)!.Kind);
        Assert.Equal(RoadSurfaceKind.Paved, roads.SurfaceForEra(4)!.Kind);
    }

    [Fact]
    public void WithoutSurfacesInDataNothingBreaks()
    {
        // Obsah bez povrchů je platný obsah — hra má vypadat jako dřív,
        // ne spadnout.
        var roads = new RoadConfig(new RgbColor(1, 2, 3), 10);

        Assert.Null(roads.SurfaceForEra(0));
    }

    private static CivDle.Core.Content.GameContent LoadContent() =>
        new CivDle.Core.Content.ContentLoader().LoadFrom(
            System.IO.Path.Combine(AppContext.BaseDirectory, "data"));
}
