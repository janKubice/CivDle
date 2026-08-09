using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Diplomacie škáluje s velikostí města (bod 41).
///
/// <para>Dřív stálo všechno stejně: dar metropoli o sto padesáti lidech vyšel na
/// tolik co dar vesnici o čtyřiceti, a obestavět šlo obojí dvanácti domy.
/// Diplomacie tím ztratila rozhodování — nebyl důvod začínat u malých.</para>
/// </summary>
public class CityScaleTests
{
    [Fact]
    public void RealContentHasCitiesOfDifferentSize()
    {
        // Bez rozdílu ve velikosti nemá co škálovat — tohle hlídá obsah.
        var content = TestData.LoadRealContent();
        var archetypes = content.NpcCities.Archetypes;

        int smallest = int.MaxValue;
        int largest = 0;
        for (int i = 0; i < archetypes.Count; i++)
        {
            smallest = Math.Min(smallest, archetypes[i].Population);
            largest = Math.Max(largest, archetypes[i].Population);
        }

        Assert.True(largest >= smallest * 2,
            $"největší město ({largest}) není proti nejmenšímu ({smallest}) dost velké");
    }

    [Fact]
    public void BiggerCityCostsMoreToBefriendAndToBuy()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new CivDle.Core.World.UniformTerrain(1));

        var (small, big) = TwoCitiesOfDifferentSize(sim);

        Assert.True(sim.CityScale(big) > sim.CityScale(small));
        Assert.True(Total(sim.GiftCostFor(big)) > Total(sim.GiftCostFor(small)),
            "dar většímu městu musí být dražší");
        Assert.True(sim.SurroundBuildingsFor(big) > sim.SurroundBuildingsFor(small),
            "větší město se musí obestavovat víc budovami");
    }

    [Fact]
    public void BuyingScalesHarderThanGifting()
    {
        // Odkup roste strměji: metropoli si nemá jít koupit za tolik co vesnici
        // jen proto, že hráč nasyslil.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new CivDle.Core.World.UniformTerrain(1));

        var (small, big) = TwoCitiesOfDifferentSize(sim);

        double giftRatio = Total(sim.GiftCostFor(big)) / (double)Total(sim.GiftCostFor(small));
        double buyRatio = Total(sim.BuyCostFor(big)) / (double)Total(sim.BuyCostFor(small));

        Assert.True(buyRatio > giftRatio, $"odkup roste ({buyRatio:0.00}×) pomaleji než dar ({giftRatio:0.00}×)");
    }

    /// <summary>Dvě města v dosahu, která nemají stejnou velikost.</summary>
    private static (long Small, long Big) TwoCitiesOfDifferentSize(Simulation sim)
    {
        long small = 0, big = 0;
        double smallest = double.MaxValue, largest = 0;

        foreach (var city in sim.CitiesNear(0, 0, NpcCityMap.CellTiles * 6))
        {
            double scale = sim.CityScale(city.Key);
            if (scale < smallest) { smallest = scale; small = city.Key; }
            if (scale > largest) { largest = scale; big = city.Key; }
        }

        Assert.True(largest > smallest, "v dosahu nejsou dvě různě velká města");
        return (small, big);
    }

    private static long Total(IReadOnlyList<ResourceAmount> cost)
    {
        long sum = 0;
        for (int i = 0; i < cost.Count; i++)
        {
            sum += cost[i].Amount;
        }

        return sum;
    }
}
