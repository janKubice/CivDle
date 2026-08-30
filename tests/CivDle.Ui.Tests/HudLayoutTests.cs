using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Screens;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Objeví se v liště tlačítko ve chvíli, kdy si ho hráč odemkl?
///
/// <para>Tenhle test vznikl z konkrétní chyby: lišta se přestavovala jen podle
/// počtu odemčených funkcí ze <c>features.json</c>. Kosmodrom, zvonohra ani
/// první bod Vzestupu v tom seznamu nejsou a přijdou pozdě — v době, kdy je
/// odemčené všechno a počítadlo se nehne. Orbita tedy fungovala, jen se k ní
/// nedalo dostat, protože tlačítko se do lišty nikdy nedostalo.</para>
/// </summary>
public class HudLayoutTests
{
    [Fact]
    public void AFinishedCarillonPutsItsButtonInTheBar()
    {
        var content = LoadContent();
        var sim = NewWorld(content);
        var layout = new HudLayout(content, sim);

        Assert.False(sim.HasCarillon);
        Assert.False(layout.HasChanged(sim), "hned po vytvoření se nic změnit nemohlo");

        // Nejdřív se lišta musí ustálit. Právě v tomhle stavu chyba žila:
        // dokud ještě přibývaly odemčené funkce, přestavovala se lišta při
        // každé z nich a všechno vypadalo v pořádku.
        Settle(sim, layout, content);

        int carillon = content.Buildings.IndexOf("carillon");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(carillon, sim.CityCenterX + 6, sim.CityCenterY + 6));

        Assert.True(sim.HasCarillon);
        Assert.True(layout.HasChanged(sim), "zvonohra stojí, a lišta se o tom nedozvěděla");

        // A jen jednou: přestavovat lištu každý snímek by bylo horší než ji
        // nepřestavět vůbec.
        Assert.False(layout.HasChanged(sim));
    }

    [Fact]
    public void AnOrdinaryHouseDoesNotRebuildTheBar()
    {
        var content = LoadContent();
        var sim = NewWorld(content);

        var layout = new HudLayout(content, sim);
        Settle(sim, layout, content);

        int house = content.Buildings.IndexOf("house");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(house, sim.CityCenterX + 20, sim.CityCenterY + 24));
        Assert.False(layout.HasChanged(sim), "obyčejná chalupa přestavěla celou lištu");
    }

    [Fact]
    public void TheBarNoticesTheFirstPrestigePoint()
    {
        var content = LoadContent();
        var sim = NewWorld(content);
        var layout = new HudLayout(content, sim);
        Settle(sim, layout, content);

        sim.DebugGrantPrestigePoints(3);

        Assert.True(layout.HasChanged(sim), "body Vzestupu jsou, a doktríny se v liště neobjevily");
    }

    /// <summary>
    /// Postaví tolik chalup, aby se odemklo všechno, co jde odemknout počtem
    /// budov — a lištu tím <b>ustálí</b>. Teprve za tímhle bodem má smysl se
    /// ptát, jestli si všimne něčeho dalšího: přesně tady totiž stará verze
    /// přestala cokoli hlásit.
    /// </summary>
    private static void Settle(Simulation sim, HudLayout layout, GameContent content)
    {
        int house = content.Buildings.IndexOf("house");
        for (int i = 0; i < 16; i++)
        {
            sim.TryPlaceBuildingFree(house, sim.CityCenterX + 20 + i, sim.CityCenterY + 20);
            layout.HasChanged(sim);
        }

        Assert.False(layout.HasChanged(sim), "lišta se neustálila — test by neověřil nic");
    }

    private static Simulation NewWorld(GameContent content)
    {
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();
        sim.DebugFillStorages();
        return sim;
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
