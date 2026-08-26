using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Tržní konjunktury: čas od času soused platí líp.
///
/// <para>Hlídá se hlavně to, že se konjunktura <b>nikam neukládá</b> — je to
/// čistá funkce seedu, města a času. Kdyby byla náhodná za běhu, dala by se
/// vylosovat opakovaným načtením hry; kdyby se ukládala, byl by to stav v savu
/// za něco, co jde spočítat.</para>
/// </summary>
public class DemandSpikeTests
{
    [Fact]
    public void RealContentHasSpikesThatEndAndPayMore()
    {
        var content = TestData.LoadRealContent();
        var spike = content.NpcCities.Spike;

        Assert.NotNull(spike);
        Assert.True(spike!.Multiplier > 1.0, "konjunktura, která nic nepřidá, není konjunktura");
        Assert.True(spike.DurationTicks < spike.IntervalTicks, "konjunktura by nikdy neskončila");
        Assert.InRange(spike.ChancePercent, 1, 99);
    }

    [Fact]
    public void WithoutSpikesInTheDataNothingEverSpikes()
    {
        var sim = World(spike: null);

        for (int i = 0; i < 5000; i++)
        {
            sim.Tick();
            Assert.False(sim.IsCityInDemandSpike(1234));
        }
    }

    [Fact]
    public void SpikesStartAndEnd()
    {
        var sim = World(new DemandSpike(IntervalTicks: 100, DurationTicks: 20, ChancePercent: 100, Multiplier: 2.0));

        // Pravděpodobnost 100 % → okno začíná hned a po dvaceti ticích končí.
        Assert.True(sim.IsCityInDemandSpike(7));

        for (int i = 0; i < 20; i++)
        {
            sim.Tick();
        }

        Assert.False(sim.IsCityInDemandSpike(7));
    }

    [Fact]
    public void ANeverRollingSpikeNeverHappens()
    {
        var sim = World(new DemandSpike(IntervalTicks: 100, DurationTicks: 20, ChancePercent: 0, Multiplier: 2.0));

        for (int i = 0; i < 500; i++)
        {
            sim.Tick();
            Assert.False(sim.IsCityInDemandSpike(7));
        }
    }

    [Fact]
    public void TheSameWorldHasTheSameBooms()
    {
        // Dvě simulace se stejným seedem musí mít konjunktury na týž tik.
        var spike = new DemandSpike(IntervalTicks: 100, DurationTicks: 20, ChancePercent: 50, Multiplier: 2.0);
        var a = World(spike, seed: 4242);
        var b = World(spike, seed: 4242);

        for (int i = 0; i < 600; i++)
        {
            for (long city = 0; city < 8; city++)
            {
                Assert.Equal(a.IsCityInDemandSpike(city), b.IsCityInDemandSpike(city));
            }

            a.Tick();
            b.Tick();
        }
    }

    [Fact]
    public void DifferentCitiesBoomAtDifferentTimes()
    {
        // Kdyby konjunktura platila pro všechna města naráz, nebylo by na co se
        // rozhodovat — hráč by prostě počkal a prodal všude.
        var spike = new DemandSpike(IntervalTicks: 100, DurationTicks: 20, ChancePercent: 50, Multiplier: 2.0);
        var sim = World(spike, seed: 9);

        bool differed = false;
        for (int window = 0; window < 40 && !differed; window++)
        {
            bool first = sim.IsCityInDemandSpike(1);
            for (long city = 2; city < 12; city++)
            {
                if (sim.IsCityInDemandSpike(city) != first)
                {
                    differed = true;
                    break;
                }
            }

            for (int i = 0; i < 100; i++)
            {
                sim.Tick();
            }
        }

        Assert.True(differed, "všechna města měla konjunkturu ve stejnou chvíli");
    }

    private static Simulation World(DemandSpike? spike, long seed = 1)
    {
        var content = TestContent.Build(npcCities: new NpcCityCatalog(
            Array.Empty<ResourceAmount>(), 0, Array.Empty<ResourceAmount>(), 600, 100,
            Array.Empty<ResourceAmount>(), 4, 8, 1, 0.0,
            new DefRegistry<NpcCityArchetype>(
                Array.Empty<NpcCityArchetype>(), a => a.Id, "cizí město", allowEmpty: true),
            Array.Empty<string>(),
            spike));

        return new Simulation(content, new UniformTerrain(1), seed);
    }
}
