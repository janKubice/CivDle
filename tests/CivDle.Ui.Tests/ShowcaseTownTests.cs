using CivDle.Capture;
using CivDle.Core.Content;
using CivDle.Core.Sim;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Ukázkové městečko postavené ve skutečném světě — tedy to, co uvidí kamera.
///
/// <para>Plán ověřuje <see cref="TownPlannerTests"/>; tady jde o něco jiného
/// a horšího: <b>plán může vyjít krásně a stavba ho nepostavit</b>. Budova
/// nesmí do biomu, do cesty se plete něco jiného, technologie chybí — a záběr
/// je pak zpola prázdný. Na obrázku to poznáš, ale až po pěti minutách
/// renderu, takže to má hlídat test.</para>
///
/// <para>Testy staví celý svět, takže nejsou nejrychlejší. Za to chytají přesně
/// tu třídu chyb, kterou by jinak našel až divák.</para>
/// </summary>
public class ShowcaseTownTests
{
    private const long Seed = 20260816;

    [Fact]
    public void MostOfThePlanActuallyGetsBuilt()
    {
        // Bez tohohle projde i městečko, ze kterého se postavila třetina —
        // plán je v pořádku, jen se do světa nevešel.
        var content = Content();
        var town = ShowcaseTown.Build(content, Seed);
        int planned = TownPlanner.Plan(Seed, ShowcaseTown.Size).Lots.Count;

        Assert.True(
            town.Simulation.Buildings.Length >= planned * 0.9,
            $"postaveno {town.Simulation.Buildings.Length} z {planned} parcel");
    }

    [Fact]
    public void TheTownIsVaried()
    {
        // Jedna kategorie = ne město, ale sídliště. Přehlídka má ukázat, že se
        // ve městě bydlí, nakupuje, pracuje i chodí do parku.
        var content = Content();
        var town = ShowcaseTown.Build(content, Seed);

        var categories = new HashSet<string>(StringComparer.Ordinal);
        var kinds = new HashSet<int>();
        foreach (var building in town.Simulation.Buildings)
        {
            categories.Add(content.Buildings[building.DefIndex].Category);
            kinds.Add(building.DefIndex);
        }

        Assert.Contains("housing", categories);
        Assert.Contains("civic", categories);
        Assert.True(kinds.Count >= 10, $"jen {kinds.Count} druhů budov");
    }

    [Fact]
    public void TheTownHasStreetsAndPeople()
    {
        var content = Content();
        var town = ShowcaseTown.Build(content, Seed);

        Assert.True(town.Simulation.Population > 0, "v městečku nikdo nebydlí");

        int roads = 0;
        for (int y = 0; y < ShowcaseTown.Size * 2; y++)
        {
            for (int x = 0; x < ShowcaseTown.Size * 2; x++)
            {
                if (town.Simulation.IsRoad(
                    (int)(town.Center.X / 16) - ShowcaseTown.Size + x,
                    (int)(town.Center.Y / 16) - ShowcaseTown.Size + y))
                {
                    roads++;
                }
            }
        }

        Assert.True(roads > 100, $"městečko má jen {roads} dlaždic silnice");
    }

    [Fact]
    public void TheLayoutStopsGrowingOnItsOwn()
    {
        // Tohle je ta chyba, kvůli které test vznikl: auto-stavba je jádro hry,
        // takže za pár vteřin přisype vlastní domy i cesty a z vyskládaných
        // bloků je souvislá mřížka. Kulisa musí zůstat taková, jak se postavila.
        var content = Content();
        var town = ShowcaseTown.Build(content, Seed);
        int before = town.Simulation.Buildings.Length;

        // Víc, než kolik se za celý záběr odtiká.
        for (int tick = 0; tick < 20 * Simulation.TicksPerSecond; tick++)
        {
            town.Simulation.Tick();
        }

        Assert.Equal(before, town.Simulation.Buildings.Length);
    }

    [Fact]
    public void EverySeedGivesAWorkingTown()
    {
        // Přehlídka jede na několika semínkách. Kdyby jedno spadlo na horách
        // nebo do vody, byl by v ní jeden prázdný záběr.
        var content = Content();

        foreach (long seed in new long[] { 20260816, 30313, 777001, 4242 })
        {
            var town = ShowcaseTown.Build(content, seed);
            int planned = TownPlanner.Plan(seed, ShowcaseTown.Size).Lots.Count;

            Assert.True(
                town.Simulation.Buildings.Length >= planned * 0.9,
                $"semínko {seed}: postaveno {town.Simulation.Buildings.Length} z {planned} parcel");
        }
    }

    private static GameContent Content() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
