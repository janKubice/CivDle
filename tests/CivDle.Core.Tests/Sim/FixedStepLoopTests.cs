using CivDle.Core.Sim;
using Xunit;

namespace CivDle.Core.Tests.Sim;

public class FixedStepLoopTests
{
    [Fact]
    public void Advance_PartialStep_NoTick()
    {
        var loop = new FixedStepLoop(ticksPerSecond: 10);

        Assert.Equal(0, loop.Advance(0.05));
        Assert.Equal(0, loop.Advance(0.04));
    }

    [Fact]
    public void Advance_AccumulatesAcrossCalls()
    {
        var loop = new FixedStepLoop(ticksPerSecond: 10);

        Assert.Equal(0, loop.Advance(0.06));
        // 0.06 + 0.06 = 0.12 s → jeden tik po 0.1 s, zbytek se přenáší.
        Assert.Equal(1, loop.Advance(0.06));
    }

    [Fact]
    public void Advance_MultipleTicksInOneFrame()
    {
        var loop = new FixedStepLoop(ticksPerSecond: 10);

        Assert.Equal(2, loop.Advance(0.25));
    }

    [Fact]
    public void Advance_LongLag_IsCappedAndBacklogDropped()
    {
        var loop = new FixedStepLoop(ticksPerSecond: 10, maxTicksPerAdvance: 5);

        Assert.Equal(5, loop.Advance(10.0));
        // Přebytek se zahodil — další malý krok nesmí sypat další tiky.
        Assert.Equal(0, loop.Advance(0.05));
    }

    [Fact]
    public void Advance_NegativeTime_Throws()
    {
        var loop = new FixedStepLoop(ticksPerSecond: 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => loop.Advance(-0.1));
    }
}
