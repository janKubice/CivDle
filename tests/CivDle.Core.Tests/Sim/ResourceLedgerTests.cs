using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Evidence toků surovin.
///
/// <para>Vznikla kvůli otázce, na kterou hra neuměla odpovědět: „proč mi
/// nepřibývá dřevo?" Ze skladu se pozná jen čistý rozdíl, takže „nevyrábí se"
/// a „vyrábí se a hned spotřebuje" vypadaly stejně. Testy hlídají, že se ty
/// dva stavy rozliší — a že se pozná i třetí, nejzákeřnější: vyrábí se, ale
/// propadá to do plného skladu.</para>
/// </summary>
public class ResourceLedgerTests
{
    private const int Tps = 10;

    [Fact]
    public void AFreshLedgerReportsNothing()
    {
        var ledger = new ResourceLedger(3);

        Assert.Equal(0, ledger.ProducedPerSecond(0));
        Assert.Equal(0, ledger.ConsumedPerSecond(0));
        Assert.Equal(0, ledger.NetPerSecond(0));
    }

    [Fact]
    public void SteadyProductionConvergesToTheRealRate()
    {
        // Deset za tik při deseti ticích za sekundu = sto za sekundu.
        var ledger = new ResourceLedger(1);

        for (int tick = 0; tick < 400; tick++)
        {
            ledger.RecordProduced(0, 10);
            ledger.EndTick(Tps);
        }

        Assert.Equal(100, ledger.ProducedPerSecond(0), 1);
    }

    [Fact]
    public void ProductionAndConsumptionAreToldApart()
    {
        // Tohle je celý důvod existence: čistý tok je nula, ale ekonomika
        // rozhodně nestojí — a hráč to musí vidět.
        var ledger = new ResourceLedger(1);

        for (int tick = 0; tick < 400; tick++)
        {
            ledger.RecordProduced(0, 5);
            ledger.RecordConsumed(0, 5);
            ledger.EndTick(Tps);
        }

        Assert.Equal(50, ledger.ProducedPerSecond(0), 1);
        Assert.Equal(50, ledger.ConsumedPerSecond(0), 1);
        Assert.Equal(0, ledger.NetPerSecond(0), 1);
    }

    [Fact]
    public void WasteIsItsOwnNumber()
    {
        // Plný sklad výrobu nezastaví, přebytek mizí. Je to záměr hry, ale
        // hráč to nemá jak zjistit — bez tohohle ukazatele.
        var ledger = new ResourceLedger(1);

        for (int tick = 0; tick < 400; tick++)
        {
            ledger.RecordProduced(0, 2);
            ledger.RecordWasted(0, 8);
            ledger.EndTick(Tps);
        }

        Assert.Equal(20, ledger.ProducedPerSecond(0), 1);
        Assert.Equal(80, ledger.WastedPerSecond(0), 1);
    }

    [Fact]
    public void BurstyProductionIsSmoothedInsteadOfFlickering()
    {
        // Dílna dokončí cyklus v jednom tiku a pak deset tiků nic. Bez
        // vyhlazení by ukazatel blikal mezi nulou a špičkou.
        var ledger = new ResourceLedger(1);

        for (int tick = 0; tick < 600; tick++)
        {
            if (tick % 10 == 0)
            {
                ledger.RecordProduced(0, 10); // 10 za deset tiků = 10/s
            }

            ledger.EndTick(Tps);
        }

        double afterBurst = ledger.ProducedPerSecond(0);
        for (int tick = 0; tick < 5; tick++)
        {
            ledger.EndTick(Tps); // tiky bez výroby
        }

        // Hodnota se drží kolem skutečné rychlosti, nespadne na nulu.
        Assert.InRange(afterBurst, 5, 15);
        Assert.InRange(ledger.ProducedPerSecond(0), 3, 15);
    }

    [Fact]
    public void ResourcesDoNotBleedIntoEachOther()
    {
        var ledger = new ResourceLedger(3);

        for (int tick = 0; tick < 200; tick++)
        {
            ledger.RecordProduced(1, 4);
            ledger.EndTick(Tps);
        }

        Assert.Equal(0, ledger.ProducedPerSecond(0));
        Assert.True(ledger.ProducedPerSecond(1) > 30);
        Assert.Equal(0, ledger.ProducedPerSecond(2));
    }

    [Fact]
    public void ProductionThatStopsFadesToZero()
    {
        // Zbouraná pila musí z ukazatele zmizet, ne tam viset navěky.
        var ledger = new ResourceLedger(1);
        for (int tick = 0; tick < 300; tick++)
        {
            ledger.RecordProduced(0, 10);
            ledger.EndTick(Tps);
        }

        Assert.True(ledger.ProducedPerSecond(0) > 50);

        for (int tick = 0; tick < 300; tick++)
        {
            ledger.EndTick(Tps);
        }

        Assert.True(ledger.ProducedPerSecond(0) < 0.5, "ukazatel se nevrátil na nulu");
    }

    [Fact]
    public void NegativeAmountsAreIgnored()
    {
        // Chyba volajícího nesmí ukazatel rozhodit do záporu.
        var ledger = new ResourceLedger(1);

        ledger.RecordProduced(0, -5);
        ledger.RecordConsumed(0, -5);
        ledger.EndTick(Tps);

        Assert.Equal(0, ledger.ProducedPerSecond(0));
        Assert.Equal(0, ledger.ConsumedPerSecond(0));
    }

