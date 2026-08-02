using CivDle.Core.Content;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Strom výzkumu musí být <b>ekonomicky průchozí</b>: na každou technologii
/// musí vést cesta, na jejímž konci si hráč cenu opravdu může dovolit.
///
/// <para>Proč to tu je: strom se dá lehce přerovnat tak, že vypadá správně
/// a přitom je zaseknutý — technologie odemykající bronz stála bronz, takže
/// se k bronzu nedalo dostat vůbec. Ve hře to znamenalo, že 73 ze 104 výzkumů
/// a deset surovin bylo navždy mimo dosah, a poznalo se to až podle hráče,
/// který se ptal, kde se sakra bere železo.</para>
///
/// <para>Testuje se to jako uzávěr: začni s tím, co jde získat bez výzkumu,
/// a opakovaně přidávej technologie, na které máš prerekvizity i suroviny.
/// Co nakonec zbude, je nedosažitelné.</para>
/// </summary>
public class TechReachabilityTests
{
    [Fact]
    public void RealContent_EveryTechCanEventuallyBeAfforded()
    {
        var content = TestData.LoadRealContent();
        var (done, _) = Reachable(content);

        var stuck = new List<string>();
        for (int i = 0; i < content.Techs.Count; i++)
        {
            if (!done.Contains(i))
            {
                stuck.Add(content.Techs[i].Id);
            }
        }

        Assert.True(stuck.Count == 0,
            $"Nedosažitelné výzkumy ({stuck.Count}): {string.Join(", ", stuck)}. "
            + "Technologie nesmí stát surovinu, kterou zpřístupní teprve ona sama nebo její potomek.");
    }

    [Fact]
    public void RealContent_EveryResourceCanEventuallyBeProduced()
    {
        // Surovina, na kterou nevede cesta, je mrtvý obsah: je vidět v receptech
        // a ve skladu, ale hráč ji nikdy neuvidí přibývat.
        var content = TestData.LoadRealContent();
        var (_, reach) = Reachable(content);

        var unreachable = new List<string>();
        for (int i = 0; i < content.Resources.Count; i++)
        {
            if (!reach.Contains(i))
            {
                unreachable.Add(content.Resources[i].Id);
            }
        }

        Assert.True(unreachable.Count == 0,
            $"Nedosažitelné suroviny: {string.Join(", ", unreachable)}.");
    }

    /// <summary>Uzávěr: co všechno jde postupně vyzkoumat a vyrobit.</summary>
    private static (HashSet<int> Techs, HashSet<int> Resources) Reachable(GameContent content)
    {
        var reach = new HashSet<int>();

        // Startovní zásoba a ruční sběr z biomů — tím hra začíná.
        for (int i = 0; i < content.Resources.Count; i++)
        {
            if (content.Resources[i].StartAmount > 0)
            {
                reach.Add(i);
            }
        }

        foreach (var biome in content.Biomes.All)
        {
            if (biome.ClickYield is not null)
            {
                reach.Add(biome.ClickYield.ResourceIndex);
            }
        }

        // Budovy, které nezamyká žádná technologie, jsou k dispozici hned.
        var gated = new HashSet<int>();
        foreach (var tech in content.Techs.All)
        {
            foreach (int building in tech.UnlockedBuildingIndices)
            {
                gated.Add(building);
            }
        }

        for (int i = 0; i < content.Buildings.Count; i++)
        {
            if (!gated.Contains(i))
            {
                AddOutputs(content, i, reach);
            }
        }

        var done = new HashSet<int>();
        bool progress = true;
        while (progress)
        {
            progress = false;
            for (int i = 0; i < content.Techs.Count; i++)
            {
                if (done.Contains(i) || !CanTake(content.Techs[i], done, reach))
                {
                    continue;
                }

                done.Add(i);
                progress = true;
                foreach (int building in content.Techs[i].UnlockedBuildingIndices)
                {
                    AddOutputs(content, building, reach);
                }
            }
        }

        return (done, reach);
    }

    private static bool CanTake(TechDef tech, HashSet<int> done, HashSet<int> reach)
    {
        foreach (int prereq in tech.PrerequisiteIndices)
        {
            if (!done.Contains(prereq))
            {
                return false;
            }
        }

        foreach (var cost in tech.Cost)
        {
            if (!reach.Contains(cost.ResourceIndex))
            {
                return false;
            }
        }

        return true;
    }

    private static void AddOutputs(GameContent content, int buildingIndex, HashSet<int> reach)
    {
        var recipe = content.Buildings[buildingIndex].Recipe;
        if (recipe is null)
        {
            return;
        }

        foreach (var output in recipe.Outputs)
        {
            reach.Add(output.ResourceIndex);
        }
    }
}
