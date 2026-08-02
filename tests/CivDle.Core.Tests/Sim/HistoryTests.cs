using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Časosběr: hrubý půdorys města zaznamenaný co pár minut, ze kterého se dá
/// přehrát celý růst.
///
/// <para>Testuje se to, co by z časosběru udělalo lež nebo přítěž: nesmí růst
/// donekonečna, po naplnění se musí <b>prořídit</b> (a ne uříznout začátek —
/// ten je nejzajímavější), musí přežít restart a Vzestupem musí zmizet, aby se
/// dva světy nemíchaly.</para>
/// </summary>
public class HistoryTests
{
    private const int Hut = 0;

    private static readonly PrestigeConfig EarlyAscension =
        new(new GoalCondition(MetricKind.Population, -1, 5), MetricKind.Population, -1, 5);

    private static GameContent Content(double intervalSeconds = 1, int maxFrames = 8)
    {
        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
            HistoryOrNull = intervalSeconds > 0 ? new HistoryConfig(intervalSeconds, maxFrames) : null,
        };

        return TestContent.Build(gameplay: gameplay, prestige: EarlyAscension);
    }

    private static Simulation NewSim(GameContent? content = null) =>
        new(content ?? Content(), new UniformTerrain(1));

    private static void Tick(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    // ----- záznam -----

    [Fact]
    public void WithoutConfigNothingIsRecorded()
    {
        // Starší data časosběr neznají — save nemá kvůli tomu narůst.
        var sim = NewSim(Content(intervalSeconds: 0));

        Tick(sim, 500);

        Assert.False(sim.HistoryEnabled);
        Assert.Equal(0, sim.History.Count);
    }

    [Fact]
    public void TheCityIsSnapshottedOverTime()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Hut, 0, 0));

        Tick(sim, (int)Simulation.TicksPerSecond * 4);

        Assert.True(sim.History.Count >= 3, $"Zaznamenalo se jen {sim.History.Count} snímků.");
    }

    [Fact]
    public void ASnapshotRemembersWhereTheCityStood()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Hut, 0, 0));
        Tick(sim, (int)Simulation.TicksPerSecond * 2);

        Assert.True(CityHistory.TryCellOf(0, 0, out int cellX, out int cellY));
        int last = sim.History.Count - 1;

        Assert.True(sim.History.IsOccupied(last, cellX, cellY), "Buňka s chalupou má být obsazená.");
        Assert.False(sim.History.IsOccupied(last, 0, 0), "Vzdálený roh mřížky má zůstat prázdný.");
    }

    [Fact]
    public void SnapshotsCarryTheHeadlineNumbers()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Hut, 0, 0));

        Tick(sim, (int)Simulation.TicksPerSecond * 2);

        var frame = sim.History.FrameAt(sim.History.Count - 1);
        Assert.Equal(1, frame.Buildings);
        Assert.True(frame.Tick > 0);
        Assert.True(frame.Seconds > 0);
    }

    // ----- kruhový buffer -----

    [Fact]
    public void HistoryNeverGrowsBeyondItsCap()
    {
        // Bez stropu by hodiny hraní nafoukly save i paměť.
        var sim = NewSim(Content(intervalSeconds: 1, maxFrames: 8));

        Tick(sim, (int)Simulation.TicksPerSecond * 60);

        Assert.InRange(sim.History.Count, 1, 8);
    }

    [Fact]
    public void ThinningKeepsTheBeginningOfTheStory()
    {
        // Kdyby se zahazoval nejstarší snímek, časosběr by časem začínal
        // uprostřed příběhu — a začátek je ta nejzajímavější část.
        var history = new CityHistory(maxFrames: 4);
        var mask = new byte[CityHistory.CellBytes];
        for (int i = 0; i < 12; i++)
        {
            history.Add(new HistoryFrame(i, i, i, -1), mask);
        }

        Assert.Equal(0, history.FrameAt(0).Tick);
        Assert.True(history.Count <= 4);
    }

    [Fact]
    public void ThinningKeepsTheOrderOfTime()
    {
        var history = new CityHistory(maxFrames: 6);
        var mask = new byte[CityHistory.CellBytes];
        for (int i = 0; i < 40; i++)
        {
            history.Add(new HistoryFrame(i, i, i, -1), mask);
        }

        for (int i = 1; i < history.Count; i++)
        {
            Assert.True(history.FrameAt(i).Tick > history.FrameAt(i - 1).Tick);
        }
    }

    [Fact]
    public void AGridOfTheWrongSizeIsRejected()
    {
        // Tichá chyba by znamenala poškozený snímek, který se pozná až při přehrání.
        var history = new CityHistory(maxFrames: 4);

        Assert.Throws<ArgumentException>(() => history.Add(default, new byte[3]));
    }

    // ----- barvy -----

    [Fact]
    public void CellsRememberTheColourOfTheRealBuilding()
    {
        // Přehrávka kreslí město v barvách skutečné zástavby — ne jako
        // anonymní mřížku. Bez toho by časosběr nevypadal jako „moje město".
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Hut, 0, 0));
        Tick(sim, (int)Simulation.TicksPerSecond * 2);

        Assert.True(CityHistory.TryCellOf(0, 0, out int cellX, out int cellY));
        int last = sim.History.Count - 1;

        var color = sim.History.ColorAt(last, cellX, cellY);
        Assert.NotNull(color);

        // Jedna barva: bez silnic nemá paleta důvod nést cokoli dalšího.
        Assert.Single(sim.History.Palette);
    }

    [Fact]
    public void TheSamePaletteColourIsNeverStoredTwice()
    {
        var history = new CityHistory(maxFrames: 4);
        var red = new CivDle.Core.Content.RgbColor(200, 40, 40);

        int first = history.PaletteIndexOf(red);
        int second = history.PaletteIndexOf(red);

        Assert.Equal(first, second);
        Assert.Single(history.Palette);
    }

    [Fact]
    public void AFullPaletteFallsBackToTheLastColourInsteadOfOverflowing()
    {
        // 256. barva nesmí přetéct bajt buňky — kreslit trochu špatnou barvou
        // je lepší než spadnout nebo nekreslit.
        var history = new CityHistory(maxFrames: 4);
        for (int i = 0; i < CityHistory.MaxPaletteColors; i++)
        {
            history.PaletteIndexOf(new CivDle.Core.Content.RgbColor((byte)i, (byte)(i * 3), (byte)(255 - i)));
        }

        int overflow = history.PaletteIndexOf(new CivDle.Core.Content.RgbColor(9, 99, 199));

        Assert.Equal(CityHistory.MaxPaletteColors - 1, overflow);
        Assert.Equal(CityHistory.MaxPaletteColors, history.Palette.Count);
    }

    // ----- detail: silnice a celé půdorysy -----

    [Fact]
    public void RoadsAreInTheTimelapseToo()
    {
        // Bez silnic vypadala přehrávka jako ostrůvky baráků ve vzduchoprázdnu —
        // město drží pohromadě právě síť mezi nimi.
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Hut, 0, 0));
        sim.AddRoadTileForTest(6, 0);
        Tick(sim, (int)Simulation.TicksPerSecond * 2);

        Assert.True(CityHistory.TryCellOf(6, 0, out int cellX, out int cellY));
        int last = sim.History.Count - 1;

        Assert.True(sim.History.IsOccupied(last, cellX, cellY), "Silnice má být v časosběru vidět.");
    }

    [Fact]
    public void ABuildingFillsItsWholeFootprint()
    {
        // Na jemné mřížce musí být velká budova opravdu velká, jinak hráč
        // v přehrávce nepozná huť od chalupy.
        var content = TestContent.Build(
            buildings: new[] { TestContent.SimpleBuilding("hut", 2) with { FootprintWidth = 4, FootprintHeight = 4 } },
            gameplay: TestContent.DefaultGameplay with
            {
                FoodPerPersonPerSecond = 0,
                PopulationGrowthPerSecond = 0,
                HistoryOrNull = new HistoryConfig(1, 8),
            });
        var sim = new Simulation(content, new UniformTerrain(1));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0));
        Tick(sim, (int)Simulation.TicksPerSecond * 2);

        int last = sim.History.Count - 1;
        Assert.True(CityHistory.TryCellOf(0, 0, out int nearX, out int nearY));
        Assert.True(CityHistory.TryCellOf(3, 3, out int farX, out int farY));

        Assert.True(sim.History.IsOccupied(last, nearX, nearY));
        Assert.True(sim.History.IsOccupied(last, farX, farY), "Vzdálený roh půdorysu má být taky obsazený.");
    }

    [Fact]
    public void TheGridIsFineEnoughToTellBuildingsApart()
    {
        // Dvě budovy vedle sebe musí padnout do RŮZNÝCH buněk — jinak je
        // přehrávka jen shluk nejasných čtverečků, což byla přesně ta stížnost.
        Assert.True(CityHistory.TryCellOf(0, 0, out int aX, out int aY));
        Assert.True(CityHistory.TryCellOf(2, 0, out int bX, out int bY));

        Assert.True(aX != bX || aY != bY);
    }

    // ----- Vzestup a save -----

    [Fact]
    public void AscendingStartsAFreshChronicle()
    {
        // Dva světy se nemají míchat do jedné přehrávky.
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Hut, 0, 0));
        Tick(sim, (int)Simulation.TicksPerSecond * 3);
        Assert.True(sim.History.Count > 0);

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.Equal(0, sim.History.Count);
    }

    [Fact]
    public void TheChronicleSurvivesSaveAndLoad()
    {
        // Bez tohohle by po restartu zmizel celý příběh běhu.
        var content = Content();
        var sim = new Simulation(content, new UniformTerrain(1));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Hut, 0, 0));
        Tick(sim, (int)Simulation.TicksPerSecond * 3);
        int before = sim.History.Count;
        Assert.True(before > 0);

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, new SaveMetadata(1, "s", "test", DateTime.UnixEpoch));
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, Content());

        Assert.Equal(before, loaded.History.Count);
        Assert.Equal(sim.History.FrameAt(0), loaded.History.FrameAt(0));
        Assert.True(CityHistory.TryCellOf(0, 0, out int cellX, out int cellY));
        Assert.Equal(
            sim.History.IsOccupied(before - 1, cellX, cellY),
            loaded.History.IsOccupied(before - 1, cellX, cellY));
    }

    // ----- mřížka -----

    [Fact]
    public void TilesMapToCellsAroundTheOrigin()
    {
        Assert.True(CityHistory.TryCellOf(0, 0, out int cx, out int cy));
        Assert.Equal(CityHistory.GridSize / 2, cx);
        Assert.Equal(CityHistory.GridSize / 2, cy);

        // Sousední dlaždice patří do stejné buňky — mřížka je hrubá schválně.
        Assert.True(CityHistory.TryCellOf(1, 1, out int nx, out int ny));
        Assert.Equal(cx, nx);
        Assert.Equal(cy, ny);
    }

    [Fact]
    public void DistantColoniesFallOutsideTheRecordedArea()
    {
        // Časosběr pokrývá okolí startu; osada na druhém konci světa se do něj
        // prostě nevejde a nesmí kvůli tomu nic spadnout.
        Assert.False(CityHistory.TryCellOf(100_000, 0, out _, out _));
        Assert.False(CityHistory.TryCellOf(0, -100_000, out _, out _));
    }
}