    [Fact]
    public void ResetClearsEverything()
    {
        // Vzestup smete město; předchozí ekonomika už nic neříká.
        var ledger = new ResourceLedger(1);
        for (int tick = 0; tick < 200; tick++)
        {
            ledger.RecordProduced(0, 10);
            ledger.EndTick(Tps);
        }

        ledger.Reset();

        Assert.Equal(0, ledger.ProducedPerSecond(0));
        Assert.Equal(0, ledger.ConsumedPerSecond(0));
        Assert.Equal(0, ledger.WastedPerSecond(0));
    }

    // ----- v opravdové simulaci -----

    [Fact]
    public void TheLedgerSeesRealProduction()
    {
        // Evidence uvnitř hry, ne v laboratoři: postav dílnu, nech ji běžet
        // a ukazatel musí ožít. Kdyby se zapojení do ProductionSystem někdy
        // vytratilo, tenhle test to chytí — unit testy nad samotným ledgerem ne.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("forest")));
        sim.SkipTutorial();

        Assert.True(content.Buildings.TryIndexOf("lumber_camp", out int camp));
        sim.DebugFillStorages();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(camp, 2, 2));

        for (int tick = 0; tick < 600; tick++)
        {
            sim.Tick();
        }

        int wood = content.Resources.IndexOf("wood");
        Assert.True(
            sim.Ledger.ProducedPerSecond(wood) > 0 || sim.Ledger.WastedPerSecond(wood) > 0,
            "dřevorubec těžil, ale evidence o tom neví");
    }

    [Fact]
    public void BuildingSomethingCountsAsConsumption()
    {
        // Útraty jdou přes jediné místo (Pay). Tenhle test hlídá, že tudy
        // opravdu jdou všechny — u devíti volání by se na jedno zapomnělo.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();
        sim.DebugFillStorages();

        Assert.True(content.Buildings.TryIndexOf("house", out int house));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 5, 5));
        sim.Tick();

        int planks = content.Resources.IndexOf("planks");
        Assert.True(
            sim.Ledger.ConsumedPerSecond(planks) > 0,
            "za postavený dům se nezaúčtovala žádná spotřeba prken");
    }

    [Fact]
    public void AFullStoreShowsUpAsWaste()
    {
        // Nejzákeřnější stav: vyrábí se naplno, zásoba se nehne, protože to
        // přetéká. Ze samotného skladu to vypadá jako „nevyrábí se".
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("forest")));
        sim.SkipTutorial();

        Assert.True(content.Buildings.TryIndexOf("lumber_camp", out int camp));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(camp, 2, 2));

        int wood = content.Resources.IndexOf("wood");
        for (int tick = 0; tick < 900; tick++)
        {
            sim.AddResource(wood, sim.GetStorageCap(wood)); // pořád plno
            sim.Tick();
        }

        Assert.True(
            sim.Ledger.WastedPerSecond(wood) > 0,
            "dřevo přetékalo, ale propad se nikde neprojevil");
    }

    [Fact]
    public void TotalConsumptionIsTheSumOfItsKinds()
    {
        var ledger = new ResourceLedger(1);
        for (int tick = 0; tick < 400; tick++)
        {
            ledger.RecordConsumed(0, 1, ConsumptionKind.People);
            ledger.RecordConsumed(0, 2, ConsumptionKind.Upkeep);
            ledger.EndTick(Tps);
        }

        Assert.Equal(10, ledger.ConsumedPerSecond(0, ConsumptionKind.People), 1);
        Assert.Equal(20, ledger.ConsumedPerSecond(0, ConsumptionKind.Upkeep), 1);
        Assert.Equal(30, ledger.ConsumedPerSecond(0), 1);
    }

    [Fact]
    public void WhatPeopleEatIsConsumption()
    {
        // Tooltip jídla dřív ukazoval „spotřeba 0", zatímco ho lidé jedli
        // a zásoba padala k nule — přesně na otázku „proč mi to ubývá" lhal.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("forest")));
        sim.SkipTutorial();
        int food = content.Gameplay.FoodResourceIndex;
        sim.DebugSetResource(food, sim.GetStorageCap(food));

        for (int tick = 0; tick < 200; tick++)
        {
            sim.Tick();
        }

        double expected = sim.Population * content.Gameplay.FoodPerPersonPerSecond;
        Assert.True(sim.Ledger.ConsumedPerSecond(food, ConsumptionKind.People) > expected * 0.5,
            "jídlo snědené lidmi se do evidence nezapsalo");
        Assert.True(sim.Ledger.NetPerSecond(food) < 0, "čistý tok jídla bez polí musí být záporný");
    }

    [Fact]
    public void AnyWithdrawalOutsideRecipesCountsAsAPurchase()
    {
        // Volba v události, modlitba, kláda na řece — všechno bere přes AddResource.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("forest")));
        int wood = content.Resources.IndexOf("wood");
        sim.DebugSetResource(wood, 100);

        for (int tick = 0; tick < 200; tick++)
        {
            sim.AddResource(wood, -0.1);
            sim.Tick();
        }

        Assert.True(sim.Ledger.ConsumedPerSecond(wood, ConsumptionKind.Purchases) > 0.5);
    }
}
