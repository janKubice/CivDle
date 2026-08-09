using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Hromadné stavění a silnice — na <b>skutečném obsahu hry</b>, ne na testovacím.
///
/// <para>Proč zvlášť: testy na vymyšleném obsahu (jedna chalupa 1×1, jeden biom)
/// prošly, a hráč přesto hlásil „×25 staví domy bez silnic". Rozdíl dělá ostrý
/// obsah — rostoucí cena domů, stropy skladu, víc typů budov.</para>
///
/// <para>Testy jsou schválně <b>nezávislé na tom, kolik si hráč může dovolit</b>:
/// ptají se na tvar plánu a na napojení toho, co opravdu vzniklo. Kdyby visely
/// na počtu, rozbila by je každá změna cen.</para>
/// </summary>
public class BulkRoadRealContentTests
{
    /// <summary>Nejdelší souvislá řada domů, kterou vkus automatu připouští.</summary>
    private const int MaxBlockRun = 5;

    /// <summary>
    /// Rozehraná hra na skutečném obsahu: vyzkoumáno, plný sklad a terén
    /// z biomu, na kterém vybraná budova opravdu smí stát.
    /// </summary>
    private static Simulation RichGame(out GameContent content, out int buildingIndex)
    {
        content = TestData.LoadRealContent();
        (buildingIndex, byte biome) = PickBuildingAndBiome(content);

        var sim = new Simulation(content, new UniformTerrain(biome));
        TopUp(sim, content);

        // Výzkum se odemyká postupně, takže jedno kolo nestačí.
        bool researched = true;
        while (researched)
        {
            researched = false;
            for (int i = 0; i < content.Techs.Count; i++)
            {
                researched |= sim.TryResearch(i) == PlacementResult.Ok;
            }
        }

        TopUp(sim, content);
        return sim;
    }

    /// <summary>
    /// Doplní sklad. Přidání se ořezává kapacitou, takže „dej mi milion" dá
    /// jen tolik, kolik se vejde — a cena domů s jejich počtem roste. Proto se
    /// dolévá průběžně, ne jednou na začátku.
    /// </summary>
    private static void TopUp(Simulation sim, GameContent content)
    {
        for (int i = 0; i < content.Resources.Count; i++)
        {
            sim.AddResource(i, 1_000_000);
        }
    }

    /// <summary>
    /// Stavitelná budova 1×1 a souš, na které smí stát. Hledá se dvojice, ne
    /// budova a biom zvlášť — ne každý dům smí na každý terén.
    /// </summary>
    private static (int Building, byte Biome) PickBuildingAndBiome(GameContent content)
    {
        for (int i = 0; i < content.Buildings.Count; i++)
        {
            var def = content.Buildings[i];
            if (!def.Buildable || def.FootprintWidth != 1 || def.FootprintHeight != 1)
            {
                continue;
            }

            for (int biome = 0; biome < content.Biomes.Count; biome++)
            {
                if (!content.Biomes[biome].IsWater
                    && biome < def.AllowedBiomes.Length && def.AllowedBiomes[biome])
                {
                    return (i, (byte)biome);
                }
            }
        }

        throw new InvalidOperationException("v datech není stavitelná budova 1×1 na souši");
    }

    [Fact]
    public void BulkMultiplierLeavesRoomForStreets()
    {
        // Jádro hráčovy stížnosti. ×25 si místa vybírá samo, stejně jako
        // guvernér — takže má stejně jako on nechat volnou uliční mřížku.
        // Bez toho z něj vyjde slitek, do kterého se žádná ulice nevejde.
        var sim = RichGame(out var content, out int def);
        var bulk = new BulkBuilder(sim, content);

        var plan = new List<BulkSlot>();
        bulk.PlanNear(def, 120, 120, 25, plan);

        Assert.NotEmpty(plan);
        AssertNoLongRun(plan);
    }

    [Fact]
    public void EverythingBulkBuiltEndsUpConnected()
    {
        var sim = RichGame(out var content, out int def);
        var bulk = new BulkBuilder(sim, content);

        // Něco už stojí a vede k tomu cesta — ×25 se nekliká na prázdnou mapu.
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(def, 100, 100));
        sim.AddRoadTileForTest(99, 100);

        // Několik dávek za sebou s doplňováním skladu: rostoucí cena domů by
        // jinak stavbu zastavila po prvním kusu a test by neověřil nic.
        var plan = new List<BulkSlot>();
        for (int round = 0; round < 6; round++)
        {
            TopUp(sim, content);
            bulk.PlanNear(def, 120, 120, 5, plan);
            bulk.Build(def, plan);
        }

        Assert.True(sim.Buildings.Length > 5, $"postavilo se jen {sim.Buildings.Length} budov");
        AssertAllConnected(sim);
    }

    [Fact]
    public void DraggedAreaIsConnectedToo()
    {
        // Tažení je jiné gesto: obdélník nakreslil hráč, takže se do něj ulice
        // necpou. Napojený ale musí být — jinak budovy vyrábějí hůř a hráč
        // nemá jak zjistit proč.
        var sim = RichGame(out var content, out int def);
        var bulk = new BulkBuilder(sim, content);

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(def, 100, 100));
        sim.AddRoadTileForTest(99, 100);

        var plan = new List<BulkSlot>();
        TopUp(sim, content);
        bulk.PlanArea(def, 130, 130, 133, 131, plan);
        bulk.Build(def, plan);

        AssertAllConnected(sim);
    }

    private static void AssertAllConnected(Simulation sim)
    {
        int connected = 0;
        for (int i = 0; i < sim.Buildings.Length; i++)
        {
            if (sim.IsBuildingConnected(i))
            {
                connected++;
            }
        }

        Assert.True(connected == sim.Buildings.Length,
            $"napojeno jen {connected} z {sim.Buildings.Length} budov");
    }

    /// <summary>Nesmí vzniknout řada delší než blok — jinak se mezi ně nevejde ulice.</summary>
    private static void AssertNoLongRun(List<BulkSlot> plan)
    {
        var taken = new HashSet<(int X, int Y)>(plan.Select(slot => (slot.X, slot.Y)));

        foreach (var slot in plan)
        {
            int horizontal = RunLength(taken, slot.X, slot.Y, 1, 0);
            int vertical = RunLength(taken, slot.X, slot.Y, 0, 1);

            Assert.True(horizontal <= MaxBlockRun,
                $"plán má vodorovnou řadu {horizontal} domů přes ({slot.X},{slot.Y})");
            Assert.True(vertical <= MaxBlockRun,
                $"plán má svislou řadu {vertical} domů přes ({slot.X},{slot.Y})");
        }
    }

    private static int RunLength(HashSet<(int X, int Y)> taken, int x, int y, int dx, int dy)
    {
        int length = 1;
        for (int sign = -1; sign <= 1; sign += 2)
        {
            for (int step = 1; taken.Contains((x + dx * step * sign, y + dy * step * sign)); step++)
            {
                length++;
            }
        }

        return length;
    }
}
