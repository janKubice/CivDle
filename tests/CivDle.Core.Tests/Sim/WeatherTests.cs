using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Počasí (living-map.md §2): vázané na biom, ambientní je jen atmosféra,
/// extrémní jev DOČASNĚ sníží výrobu — a nikdy nic nezničí. Stav se neukládá:
/// jev je čistá funkce (seed, čas), takže přežije save/load beze změny.
/// </summary>
public class WeatherTests
{
    private static ITerrain Grass() => new UniformTerrain(1);

    private static WeatherDef Def(string id, bool extreme, double productionMult, double weight = 1) =>
        new(id, new[] { true, true }, extreme, productionMult,
            DurationSeconds: 3600, Weight: weight,
            TintColor: new RgbColor(10, 10, 10), TintAlpha: 0.3, Particle: "rain");

    /// <summary>Populace nejí ani neroste — jinak by úbytek suroviny 0 (v testovém
    /// obsahu je zároveň „jídlo") maskoval to, co má test měřit: vliv počasí.</summary>
    private static GameContent WeatherContent(params WeatherDef[] weather) =>
        TestContent.Build(
            gameplay: TestContent.DefaultGameplay with
            {
                FoodPerPersonPerSecond = 0,
                PopulationGrowthPerSecond = 0,
            },
            weather: weather);

    [Fact]
    public void NoWeatherDefined_MeansNoEffect()
    {
        var sim = new Simulation(WeatherContent(), Grass());

        Assert.Equal(-1, sim.CurrentWeatherIndex);
        Assert.Equal(1.0, sim.WeatherProductionMult);
        Assert.False(sim.IsExtremeWeather);
    }

    [Fact]
    public void ExtremeWeather_ThrottlesProductionButNeverDestroys()
    {
        var sim = new Simulation(WeatherContent(Def("tornado", extreme: true, productionMult: 0.4)), Grass());

        Assert.True(sim.IsExtremeWeather);
        Assert.Equal(0.4, sim.WeatherProductionMult);

        // Klíčová vlastnost: jen zpomalí. Suroviny ani budovy nemizí.
        double woodBefore = sim.GetResource(0);
        for (int i = 0; i < 100; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.GetResource(0) >= woodBefore - 1e-9, "počasí nesmí ubírat suroviny");
        Assert.Empty(sim.Buildings.ToArray()); // nic nevzniklo ani nezmizelo
    }

    [Fact]
    public void AmbientWeather_DoesNotThrottleProduction()
    {
        var sim = new Simulation(WeatherContent(Def("rain", extreme: false, productionMult: 1.0)), Grass());

        Assert.False(sim.IsExtremeWeather);
        Assert.Equal(1.0, sim.WeatherProductionMult);
    }

    [Fact]
    public void Weather_IsDeterministicForSameSeed()
    {
        // Počasí se neukládá — po načtení savu (stejný seed a tik) musí vyjít stejné.
        var a = new Simulation(WeatherContent(Def("rain", false, 1.0, weight: 3), Def("tornado", true, 0.4)), Grass(), seed: 99);
        var b = new Simulation(WeatherContent(Def("rain", false, 1.0, weight: 3), Def("tornado", true, 0.4)), Grass(), seed: 99);

        for (int i = 0; i < 50; i++)
        {
            a.Tick();
            b.Tick();
            Assert.Equal(a.CurrentWeatherIndex, b.CurrentWeatherIndex);
        }
    }

    [Fact]
    public void Weather_OnlyPicksEventsValidForTheBiome()
    {
        // Jev povolený jen v biomu 0, ale město stojí v biomu 1 → nesmí se vybrat.
        var onlyOtherBiome = new WeatherDef(
            "snow", new[] { true, false }, Extreme: true, ProductionMult: 0.5,
            DurationSeconds: 3600, Weight: 1,
            TintColor: new RgbColor(1, 1, 1), TintAlpha: 0.3, Particle: "snow");

        var sim = new Simulation(WeatherContent(onlyOtherBiome), Grass()); // UniformTerrain(1) → biom 1

        Assert.Equal(-1, sim.CurrentWeatherIndex);
        Assert.Equal(1.0, sim.WeatherProductionMult);
    }

    [Fact]
    public void RealContent_HasAmbientAndExtremeWeather()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Weather.Count >= 6, $"počasí má být pestré, je {content.Weather.Count} jevů");
        Assert.Contains(content.Weather.All, w => !w.Extreme);
        Assert.Contains(content.Weather.All, w => w.Extreme);

        // Extrémní jev smí flow jen snížit — nikdy nesmí výrobu zastavit úplně ani zrychlit.
        foreach (var w in content.Weather.All)
        {
            Assert.InRange(w.ProductionMult, 0.1, 1.0);
        }
    }
}
