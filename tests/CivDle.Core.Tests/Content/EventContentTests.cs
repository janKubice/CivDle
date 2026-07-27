using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Události jsou to, co v idle smyčce láme monotonii, takže jich musí být dost
/// a musí sedět do doby: nabídka oceli osadě, která neumí bronz, je horší než
/// žádná událost. Testy hlídají obojí i to, že hráč má co dělat hned na startu.
/// </summary>
public class EventContentTests
{
    [Fact]
    public void RealContent_HasEnoughEvents()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Events.Count >= 25, $"událostí má být aspoň 25, je {content.Events.Count}");
    }

    [Fact]
    public void SomeEvents_AreAvailableFromTheStart()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(1));

        int available = Eligible(content, sim);

        Assert.True(available >= 5, $"na startu má být dostupných aspoň 5 událostí, je {available}");
    }

    [Fact]
    public void LateEvents_StayLockedUntilTheirTech()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(1));

        var late = content.Events[content.Events.IndexOf("orbital_contract")];
        Assert.NotNull(late.Requirement);

        var requirement = late.Requirement!.Value;
        Assert.True(sim.EvaluateMetric(requirement.Kind, requirement.Param) < requirement.Target,
            "Orbitální kontrakt nesmí být dostupný hned na startu.");
    }

    [Fact]
    public void EveryChoice_EitherCostsOrGivesSomething()
    {
        // Volba bez ceny i bez zisku je jen „zavřít okno" — jedna taková na
        // událost je v pořádku (odmítnout), dvě znamenají, že se něco ztratilo.
        var content = TestData.LoadRealContent();

        var empty = content.Events.All
            .Where(e => e.Choices.Count(c => c.Cost.Count == 0 && c.Gain.Count == 0) > 1)
            .Select(e => e.Id)
            .ToList();

        Assert.True(empty.Count == 0,
            $"Události s víc než jednou prázdnou volbou: {string.Join(", ", empty)}");
    }

    private static int Eligible(GameContent content, Simulation sim)
    {
        int count = 0;
        foreach (var candidate in content.Events.All)
        {
            if (candidate.Requirement is not { } requirement
                || sim.EvaluateMetric(requirement.Kind, requirement.Param) >= requirement.Target)
            {
                count++;
            }
        }

        return count;
    }
}
