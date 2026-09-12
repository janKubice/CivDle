using CivDle.Rendering.Effects;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Trh, který vyroste po příjezdu karavany.
///
/// <para>Karavana dřív dojela, vyplatila surovinu a zmizela — událost bez
/// následku, číslo, které vyskočilo a spadlo zpátky. Testuje se to, co z ní
/// dělá událost s obloukem: že má začátek, průběh a konec, a že po sobě
/// nenechá stát stánky navždycky.</para>
/// </summary>
public class MarketEventTests
{
    [Fact]
    public void ThereIsNoMarketUntilACaravanArrives()
    {
        var market = new MarketEvent();

        Assert.False(market.IsOpen);
        Assert.Equal(MarketPhase.Closed, market.Phase);
        Assert.Equal(0f, market.StallScale);
    }

    [Fact]
    public void StallsGoUpWhereTheCaravanStopped()
    {
        var market = new MarketEvent();

        market.Open(12, -4);

        Assert.True(market.IsOpen);
        Assert.Equal(12, market.TileX);
        Assert.Equal(-4, market.TileY);
        Assert.Equal(MarketPhase.RaisingStalls, market.Phase);
    }

    [Fact]
    public void StallsRiseInsteadOfPoppingIntoExistence()
    {
        // Trh, který naskočí celý naráz, vypadá jako chyba vykreslení.
        var market = new MarketEvent();
        market.Open(0, 0);

        market.Update(0.5f);
        float early = market.StallScale;

        market.Update(1f);

        Assert.True(early > 0f && early < 1f, $"stánky naskočily rovnou na {early:0.00}");
        Assert.True(market.StallScale > early, "stánky se přestaly stavět");
    }

    [Fact]
    public void OnceItIsUpThePeopleCome()
    {
        var market = Trading();

        Assert.Equal(MarketPhase.Trading, market.Phase);
        Assert.True(market.DrawsCrowd, "na otevřený trh se nikdo nehrne");
        Assert.Equal(1f, market.StallScale);
    }

    [Fact]
    public void NobodyIsCalledToStallsThatAreStillGoingUp()
    {
        var market = new MarketEvent();
        market.Open(0, 0);
        market.Update(0.5f);

        Assert.False(market.DrawsCrowd, "lidi se sbíhají k trhu, který ještě nestojí");
    }

    [Fact]
    public void TheMarketPacksUpAndLeaves()
    {
        // Tohle je ta půlka, na kterou se zapomíná: bez konce by ve městě
        // po každé karavaně zůstaly stát stánky navždycky.
        var market = Trading();

        Advance(market, 60f);

        Assert.Equal(MarketPhase.Closed, market.Phase);
        Assert.False(market.IsOpen);
        Assert.Equal(0f, market.StallScale);
    }

    [Fact]
    public void StallsComeDownGraduallyToo()
    {
        var market = Trading();
        Advance(market, 45f); // dost na to, aby se začalo balit

        Assert.Equal(MarketPhase.PackingUp, market.Phase);
        Assert.True(market.StallScale < 1f, "balení nezačalo");
        Assert.True(market.StallScale > 0f, "stánky zmizely naráz");
        Assert.False(market.DrawsCrowd, "lidi zůstali stát u sbaleného trhu");
    }

    [Fact]
    public void AnotherCaravanStartsTheWholeThingOver()
    {
        var market = Trading();
        Advance(market, 60f);

        market.Open(3, 3);

        Assert.Equal(MarketPhase.RaisingStalls, market.Phase);
        Assert.Equal(3, market.TileX);
    }

    [Fact]
    public void AClosedMarketDoesNotDriftInTime()
    {
        // Tikat naprázdno by po dlouhé hře přeteklo počítadlo a trh by se
        // sám otevřel na místě, kde nikdy žádná karavana nebyla.
        var market = new MarketEvent();

        Advance(market, 10_000f);

        Assert.Equal(MarketPhase.Closed, market.Phase);
    }

    private static MarketEvent Trading()
    {
        var market = new MarketEvent();
        market.Open(0, 0);
        Advance(market, 3f);
        return market;
    }

    private static void Advance(MarketEvent market, float seconds)
    {
        for (float t = 0; t < seconds; t += 0.1f)
        {
            market.Update(0.1f);
        }
    }
}
