using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Plavení dřeva po řece.
///
/// <para>Nejdůležitější test je ten poslední: <b>kláda musí umět skončit</b>.
/// Přesně na tom mechanika padá — tok končí v jezeře nebo v prohlubni, a kdyby
/// tam kláda jen stála, hromadily by se donekonečna a s nimi paměť.</para>
/// </summary>
public class LogRaftTests
{
    [Fact]
    public void RealContentHasSomethingToFloatAndSomethingToCatch()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.HasRafting);
        Assert.Contains(content.Buildings.All, b => b.DropsLogs);
        Assert.Contains(content.Buildings.All, b => b.CatchesLogs);
    }

    [Fact]
    public void CatchingIsNeverALoss()
    {
        // Násobič pod 1 by znamenal, že se plavením dřevo ztrácí — pak by řeku
        // nikdo nepoužil a mechanika by ve hře byla jen jako past.
        var content = TestData.LoadRealContent();

        foreach (var def in content.Buildings.All)
        {
            if (def.CatchesLogs)
            {
                Assert.True(def.Raft!.CatchMultiplier >= 1.0, $"'{def.Id}' plavením dřevo ztrácí");
            }
        }
    }

    [Fact]
    public void RiverFlowGoesDownhillAndIsAlwaysTheSame()
    {
        var river = new SlopingRiver();

        Assert.True(river.TryRiverFlow(0, 0, out int dx, out int dy));
        Assert.Equal(1, dx);
        Assert.Equal(0, dy);

        // Dvakrát tentýž dotaz musí dát tutéž odpověď — bez toho by se tok
        // mezi dvěma spuštěními hry lišil.
        Assert.True(river.TryRiverFlow(0, 0, out int dx2, out int dy2));
        Assert.Equal((dx, dy), (dx2, dy2));
    }

    [Fact]
    public void ALogFloatsDownstream()
    {
        var rafts = new LogRaftSystem(new SlopingRiver());
        Assert.True(rafts.TryDrop(0, 0, resourceIndex: 0, amount: 5));

        TickRafts(rafts, ticks: 40, out _);

        var log = Assert.Single(rafts.Logs);
        Assert.True(log.X > 0, "kláda se nehnula z místa");
    }

    [Fact]
    public void ALogDroppedOnDryLandNeverStarts()
    {
        var rafts = new LogRaftSystem(new UniformTerrain(1));

        Assert.False(rafts.TryDrop(0, 0, resourceIndex: 0, amount: 5));
        Assert.Equal(0, rafts.Count);
    }

    [Fact]
    public void TheBoomTakesTheLogAndDeliversMoreThanItCarried()
    {
        var rafts = new LogRaftSystem(new SlopingRiver());
        Assert.True(rafts.TryDrop(0, 0, resourceIndex: 0, amount: 5));

        double delivered = 0;
        for (int i = 0; i < 40 && rafts.Count > 0; i++)
        {
            // Česle stojí na páté dlaždici po proudu.
            rafts.Tick((x, _) => x == 5 ? 1.6 : 0, (_, amount) => delivered += amount);
        }

        Assert.Equal(0, rafts.Count);
        Assert.Equal(8, delivered, 6); // 5 × 1,6 — odměna za to, že se nemuselo vozit
    }

    [Fact]
    public void ALogThatReachesTheEndOfTheRiverSinks()
    {
        // Tohle je ta chyba, na které mechanika padá: bez pravidla by kláda na
        // konci toku zůstala stát a s každou další by rostla paměť.
        var rafts = new LogRaftSystem(new ShortRiver(length: 3));
        Assert.True(rafts.TryDrop(0, 0, resourceIndex: 0, amount: 5));

        TickRafts(rafts, ticks: 200, out double delivered);

        Assert.Equal(0, rafts.Count);
        Assert.Equal(0, delivered);
    }

    [Fact]
    public void LogsNeverPileUpBeyondTheCapacity()
    {
        var rafts = new LogRaftSystem(new SlopingRiver());

        for (int i = 0; i < LogRaftSystem.Capacity * 3; i++)
        {
            rafts.TryDrop(0, 0, resourceIndex: 0, amount: 1);
        }

        Assert.True(rafts.Count <= LogRaftSystem.Capacity);
    }

    [Fact]
    public void ALogGivesUpEvenOnARiverThatNeverEnds()
    {
        // Pojistka pro proud, který by se stočil do kruhu: i na nekonečné řece
        // musí kláda jednou zmizet.
        var rafts = new LogRaftSystem(new SlopingRiver());
        Assert.True(rafts.TryDrop(0, 0, resourceIndex: 0, amount: 5));

        TickRafts(rafts, ticks: 6000, out _);

        Assert.Equal(0, rafts.Count);
    }

    [Fact]
    public void LogsSurviveARoundTripThroughRestore()
    {
        var rafts = new LogRaftSystem(new SlopingRiver());
        Assert.True(rafts.TryDrop(0, 0, resourceIndex: 0, amount: 5));
        TickRafts(rafts, ticks: 12, out _);

        var saved = rafts.Logs.ToList();
        var loaded = new LogRaftSystem(new SlopingRiver());
        loaded.Restore(saved);

        Assert.Equal(saved, loaded.Logs.ToList());
    }

    private static void TickRafts(LogRaftSystem rafts, int ticks, out double delivered)
    {
        double total = 0;
        for (int i = 0; i < ticks; i++)
        {
            rafts.Tick(static (_, _) => 0, (_, amount) => total += amount);
        }

        delivered = total;
    }

    /// <summary>Řeka, která teče doprava donekonečna.</summary>
    private sealed class SlopingRiver : ITerrain
    {
        public byte BiomeAt(int x, int y) => 1;

        public bool TryRiverFlow(int x, int y, out int dx, out int dy)
        {
            dx = y == 0 ? 1 : 0;
            dy = 0;
            return y == 0;
        }
    }

    /// <summary>Řeka, která po pár dlaždicích končí v jezeře.</summary>
    private sealed class ShortRiver : ITerrain
    {
        private readonly int _length;

        public ShortRiver(int length) => _length = length;

        public byte BiomeAt(int x, int y) => 1;

        public bool TryRiverFlow(int x, int y, out int dx, out int dy)
        {
            dx = y == 0 && x < _length ? 1 : 0;
            dy = 0;
            return dx != 0;
        }
    }
}
