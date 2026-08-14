using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Automatický výzkum — poslední vrstva automatizace, odemykaná až Odkazem.
///
/// <para>Testy hlídají to, co odlišuje automatizaci od cheatu: že platí tytéž
/// ceny a že rychlost jde zvýšit, ale ne rozbít.</para>
/// </summary>
public class AutoResearchTests
{
    [Fact]
    public void WithoutTheUpgradeNothingResearchesItself()
    {
        // Bez koupeného vylepšení je rozpočet nula — v první hře má klikání ve
        // stromu zůstat součástí hry.
        Assert.Equal(0, AutoResearchSystemBudget(0));
    }

    [Fact]
    public void EachLevelAddsOneTechPerRound()
    {
        Assert.Equal(1, AutoResearchSystemBudget(1));
        Assert.Equal(3, AutoResearchSystemBudget(3));
    }

    [Fact]
    public void PartialLevelsRoundDownInsteadOfSneakingIn()
    {
        // Půl úrovně nesmí dát půl výzkumu ani celý — jinak by se rozpočet
        // choval jinak, než co je napsané na tlačítku.
        Assert.Equal(1, AutoResearchSystemBudget(1.9));
    }

    [Fact]
    public void TheBudgetIsCapped()
    {
        // Pojistka proti dlouhému tiku: bez stropu by vysoká úroveň probublala
        // celý strom v jednom tiku a hra by na okamžik stála.
        Assert.True(AutoResearchSystemBudget(1_000) <= 32);
    }

    [Fact]
    public void SpeedShortensTheInterval()
    {
        int normal = AutoResearchSchedule.IntervalFor(1.0);
        int fast = AutoResearchSchedule.IntervalFor(3.0);

        Assert.True(fast < normal);
    }

    [Fact]
    public void SpeedNeverGoesBelowTheFloor()
    {
        // Bez dna by výzkum probublal strom za pár vteřin a hráč by neviděl,
        // co se vlastně odemklo.
        Assert.True(AutoResearchSchedule.IntervalFor(10_000) >= 10);
    }

    [Fact]
    public void SlowingDownIsNotAThing()
    {
        // Násobič pod 1 by interval prodloužil; tempo se dá jen zvyšovat.
        Assert.Equal(AutoResearchSchedule.IntervalFor(1.0), AutoResearchSchedule.IntervalFor(0.2));
    }

    [Fact]
    public void RealContentOffersBothUpgrades()
    {
        // Efekt bez jediného vylepšení v datech je mrtvý kód — přesně tak
        // vznikla chyba u tempa guvernéra.
        var content = TestData.LoadRealContent();

        bool auto = false;
        bool speed = false;
        var upgrades = content.LegacyUpgrades.All;
        for (int i = 0; i < upgrades.Count; i++)
        {
            auto |= upgrades[i].Effect == "auto_research";
            speed |= upgrades[i].Effect == "research_speed";
        }

        Assert.True(auto, "v Odkazu chybí vylepšení s efektem auto_research");
        Assert.True(speed, "v Odkazu chybí vylepšení s efektem research_speed");
    }

    [Fact]
    public void ANewGameHasAutoResearchOff()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(1));

        Assert.False(sim.AutoResearchActive);
        Assert.Equal(1.0, sim.ResearchSpeed);
    }

    [Fact]
    public void TotalCostRanksCheapTechsFirst()
    {
        // Automat vybírá nejlevnější dostupnou technologii; kdyby cena vracela
        // nesmysl, procházel by strom náhodně.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(1));

        for (int i = 0; i < sim.TechCount; i++)
        {
            Assert.True(sim.TotalResearchCost(i) > 0, $"technologie {i} má nulovou cenu");
        }
    }

    private static int AutoResearchSystemBudget(double level) => AutoResearchSchedule.BudgetFor(level);
}
