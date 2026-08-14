using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Demoverze — kam až hráč v ukázce dojde.
///
/// <para>Testy hlídají obě strany, a druhá je důležitější: <b>plná hra se nesmí
/// chovat jako demo</b>. Meze jsou v datech i v plném buildu (ignorované), takže
/// překlep v podmínce by je tiše zapnul všem — a to je chyba, kterou by nikdo
/// nenašel dřív než zákazníci.</para>
/// </summary>
public class DemoEditionTests
{
    // ----- meze samotné -----

    [Fact]
    public void TheDemoBlockHasSaneDefaults()
    {
        var demo = DemoConfig.Default;

        Assert.True(demo.PopulationCap > 0);
        Assert.True(demo.AscensionRequirement > 0);
        Assert.InRange(demo.SafeTechFraction, 0.05, 1.0);
    }

    [Fact]
    public void ATypoInTheDataCannotEmptyTheTechTree()
    {
        // Nula v datech by znamenala strom bez jediné dostupné technologie —
        // hráč by to nečetl jako demo, ale jako rozbitou hru.
        var broken = new DemoConfig(10_000, 10_000, 0.0);

        Assert.True(broken.TechCountFor(100) >= 1);
    }

    [Fact]
    public void TheTechCutIsAFractionOfTheTree()
    {
        var demo = new DemoConfig(10_000, 10_000, 0.2);

        Assert.Equal(20, demo.TechCountFor(100));
        Assert.True(demo.TechCountFor(7) >= 1, "malý strom nesmí zmizet celý");
    }

    [Fact]
    public void RealDataCarriesTheDemoBlock()
    {
        // Blok v datech je to jediné, čím jde demo doladit bez překladu hry.
        var content = TestData.LoadRealContent();

        Assert.True(content.Demo.PopulationCap > 0);
        Assert.True(content.Demo.SafeTechFraction < 1.0, "demo by nabídlo celý strom");
    }

    // ----- plná hra -----

    [Fact]
    public void TheFullGameIgnoresTheDemoLimits()
    {
        var full = World();
        var demo = DemoWorld(out _);

        // Měřítko se rozjede až s Vzestupy — teprve tam je vidět, že plná hra
        // roste dál a ukázka narazí na svůj strop.
        full.DebugGrantAscensionLevels(6);
        demo.DebugGrantAscensionLevels(6);

        Assert.False(full.ContentIsDemoForTests);
        Assert.False(full.IsTechBeyondDemo(full.TechCount - 1));
        Assert.True(full.PopulationCap > demo.PopulationCap, "plná hra narazila na demo strop");
    }

    // ----- demo -----

    [Fact]
    public void TheDemoCapsPopulation()
    {
        var sim = DemoWorld(out var content);

        Assert.True(sim.PopulationCap <= content.Demo.PopulationCap);
    }

    [Fact]
    public void TheFirstAscensionStaysNormal()
    {
        // Prestiž si má hráč osahat celou — kdyby na ni v ukázce nedosáhl,
        // neukázalo by demo tu mechaniku, na které hra stojí.
        var demo = DemoWorld(out var content);
        var full = World();

        Assert.Equal(full.AscensionRequirement(), demo.AscensionRequirement());
    }

    [Fact]
    public void TheSecondAscensionIsTheDemoFinishLine()
    {
        var sim = DemoWorld(out var content);
        sim.DebugGrantAscensionLevels(1);

        Assert.Equal(content.Demo.AscensionRequirement, sim.AscensionRequirement());
    }

    [Fact]
    public void OnlyAFractionOfTheTreeIsReachable()
    {
        var sim = DemoWorld(out var content);
        int available = content.Demo.TechCountFor(sim.TechCount);

        int open = 0;
        for (int i = 0; i < sim.TechCount; i++)
        {
            if (!sim.IsTechBeyondDemo(i))
            {
                open++;
            }
        }

        Assert.Equal(available, open);
        Assert.True(open < sim.TechCount, "ukázka nabídla celý strom");
        Assert.True(sim.IsTechBeyondDemo(sim.TechCount - 1), "poslední uzel má být zamčený");
    }

    [Fact]
    public void LockedTechsCannotBeResearchedEvenWithFullStorages()
    {
        // Zámek musí držet i proti surovinám: kdyby šlo demo obejít bohatstvím,
        // nebyl by to zámek, ale jen doporučení.
        var sim = DemoWorld(out var content);
        sim.DebugFillStorages();
        for (int r = 0; r < sim.ResourceCount; r++)
        {
            sim.AddResource(r, 1_000_000);
        }

        int locked = -1;
        for (int i = sim.TechCount - 1; i >= 0 && locked < 0; i--)
        {
            if (sim.IsTechBeyondDemo(i))
            {
                locked = i;
            }
        }

        Assert.True(locked >= 0, "v ukázce není zamčená ani jedna technologie");
        Assert.Equal(PlacementResult.NotUnlocked, sim.CanResearch(locked));
        Assert.NotEqual(PlacementResult.Ok, sim.TryResearch(locked));
    }

    [Fact]
    public void TheCutKeepsTheTreeConnected()
    {
        // Nejdůležitější test celého dema: na všechno, co je v ukázce vidět,
        // musí vést cesta. Naivní řez „prvních N v pořadí dat" tuhle podmínku
        // NESPLŇUJE — strom není psaný striktně od kořene a třeba kartografie
        // by v ukázce visela bez svého předpokladu.
        var content = TestData.LoadRealContent();
        var allowed = DemoTechSelection.Build(
            content.Techs.All, content.Demo.TechCountFor(content.Techs.Count));

        for (int i = 0; i < allowed.Length; i++)
        {
            if (!allowed[i])
            {
                continue;
            }

            foreach (int prereq in content.Techs[i].PrerequisiteIndices)
            {
                Assert.True(
                    allowed[prereq],
                    $"'{content.Techs[i].Id}' je v ukázce, ale jeho předpoklad ne");
            }
        }
    }

    [Fact]
    public void TheCutOffersAsMuchAsItPromises()
    {
        // Uzávěr nesmí výřez tiše scvrknout: kdyby vracel míň, měl by hráč
        // v ukázce míň stromu, než kolik si autor nastavil v datech.
        var content = TestData.LoadRealContent();
        int wanted = content.Demo.TechCountFor(content.Techs.Count);

        var allowed = DemoTechSelection.Build(content.Techs.All, wanted);

        Assert.Equal(wanted, allowed.Count(x => x));
    }

    // ----- pomůcky -----

    private static Simulation World()
    {
        var content = TestData.LoadRealContent();
        return new Simulation(content, new UniformTerrain(1));
    }

    private static Simulation DemoWorld(out GameContent content)
    {
        content = TestData.LoadRealContent();
        content.EnableDemoEdition();
        return new Simulation(content, new UniformTerrain(1));
    }
}
