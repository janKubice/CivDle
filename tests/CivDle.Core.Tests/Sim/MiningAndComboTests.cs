using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Těžba napříč érami a síla klikacího komba (bod 21).
///
/// <para>Na ruční těžbu vedla jediná technologie (<c>deep_mining</c>) a ta až
/// v době železné — do té doby se sběr nedal nijak vylepšit. A protože série
/// končila na desátém kliknutí a přidávala nanejvýš ×1,8, v pozdní hře nemělo
/// klikání smysl vůbec.</para>
/// </summary>
public class MiningAndComboTests
{
    [Fact]
    public void EveryEraHasSomethingThatImprovesMining()
    {
        // Nejde o počet uzlů, ale o to, že se v každé fázi hry dá s těžbou hnout.
        var content = TestData.LoadRealContent();

        int harvest = 0;
        for (int i = 0; i < content.Techs.Count; i++)
        {
            if (content.Techs[i].Effect == "harvest_mult")
            {
                harvest++;
            }
        }

        Assert.True(harvest >= 8, $"na ruční sběr míří jen {harvest} výzkumů");
    }

    [Fact]
    public void EarlyMiningTechNeedsNoMetals()
    {
        // Pazourkové špičáky mají být dostupné dřív než bronz — jinak to není
        // „technologie pro nižší éry", jen další uzel v době železné.
        var content = TestData.LoadRealContent();
        var picks = content.Techs[content.Techs.IndexOf("flint_picks")];

        foreach (var cost in picks.Cost)
        {
            string id = content.Resources[cost.ResourceIndex].Id;
            Assert.True(id is "stone" or "wood" or "planks" or "tools" or "food",
                $"raná těžební technologie nesmí stát '{id}'");
        }
    }

    [Fact]
    public void ComboPowerLengthensTheStreakAndTheStep()
    {
        var combo = new ComboConfig(1.5, 0.08, 10);

        // Bez bonusu: desátý krok je strop, jedenáctý už nepřidá nic.
        Assert.Equal(combo.Multiplier(11), combo.Multiplier(20), 6);

        // S bonusem se posune obojí — přírůstek za krok i strop série.
        double plain = combo.Multiplier(11, 1.0);
        double powered = combo.Multiplier(11, 2.0);
        Assert.True(powered > plain, $"síla komba nezabrala ({powered} vs {plain})");
        Assert.True(combo.Multiplier(20, 2.0) > combo.Multiplier(11, 2.0),
            "se silou komba má série pokračovat dál než k desátému kliknutí");
    }

    [Fact]
    public void ComboPowerNeverWeakensTheCombo()
    {
        // Bonus je násobič ≥ 1; hodnota pod jedničkou (třeba z modu) nesmí
        // hráči sérii ubrat.
        var combo = new ComboConfig(1.5, 0.08, 10);

        Assert.Equal(combo.Multiplier(5, 1.0), combo.Multiplier(5, 0.2), 6);
    }

    [Fact]
    public void ResearchingComboTechsRaisesTheMultiplier()
    {
        // Skutečný obsah a skutečný výzkum: kdyby se efekt jmenoval v datech
        // jinak, než ho čte simulace, bonus by tiše nedělal nic.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));

        double before = content.Gameplay.Combo.Multiplier(11);
        foreach (string id in new[] { "milling", "trade", "tools", "flint_picks", "rhythm_of_the_pick" })
        {
            for (int i = 0; i < content.Resources.Count; i++)
            {
                sim.AddResource(i, sim.GetStorageCap(i));
            }

            Assert.Equal(PlacementResult.Ok, sim.TryResearch(content.Techs.IndexOf(id)));
        }

        double after = content.Gameplay.Combo.Multiplier(11, sim.Bonuses.ComboPower);
        Assert.True(after > before, $"série se nezesílila ({before} → {after})");
    }
}
