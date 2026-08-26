using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Kronika: z časosběru pár vět, které se dají přečíst.
///
/// <para>Hlídají se dvě věci. Za prvé <b>pořadí</b> — věty musí jít podle času,
/// jinak by „dnes" stálo nad „a pak přišla éra páry" a stránka by nedávala
/// smysl. Za druhé to, že zlom je <b>přírůstek</b>, ne absolutní číslo:
/// nejvíc lidí je vždycky na posledním snímku a o běhu to neřekne nic.</para>
/// </summary>
public class ChronicleWriterTests
{
    private static readonly byte[] EmptyCells = new byte[CityHistory.CellBytes];

    [Fact]
    public void WithoutTemplates_NothingIsWritten()
    {
        var history = HistoryOf((0, 10), (600, 500));

        Assert.Empty(ChronicleWriter.Write(history, ChronicleCatalog.Empty));
    }

    [Fact]
    public void WithoutFrames_NothingIsWritten()
    {
        var catalog = Catalog(("today", ChronicleMoment.Today, 0));

        Assert.Empty(ChronicleWriter.Write(new CityHistory(8), catalog));
    }

    [Fact]
    public void FoundedAndTodayFrameTheStory()
    {
        var history = HistoryOf((0, 10), (600, 400), (1200, 5000));
        var catalog = Catalog(
            ("today", ChronicleMoment.Today, 0),
            ("founded", ChronicleMoment.Founded, 0));

        var lines = ChronicleWriter.Write(history, catalog);

        Assert.Equal(2, lines.Count);

        // V datech je „dnes" první, na stránce musí být poslední — řadí se čas,
        // ne pořadí v souboru.
        Assert.Equal("chronicle.line.founded", lines[0].TextKey);
        Assert.Equal("chronicle.line.today", lines[1].TextKey);
        Assert.Equal(5000, lines[1].Value);
        Assert.Equal(1200, lines[1].Tick);
    }

    [Fact]
    public void GrowthPicksTheBiggestJump_NotTheBiggestNumber()
    {
        // Skok 0→400 je největší přírůstek; poslední snímek má nejvíc lidí,
        // ale přibylo v něm jen 100.
        var history = HistoryOf((0, 10), (600, 410), (1200, 510));
        var catalog = Catalog(("growth", ChronicleMoment.Growth, 0));

        var line = Assert.Single(ChronicleWriter.Write(history, catalog));

        Assert.Equal(400, line.Value);
        Assert.Equal(600, line.Tick);
    }

    [Fact]
    public void GrowthNeedsTwoFrames()
    {
        var catalog = Catalog(("growth", ChronicleMoment.Growth, 0));

        Assert.Empty(ChronicleWriter.Write(HistoryOf((0, 10)), catalog));
    }

    [Fact]
    public void EachEraGetsOneLine()
    {
        var history = HistoryOf((0, 10, 0), (600, 100, 0), (1200, 300, 1), (1800, 500, 1), (2400, 900, 2));
        var catalog = Catalog(("era", ChronicleMoment.EraChange, 0));

        var lines = ChronicleWriter.Write(history, catalog);

        Assert.Equal(2, lines.Count);
        Assert.Equal(1, lines[0].EraIndex);
        Assert.Equal(1200, lines[0].Tick);
        Assert.Equal(2, lines[1].EraIndex);
    }

    [Fact]
    public void PeakIsTheHighestPopulation_EvenWhenTheCityShrankLater()
    {
        var history = HistoryOf((0, 10), (600, 900), (1200, 300));
        var catalog = Catalog(("peak", ChronicleMoment.Peak, 0));

        var line = Assert.Single(ChronicleWriter.Write(history, catalog));

        Assert.Equal(900, line.Value);
        Assert.Equal(600, line.Tick);
    }

    [Fact]
    public void HardshipReportsTheFirstBadMoment_NotEveryOne()
    {
        var history = new CityHistory(8);
        history.Add(new HistoryFrame(0, 10, 1, 0, Happiness: 0.9), EmptyCells);
        history.Add(new HistoryFrame(600, 100, 8, 0, Happiness: 0.3), EmptyCells);
        history.Add(new HistoryFrame(1200, 120, 9, 0, Happiness: 0.2), EmptyCells);

        var catalog = Catalog(("hardship", ChronicleMoment.Hardship, 0.4));

        var line = Assert.Single(ChronicleWriter.Write(history, catalog));

        Assert.Equal(600, line.Tick);
        Assert.Equal(30, line.Value); // procenta, ne podíl — věta je o „30 %"
    }

    [Fact]
    public void AMomentThatNeverHappenedIsSimplyNotWritten()
    {
        var history = new CityHistory(8);
        history.Add(new HistoryFrame(0, 10, 1, 0, Happiness: 1.0, Pollution: 0), EmptyCells);

        var catalog = Catalog(
            ("hardship", ChronicleMoment.Hardship, 0.4),
            ("pollution", ChronicleMoment.Pollution, 0.5));

        Assert.Empty(ChronicleWriter.Write(history, catalog));
    }

    [Fact]
    public void RealContentWritesAChronicleForARealRun()
    {
        var content = TestData.LoadRealContent();
        Assert.True(content.Chronicle.IsEnabled, "bez šablon by kronika byla prázdná stránka");

        var history = HistoryOf((0, 5), (600, 300), (1200, 4000));
        var lines = ChronicleWriter.Write(history, content.Chronicle);

        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.StartsWith("chronicle.line.", line.TextKey));

        // Věty musí jít podle času — na stránce se čtou odshora dolů.
        for (int i = 1; i < lines.Count; i++)
        {
            Assert.True(lines[i - 1].Tick <= lines[i].Tick);
        }
    }

    // ----- pomocné -----

    private static ChronicleCatalog Catalog(params (string Id, ChronicleMoment Moment, double Threshold)[] lines)
        => new(lines.Select(l => new ChronicleTemplateDef(l.Id, l.Moment, l.Threshold)).ToArray());

    private static CityHistory HistoryOf(params (long Tick, long Population)[] frames)
        => HistoryOf(frames.Select(f => (f.Tick, f.Population, 0)).ToArray());

    private static CityHistory HistoryOf(params (long Tick, long Population, int Era)[] frames)
    {
        var history = new CityHistory(Math.Max(2, frames.Length));
        foreach (var (tick, population, era) in frames)
        {
            history.Add(new HistoryFrame(tick, population, 1, era), EmptyCells);
        }

        return history;
    }
}
