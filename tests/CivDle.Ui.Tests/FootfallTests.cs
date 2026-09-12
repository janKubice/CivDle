using CivDle.Rendering.Effects;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Vyšlapané cesty.
///
/// <para>Chodci jsou kulisa, která zmizí, jakmile se hráč podívá jinam.
/// Stezka je stopa, kterou po sobě nechají — jediná věc, ze které je chování
/// vidět i na prázdném náměstí. Testuje se to, co ji drží použitelnou: že
/// vzniká postupně, že zarůstá, a že si nekonečný svět nevyžere paměť.</para>
/// </summary>
public class FootfallTests
{
    [Fact]
    public void OneFootstepBarelyShows()
    {
        // Kdyby jeden došlap udělal cestu, byla by po minutě ošlapaná celá mapa.
        var map = new FootfallMap();

        map.Step(4, 4);

        Assert.True(map.WearAt(4, 4) > 0f, "po došlapu není vidět nic");
        Assert.True(map.WearAt(4, 4) < 0.2f, "jeden došlap udělal rovnou pěšinu");
    }

    [Fact]
    public void AWellTroddenRouteBecomesAPath()
    {
        var map = new FootfallMap();

        for (int i = 0; i < 60; i++)
        {
            map.Step(4, 4);
        }

        Assert.True(map.WearAt(4, 4) > 0.8f, $"ani po šedesáti došlapech tu není cesta ({map.WearAt(4, 4):0.00})");
    }

    [Fact]
    public void WearNeverRunsOffTheScale()
    {
        var map = new FootfallMap();

        for (int i = 0; i < 10_000; i++)
        {
            map.Step(1, 1);
        }

        Assert.InRange(map.WearAt(1, 1), 0f, 1f);
    }

    [Fact]
    public void UntroddenGroundIsUntouched()
    {
        Assert.Equal(0f, new FootfallMap().WearAt(123, -456));
    }

    [Fact]
    public void PathsGrowOverWhenNobodyUsesThem()
    {
        // Bez zarůstání by se po hodině hraní ošlapala celá mapa a z cest by
        // byla jednolitá plocha, jen hnědá místo zelené.
        var map = new FootfallMap();
        for (int i = 0; i < 20; i++)
        {
            map.Step(2, 2);
        }

        float fresh = map.WearAt(2, 2);
        for (int i = 0; i < 20; i++)
        {
            map.Update(2.5f);
        }

        Assert.True(map.WearAt(2, 2) < fresh, "stezka po dlouhé době nezarostla ani trochu");
    }

    [Fact]
    public void AForgottenPathStopsCostingMemory()
    {
        var map = new FootfallMap();
        map.Step(7, 7);

        for (int i = 0; i < 200; i++)
        {
            map.Update(2.5f);
        }

        Assert.Equal(0, map.Count);
    }

    [Fact]
    public void TheMapRefusesToGrowWithoutBound()
    {
        // Svět je nekonečný, paměť ne. Bez stropu by se chozením po mapě
        // nasbíraly statisíce dlaždic a rostlo by to, dokud hra nespadne.
        var map = new FootfallMap();

        for (int x = 0; x < 200; x++)
        {
            for (int y = 0; y < 200; y++)
            {
                map.Step(x, y);
            }
        }

        map.Update(3f);

        Assert.True(map.Count <= 4096, $"stezky si vzaly {map.Count} dlaždic");
    }

    [Fact]
    public void NegativeCoordinatesAreTheirOwnPlace()
    {
        // Svět jde na obě strany; kdyby se klíče přetekly, ošlapal by se na
        // druhém konci mapy kus země, po kterém nikdo nešel.
        var map = new FootfallMap();

        map.Step(-5, -9);

        Assert.True(map.WearAt(-5, -9) > 0f);
        Assert.Equal(0f, map.WearAt(5, 9));
        Assert.Equal(0f, map.WearAt(-5, 9));
        Assert.Equal(0f, map.WearAt(5, -9));
    }

    [Fact]
    public void ClearingForgetsEverything()
    {
        var map = new FootfallMap();
        map.Step(1, 2);

        map.Clear();

        Assert.Equal(0, map.Count);
        Assert.Equal(0f, map.WearAt(1, 2));
    }
}

/// <summary>
/// Jak silně je stezka vidět. Vytažené zvlášť, protože náběh krytí je to
/// jediné, co u kreslení stezek může vypadat špatně: skokem by se objevila
/// celá naráz jako položená dlažba.
/// </summary>
public class WornPathLookTests
{
    [Fact]
    public void FaintTrampleDoesNotShowAtAll()
    {
        Assert.Equal(0f, CivDle.Rendering.WornPathRenderer.Alpha(0.05f));
    }

    [Fact]
    public void APathFadesInInsteadOfPoppingUp()
    {
        // Kdyby stezka naskočila, vypadalo by to jako položená cesta, ne jako
        // ušlapaná tráva.
        float justVisible = CivDle.Rendering.WornPathRenderer.Alpha(0.13f);

        Assert.True(justVisible > 0f, "čerstvá stezka není vidět vůbec");
        Assert.True(justVisible < 0.08f, $"čerstvá stezka naskočila rovnou na {justVisible:0.00}");
    }

    [Fact]
    public void EvenTheMostWornGroundStaysTranslucent()
    {
        // Přes určitou mez by stezka přebila terén pod sebou a z hlíny by byla
        // barva, ne prosvítající zem.
        Assert.True(CivDle.Rendering.WornPathRenderer.Alpha(1f) <= 0.4f);
    }

    [Fact]
    public void MoreFootstepsMeanAStrongerPath()
    {
        Assert.True(
            CivDle.Rendering.WornPathRenderer.Alpha(0.9f) > CivDle.Rendering.WornPathRenderer.Alpha(0.4f));
    }
}
