using CivDle.Core.Config;
using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Core.WorldGen;
using CivDle.Rendering;
using CivDle.Screens;
using Xunit;
using Xunit.Abstractions;

namespace CivDle.Ui.Tests;

/// <summary>
/// Prvních pět minut z pohledu nového hráče, který dělá jen to, na co ukazuje
/// šipka — s lidskými prodlevami (dvě kliknutí za sekundu, pár sekund na
/// rozkoukání po každém novém kroku).
///
/// <para>Hlídá časovou osu úvodu na všech prověřených světech: háček („město
/// pracuje samo") do první minuty a půl, farma dřív, než dojde jídlo, a první
/// soumrak až po čtyřech minutách. Kdyby změna dat nebo průvodce osu posunula
/// zpátky k „tma ve dvou minutách", tady to spadne.</para>
/// </summary>
public class FirstFiveMinutesTests
{
    private const double ClicksPerSecond = 2;
    private const double ReactionSeconds = 4;

    private static readonly GameContent Content =
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));

    private readonly ITestOutputHelper _output;

    public FirstFiveMinutesTests(ITestOutputHelper output) => _output = output;

    public static IEnumerable<object[]> Seeds() =>
        Content.Gameplay.Onboarding.QuickStartSeeds.Select(seed => new object[] { seed });

    [Theory]
    [MemberData(nameof(Seeds))]
    public void AnObedientNewcomerHitsEveryBeatOnTime(long seed)
    {
        var timeline = Play(seed, seconds: 300);
        _output.WriteLine($"seed {seed}: {string.Join(", ", timeline.Select(kv => $"{kv.Key}@{kv.Value:0}s"))}");

        Assert.True(timeline.ContainsKey("first_click") && timeline["first_click"] <= 12,
            "první klik má přijít do pár vteřin — šipka musí ukazovat na strom hned");
        Assert.True(timeline.ContainsKey("works_alone") && timeline["works_alone"] <= 90,
            "háček „město pracuje samo“ má přijít do minuty a půl");
        Assert.True(timeline.ContainsKey("farm") && timeline["farm"] <= 180,
            "farma má stát dřív, než dojde startovní jídlo");
        Assert.False(timeline.ContainsKey("food_out"), "jídlo v prvních pěti minutách nesmí dojít");
        Assert.True(!timeline.ContainsKey("dusk") || timeline["dusk"] >= 240,
            "první soumrak má přijít až po čtyřech minutách");
        Assert.True(!timeline.ContainsKey("safe_to_close") || timeline["safe_to_close"] >= 290,
            "„klidně zavři“ nesmí hráče poslat pryč dřív, než uplyne pět minut");
    }

    /// <summary>Odehraje úvod a vrátí, kdy (v sekundách) se co stalo.</summary>
    private static Dictionary<string, double> Play(long seed, int seconds)
    {
        var preset = Content.WorldGen.Presets[Content.WorldGen.DefaultPresetIndex];
        var sim = new Simulation(Content, new ProceduralTerrain(Content.Biomes, preset, seed), seed);
        var guide = new OnboardingGuide(Content, sim, new PlayerProfile());
        var timeline = new Dictionary<string, double>();
        int farm = Content.Buildings.IndexOf("farm");
        int food = Content.Gameplay.FoodResourceIndex;

        double clickBudget = 0;
        double busyUntil = ReactionSeconds; // rozkoukání po nájezdu kamery
        int tps = (int)Simulation.TicksPerSecond;
        for (int tick = 0; tick < seconds * tps; tick++)
        {
            double now = tick / (double)tps;
            guide.Update(1f / tps, DayNightCycle.NightFactor(sim.TimeOfDay01));
            if (guide.TryTakeStepChange(out var step))
            {
                busyUntil = now + ReactionSeconds; // nový krok: chvíli čte, co po něm hra chce
                timeline.TryAdd("step:" + step.Id, now);
            }

            clickBudget = Math.Min(1, clickBudget + ClicksPerSecond / tps);
            var target = guide.Target;
            if (now >= busyUntil && clickBudget >= 1 && target.Kind != GuidePointer.None)
            {
                clickBudget -= 1;
                bool acted = target.Kind == GuidePointer.Harvest
                    ? sim.TryHarvest(target.X, target.Y, out _, out _)
                    : sim.TryPlaceBuilding(target.DefIndex, target.X, target.Y) == PlacementResult.Ok;
                if (acted)
                {
                    timeline.TryAdd("first_click", now);
                }
            }

            sim.Tick();

            var events = sim.VisualEvents;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind == VisualEventKind.Produced && events[i].ResourceIndex >= 0)
                {
                    guide.OnProduced(events[i].ResourceIndex);
                }
            }

            events.Clear();
            while (guide.TryTakeMoment(out var moment))
            {
                timeline.TryAdd(OnboardingGuide.MomentId(moment), now);
            }

            if (sim.Buildings.ToArray().Any(b => b.DefIndex == farm))
            {
                timeline.TryAdd("farm", now);
            }

            if (sim.GetResource(food) <= 0)
            {
                timeline.TryAdd("food_out", now);
            }

            if (DayNightCycle.NightFactor(sim.TimeOfDay01) > 0.05)
            {
                timeline.TryAdd("dusk", now);
            }
        }

        return timeline;
    }
}
