using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Ředitel obsahu: nepřidává žádný obsah, jen vybírá, co a kdy ukázat.
///
/// <para>Testuje se přesně to, kvůli čemu vznikl: tipy chodí, jen když je na co
/// upozornit, neopakují se dokola, a událost se vybere podle toho, co městu
/// zrovna chybí — kupec s dřevem přijde, když došlo dřevo.</para>
/// </summary>
public class ContentDirectorTests
{
    private const int Wood = 0;
    private const int Stone = 1;

    /// <summary>Dvě události: jedna dává dřevo, druhá kámen. Jinak identické.</summary>
    private static GameContent Content(bool withEvents = true)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("plain") };
        var resources = new[]
        {
            // Půl skladu: ani nedostatek, ani přetečení — „zdravé" výchozí město.
            new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 50, BaseStorage: 100),
            new Resource("stone", new RgbColor(1, 1, 1), StartAmount: 50, BaseStorage: 100),
        };

        var events = withEvents
            ? new[]
            {
                new EventDef("wood_trader", new[]
                {
                    new EventChoiceDef("event.wood_trader.take",
                        Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(Wood, 30) }),
                }),
                new EventDef("stone_trader", new[]
                {
                    new EventChoiceDef("event.stone_trader.take",
                        Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(Stone, 30) }),
                }),
            }
            : Array.Empty<EventDef>();

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodResourceIndex = Wood,
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
        };

        return TestContent.Build(biomes, 1, resources, gameplay: gameplay, events: events);
    }

    private static (Simulation Sim, ContentDirector Director) NewGame(bool withEvents = true)
    {
        var content = Content(withEvents);
        var sim = new Simulation(content, new UniformTerrain(1));
        return (sim, new ContentDirector(content, seed: 7));
    }

    /// <summary>Nechá běžet čas po vteřinových krocích a posbírá, co ředitel navrhl.</summary>
    private static List<DirectorDecision> Run(Simulation sim, ContentDirector director, int seconds)
    {
        var decisions = new List<DirectorDecision>();
        for (int i = 0; i < seconds; i++)
        {
            var decision = director.Advance(sim, 1.0);
            if (decision.Cue != DirectorCue.None)
            {
                decisions.Add(decision);
            }
        }

        return decisions;
    }

    [Fact]
    public void AHealthyCityIsLeftAlone()
    {
        // Nejdůležitější vlastnost: když je všechno v pořádku, ředitel mlčí.
        // Tip o ničem je horší než ticho.
        var (sim, director) = NewGame(withEvents: false);

        var decisions = Run(sim, director, 300);

        Assert.Empty(decisions);
    }

    [Fact]
    public void FullStorageGetsPointedOut()
    {
        var (sim, director) = NewGame(withEvents: false);
        sim.AddResource(Wood, 10_000); // sklad má strop 100 → přeteče
        sim.AddResource(Stone, 10_000);

        var decisions = Run(sim, director, 120);

        Assert.Contains(decisions, d => d.Cue == DirectorCue.Hint && d.HintKey == "hint.storageFull");
    }

    [Fact]
    public void TheSameHintDoesNotNagOverAndOver()
    {
        // Tentýž problém dokola je otrava, ne pomoc.
        var (sim, director) = NewGame(withEvents: false);
        sim.AddResource(Wood, 10_000);
        sim.AddResource(Stone, 10_000);

        var decisions = Run(sim, director, 400);

        int storageHints = decisions.Count(d => d.HintKey == "hint.storageFull");
        Assert.Equal(1, storageHints);
    }

    [Fact]
    public void TheMerchantBringsWhateverRanOut()
    {
        // Tohle je celý smysl ředitele: tentýž kupec z týchž dat, ale ve chvíli,
        // kdy dává smysl.
        var (sim, director) = NewGame();
        sim.AddResource(Wood, -sim.GetResource(Wood)); // dřevo došlo, kámen zůstal

        var decisions = Run(sim, director, 900);
        var offered = decisions.FirstOrDefault(d => d.Cue == DirectorCue.Event);

        Assert.Equal(DirectorCue.Event, offered.Cue);
        Assert.Equal(0, offered.EventIndex); // kupec s dřevem, ne s kamenem
    }

    [Fact]
    public void WithNothingScarceItStillOffersSomething()
    {
        // Když nic nechybí, událost je jen zpestření — ale pořád má přijít.
        var (sim, director) = NewGame();

        var decisions = Run(sim, director, 900);

        Assert.Contains(decisions, d => d.Cue == DirectorCue.Event);
    }

    [Fact]
    public void EventsStayRareEvenOverALongSession()
    {
        // Vyskakovací okno bere hráči kontrolu — v relaxační hře musí být vzácné.
        var (sim, director) = NewGame();

        var decisions = Run(sim, director, 3600);

        int events = decisions.Count(d => d.Cue == DirectorCue.Event);
        Assert.InRange(events, 1, 9); // hodina hry → jednotky událostí, ne desítky
    }

    [Fact]
    public void WithoutEventContentItOnlyEverHints()
    {
        var (sim, director) = NewGame(withEvents: false);
        sim.AddResource(Wood, 10_000);
        sim.AddResource(Stone, 10_000);

        var decisions = Run(sim, director, 900);

        Assert.NotEmpty(decisions);
        Assert.DoesNotContain(decisions, d => d.Cue == DirectorCue.Event);
    }
}
