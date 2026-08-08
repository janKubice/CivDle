using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Rychlost guvernéra jde vyzkoumat (bod 37).
///
/// <para>Tempo automatiky bylo v simulaci dávno napojené na bonus
/// <c>autobuild_speed</c>, ale v datech nebyl jediný výzkum, který by ho dával —
/// takže guvernér zůstal pomalý napořád a hráč s tím nemohl nic dělat.</para>
/// </summary>
public class GovernorSpeedTests
{
    [Fact]
    public void RealContentHasResearchThatSpeedsTheGovernorUp()
    {
        var content = TestData.LoadRealContent();

        int speedUps = 0;
        double total = 0;
        for (int i = 0; i < content.Techs.Count; i++)
        {
            var tech = content.Techs[i];
            if (tech.Effect == "autobuild_speed")
            {
                speedUps++;
                total += tech.Magnitude;
            }
        }

        Assert.True(speedUps >= 2, $"na rychlost guvernéra vede jen {speedUps} výzkumů");
        Assert.True(total >= 0.5, $"dohromady zrychlí jen o {total:P0} — to hráč nepozná");
    }

    [Fact]
    public void ResearchingThemActuallyShortensTheInterval()
    {
        // Test jde přes skutečný obsah a skutečný výzkum: kdyby se efekt jmenoval
        // v datech jinak, než ho čte simulace, tenhle test to chytí — a přesně
        // tak vznikla původní chyba (bonus se počítal, ale nikdo ho nedával).
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        int before = sim.AutoBuildInterval;

        // Skladiště napřed: cena středověkého výzkumu se do základního skladu
        // nevejde (planks 120 strop vs. 120 × násobič ceny), takže „nasypat
        // suroviny" samo o sobě nestačí — hráč to řeší sklady a test taky.
        BuildWarehouses(sim, content, 8);

        foreach (string id in new[] { "milling", "trade", "masonry", "public_works" })
        {
            TopUp(sim, content);
            Assert.Equal(PlacementResult.Ok, sim.TryResearch(content.Techs.IndexOf(id)));
        }

        Assert.True(sim.AutoBuildInterval < before,
            $"interval se nezkrátil ({before} → {sim.AutoBuildInterval})");
    }

    /// <summary>Postaví řádku skladišť, aby se do zásob vešly ceny pozdějších výzkumů.</summary>
    private static void BuildWarehouses(Simulation sim, GameContent content, int count)
    {
        int warehouse = content.Buildings.IndexOf("warehouse");
        for (int i = 0; i < count; i++)
        {
            TopUp(sim, content);
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(warehouse, i * 3, 0));
        }
    }

    /// <summary>Naplní všechny sklady na strop (strop se sklady zvedá, proto se volá opakovaně).</summary>
    private static void TopUp(Simulation sim, GameContent content)
    {
        for (int i = 0; i < content.Resources.Count; i++)
        {
            sim.AddResource(i, sim.GetStorageCap(i));
        }
    }
}
