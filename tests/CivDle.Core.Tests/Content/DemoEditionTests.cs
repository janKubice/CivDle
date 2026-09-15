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
        Assert.True(demo.TechCount >= 1);
    }

    [Fact]
    public void ATypoInTheDataCannotEmptyTheTechTree()
    {
        // Nula v datech by znamenala strom bez jediné dostupné technologie —
        // hráč by to nečetl jako demo, ale jako rozbitou hru.
        var broken = new DemoConfig(10_000, 10_000, 0, Array.Empty<string>());

        Assert.True(broken.TechCountFor(100) >= 1);
    }

    [Fact]
    public void TheTechCutIsACountNotAShareOfTheTree()
    {
        // Tohle je ten rozdíl: šestnáct uzlů je šestnáct uzlů, ať má plná hra
        // stromů kolik chce.
        var demo = new DemoConfig(1_500, 10_000, 16, Array.Empty<string>());

        Assert.Equal(16, demo.TechCountFor(100));
        Assert.Equal(16, demo.TechCountFor(1_000));
    }

    [Fact]
    public void GrowingTheFullGameDoesNotGrowTheDemo()
    {
        // Dřív to byl podíl (0,2 stromu). Každých pět technologií přidaných do
        // plné hry tím tiše přidalo jednu do ukázky — délku dema měnil kdokoli,
        // kdo doplnil obsah, a nikdo o tom nevěděl.
        var demo = new DemoConfig(1_500, 10_000, 16, Array.Empty<string>());

        Assert.Equal(demo.TechCountFor(158), demo.TechCountFor(400));
    }

    [Fact]
    public void ASmallTreeIsNeverCutBelowItself()
    {
        var demo = new DemoConfig(1_500, 10_000, 16, Array.Empty<string>());

        Assert.Equal(7, demo.TechCountFor(7));
    }

    [Fact]
    public void RealDataCarriesTheDemoBlock()
    {
        // Blok v datech je to jediné, čím jde demo doladit bez překladu hry.
        var content = TestData.LoadRealContent();

        Assert.True(content.Demo.PopulationCap > 0);
        Assert.True(
            content.Demo.TechCountFor(content.Techs.Count) < content.Techs.Count,
            "demo by nabídlo celý strom");
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
    public void OnlyASmallSliceOfTheTreeIsReachable()
    {
        var sim = DemoWorld(out var content);

        int open = 0;
        for (int i = 0; i < sim.TechCount; i++)
        {
            if (!sim.IsTechBeyondDemo(i))
            {
                open++;
            }
        }

        Assert.True(open >= content.Demo.TechIds.Count, "ukázka nenabídla ani to, co má vyjmenované");
        Assert.True(open < sim.TechCount / 4, $"ukázka otevřela {open} ze {sim.TechCount} uzlů");
        Assert.True(sim.IsTechBeyondDemo(sim.TechCount - 1), "poslední uzel má být zamčený");
    }

    [Fact]
    public void TheDemoNeverReachesTheIndustrialBranch()
    {
        // Uzávěr přes předpoklady umí ukázku tiše protáhnout přes půl stromu:
        // sklady, akvadukty i balon visí přes toolsmithing na celé železné
        // větvi až po parní stroj. Demo má být první hodina hry, ne exkurze
        // do průmyslu — a tohle je jediný způsob, jak to uhlídat, protože se
        // to nepozná jinak než vyzkoušením.
        var sim = DemoWorld(out var content);

        foreach (string beyond in new[] { "steam_power", "electrification", "electronics", "iron_working" })
        {
            int index = content.Techs.IndexOf(beyond);
            Assert.True(
                index < 0 || sim.IsTechBeyondDemo(index),
                $"'{beyond}' se do ukázky dostal přes předpoklady");
        }
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

    [Fact]
    public void TheDemoAllowsExactlyOneAscension()
    {
        // „Jeden Vzestup" znamená, že na druhý se nedá dosáhnout: práh musí
        // ležet NAD stropem obyvatel. Kdyby byly stejné, visel by druhý Vzestup
        // přesně na stropu a hráč by ho po dlouhém dojezdu dostal — z ukázky by
        // byla plná hra se zpožděním.
        var content = TestData.LoadRealContent();

        Assert.True(
            content.Demo.AscensionRequirement > content.Demo.PopulationCap,
            $"druhý Vzestup ({content.Demo.AscensionRequirement}) je v dosahu stropu "
            + $"({content.Demo.PopulationCap})");
    }

    [Fact]
    public void TheDemoEndsWhereThePlayerCanSeeIt()
    {
        // Strop musí být nad prahem prvního Vzestupu, jinak hráč narazí na zeď
        // dřív, než mu ukázka stihne ukázat prestiž — tedy to hlavní.
        var demo = DemoWorld(out var content);

        Assert.True(
            content.Demo.PopulationCap > demo.AscensionRequirement(),
            $"strop ({content.Demo.PopulationCap}) je pod prvním Vzestupem "
            + $"({demo.AscensionRequirement()})");
    }

    [Fact]
    public void TheDemoPicksItsTechsByName()
    {
        // Prvních N v pořadí souboru je špatný vzorek: v prvních šestnácti je
        // deset technologií, které neodemknou nic viditelného. Hráč by v ukázce
        // desetkrát bádal a desetkrát se mu nic nového neobjevilo.
        var content = TestData.LoadRealContent();

        Assert.True(content.Demo.HasCuratedTechs, "ukázka nemá vybraný výzkum");
    }

    [Fact]
    public void MostDemoResearchUnlocksSomething()
    {
        // Odemykání je ta odměna, kvůli které se bádá dál. V ukázce, která má
        // hodinu, musí většina kroků něco přinést.
        var content = TestData.LoadRealContent();

        int withUnlocks = 0;
        foreach (string id in content.Demo.TechIds)
        {
            int index = content.Techs.IndexOf(id);
            if (content.Techs[index].UnlockedBuildingIndices.Count > 0)
            {
                withUnlocks++;
            }
        }

        Assert.True(
            withUnlocks * 2 > content.Demo.TechIds.Count,
            $"jen {withUnlocks} z {content.Demo.TechIds.Count} kroků výzkumu něco odemkne");
    }

    [Fact]
    public void TheNamedListIsExactlyWhatTheDemoOffers()
    {
        var content = TestData.LoadRealContent();
        content.EnableDemoEdition();
        var sim = new Simulation(content, new UniformTerrain(1));

        foreach (string id in content.Demo.TechIds)
        {
            Assert.False(
                sim.IsTechBeyondDemo(content.Techs.IndexOf(id)),
                $"'{id}' je v seznamu ukázky, ale zamčený");
        }
    }

    [Fact]
    public void NamingATechAlsoBringsItsPrerequisites()
    {
        // Kdyby se předpoklad nedosypal, visel by v ukázce uzel, ke kterému
        // nevede cesta — a to vypadá jako chyba, ne jako hranice dema.
        var content = TestData.LoadRealContent();

        var allowed = DemoTechSelection.BuildFrom(content.Techs.All, content.Demo.TechIds);

        for (int i = 0; i < allowed.Length; i++)
        {
            if (!allowed[i])
            {
                continue;
            }

            foreach (int prereq in content.Techs[i].PrerequisiteIndices)
            {
                Assert.True(allowed[prereq], $"'{content.Techs[i].Id}' visí bez předpokladu");
            }
        }
    }

    [Fact]
    public void AnEmptyListFallsBackToCounting()
    {
        // Zpětná slučitelnost: data bez seznamu se mají chovat jako dřív.
        var demo = new DemoConfig(1_500, 10_000, 16, Array.Empty<string>());

        Assert.False(demo.HasCuratedTechs);
        Assert.Equal(16, demo.TechCountFor(158));
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
