using CivDle.Core.Content;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Přehled výrobních řetězců.
///
/// <para>Je to odvozený pohled na data, takže se testuje proti <b>skutečnému
/// obsahu</b>: kdyby se do dat dostala surovina, kterou nikdo nevyrábí a
/// nejde ani sebrat, byl by to slepý konec, na který hráč narazí až po
/// hodinách.</para>
/// </summary>
public class ProductionChainsTests
{
    [Fact]
    public void PlanksAreMadeFromWood()
    {
        var content = TestData.LoadRealContent();
        var chains = new ProductionChains(content);

        int planks = content.Resources.IndexOf("planks");
        int wood = content.Resources.IndexOf("wood");

        Assert.False(chains.IsRaw(planks));
        Assert.NotEmpty(chains.ProducersOf(planks));
        Assert.Contains(wood, chains.IngredientsOf(planks, content));
    }

    [Fact]
    public void WoodCanAlwaysBeMadeFromNothing()
    {
        // Dřevo je začátek všech řetězců: musí ho umět vyrobit aspoň jedna
        // budova, která k tomu nic nepotřebuje. Jinak by se hra dala zaseknout
        // do stavu, ze kterého není cesta ven.
        //
        // (Vstupy dřevo MÁ — školka ho pěstuje z jídla. To je legitimní druhá
        // cesta, ne důvod, proč by ta první nemusela existovat.)
        var content = TestData.LoadRealContent();
        var chains = new ProductionChains(content);

        int wood = content.Resources.IndexOf("wood");

        Assert.Contains(
            chains.ProducersOf(wood),
            step => content.Buildings[step.BuildingIndex].Recipe!.Inputs.Count == 0);
    }

    [Fact]
    public void EveryResourceIsEitherMadeOrGathered()
    {
        // Surovina, kterou nikdo nevyrábí a zároveň se nedá nikde sebrat,
        // je slepý konec — recept, který na ni čeká, se nikdy nespustí.
        var content = TestData.LoadRealContent();
        var chains = new ProductionChains(content);

        var orphans = new List<string>();
        for (int i = 0; i < content.Resources.Count; i++)
        {
            if (!chains.IsRaw(i))
            {
                continue;
            }

            // Sbíratelná = dá se naklikat na nějakém biomu.
            bool harvestable = content.Biomes.All.Any(b => b.ClickYield?.ResourceIndex == i);
            if (!harvestable && chains.ConsumersOf(i).Count > 0)
            {
                orphans.Add(content.Resources[i].Id);
            }
        }

        Assert.True(
            orphans.Count == 0,
            "Suroviny, které nikdo nevyrábí a nejdou ani těžit, ale někdo je potřebuje: "
            + string.Join(", ", orphans));
    }

    [Fact]
    public void ConsumersAreFoundToo()
    {
        var content = TestData.LoadRealContent();
        var chains = new ProductionChains(content);

        int wood = content.Resources.IndexOf("wood");

        // Dřevo spotřebovává aspoň pila.
        Assert.NotEmpty(chains.ConsumersOf(wood));
    }

    [Fact]
    public void TheRateIsPerSecond_NotPerCycle()
    {
        // Číslo v přehledu má být srovnatelné mezi budovami s různě dlouhým
        // cyklem — jinak by „2 za cyklus" vypadalo líp než „3 za cyklus"
        // dvakrát tak dlouhý.
        var step = new ChainStep(BuildingIndex: 0, Amount: 5, TimeTicks: 50);

        Assert.Equal(1.0, step.PerSecond, 6); // 5 kusů za 5 sekund
    }

    [Fact]
    public void AZeroLengthCycleDoesNotDivideByZero()
    {
        var step = new ChainStep(BuildingIndex: 0, Amount: 5, TimeTicks: 0);

        Assert.Equal(0, step.PerSecond);
    }
}
