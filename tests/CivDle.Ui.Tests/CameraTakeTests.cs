using CivDle.Capture;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Natočená jízda kamery a časování videa.
///
/// <para>Video se nedá nahrát v reálném čase — jeden 4K snímek bez LOD trvá
/// dýl než šestnáct milisekund. Nahrává se proto jen pohyb kamery a scéna se
/// pak renderuje mimo reálný čas. Testuje se to, na čem ten trik stojí: že se
/// jízda dá vzorkovat v libovolném čase, že se stání scvrkne na pár klíčů,
/// a hlavně že se tikání simulace <b>nekumuluje s chybou</b> — jinak by se
/// video rozešlo se zvukem.</para>
/// </summary>
public sealed class CameraTakeTests
{
    private static CameraTake Take(params (double Time, float X, float Zoom)[] points)
    {
        var take = new CameraTake();
        foreach (var (time, x, zoom) in points)
        {
            take.Record(time, new Vector2(x, 0), zoom);
        }

        return take;
    }

    [Fact]
    public void AnEmptyTakeHasNoDuration()
    {
        var take = new CameraTake();

        Assert.True(take.IsEmpty);
        Assert.Equal(0, take.Duration);
    }

    [Fact]
    public void SamplingAnEmptyTakeIsHarmless()
    {
        // Prázdný záběr je chyba obsluhy, ne důvod k pádu.
        var key = new CameraTake().Sample(3.0);

        Assert.Equal(Vector2.Zero, key.Position);
    }

    [Fact]
    public void ItInterpolatesBetweenKeys()
    {
        var take = Take((0, 0, 1f), (2, 100, 3f));

        var middle = take.Sample(1.0);

        Assert.Equal(50f, middle.Position.X, 2);
        Assert.Equal(2f, middle.Zoom, 2);
    }

    [Fact]
    public void BeforeAndAfterTheTakeItStandsStill()
    {
        // Zaokrouhlení může požádat o čas o zlomek snímku za koncem; to nesmí
        // vrátit extrapolovaný nesmysl někde za mapou.
        var take = Take((1, 10, 2f), (3, 90, 4f));

        Assert.Equal(10f, take.Sample(0).Position.X, 2);
        Assert.Equal(90f, take.Sample(99).Position.X, 2);
    }

    [Fact]
    public void StandingStillCollapsesToAFewKeys()
    {
        // Půl minuty nehybné kamery v 60 fps je 1800 vzorků, ve kterých není
        // žádná informace. Musí z nich zbýt pár klíčů.
        var take = new CameraTake();
        for (int i = 0; i < 600; i++)
        {
            take.Record(i / 60.0, new Vector2(42, 7), 2f);
        }

        Assert.True(take.KeyCount <= 3, $"Stání se scvrklo jen na {take.KeyCount} klíčů.");
        Assert.Equal(42f, take.Sample(5.0).Position.X, 2);
    }

    [Fact]
    public void StandingStillStillKeepsItsLength()
    {
        // Když se klíče slučují, nesmí se přitom ztratit čas — jinak by
        // z desetivteřinového stání bylo ve videu bliknutí.
        var take = new CameraTake();
        for (int i = 0; i <= 600; i++)
        {
            take.Record(i / 60.0, new Vector2(42, 7), 2f);
        }

        Assert.Equal(10.0, take.Duration, 2);
    }

    [Fact]
    public void MovingIsNotCollapsed()
    {
        var take = new CameraTake();
        for (int i = 0; i < 60; i++)
        {
            take.Record(i / 60.0, new Vector2(i * 10, 0), 2f);
        }

        Assert.True(take.KeyCount > 50, $"Z pohybu zbylo jen {take.KeyCount} klíčů.");
    }

    [Fact]
    public void ClearingStartsOver()
    {
        var take = Take((0, 0, 1f), (1, 10, 1f));

        take.Clear();

        Assert.True(take.IsEmpty);
    }

    [Fact]
    public void FrameCountFollowsTheDuration()
    {
        Assert.Equal(VideoTiming.Fps, VideoTiming.FrameCount(1.0));
        Assert.Equal(VideoTiming.Fps * 5, VideoTiming.FrameCount(5.0));
    }

    [Fact]
    public void EvenAZeroLengthTakeRendersOneFrame()
    {
        // Nula snímků by znamenala prázdnou složku a žádné vysvětlení.
        Assert.Equal(1, VideoTiming.FrameCount(0));
        Assert.Equal(1, VideoTiming.FrameCount(-5));
    }

    [Fact]
    public void TickSchedulingNeverDriftsApart()
    {
        // Přičítání zlomků by po pár tisících snímcích ujelo o celý tik.
        // Součet dílčích dávek proto musí přesně sedět na celkovém počtu.
        int running = 0;
        for (int frame = 0; frame < 3600; frame++)
        {
            running += VideoTiming.TicksBeforeFrame(frame);
            Assert.Equal(VideoTiming.TotalTicksBy(frame), running);
        }
    }

    [Fact]
    public void OneSecondOfVideoIsOneSecondOfSimulation()
    {
        int ticks = VideoTiming.TotalTicksBy(VideoTiming.Fps);

        Assert.Equal((int)Simulation.TicksPerSecond, ticks);
    }

    [Fact]
    public void TheFfmpegCommandPointsAtTheSequence()
    {
        string command = VideoTiming.FfmpegCommand("/tmp/take");

        Assert.Contains("frame-%06d.png", command);
        Assert.Contains($"-framerate {VideoTiming.Fps}", command);
    }
}
