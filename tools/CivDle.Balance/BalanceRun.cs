using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;

namespace CivDle.Balance;

/// <summary>Jeden odečet stavu města v čase — řádek výsledné křivky.</summary>
/// <param name="Minutes">Herní čas od začátku běhu.</param>
/// <param name="Population">Počet obyvatel.</param>
/// <param name="Buildings">Počet postavených budov.</param>
/// <param name="Happiness">Spokojenost 0–1.</param>
/// <param name="AscensionProgress">Postup k dalšímu Vzestupu 0–1.</param>
/// <param name="Resources">Zásoby surovin (v pořadí registru).</param>
public sealed record BalanceSample(
    double Minutes,
    double Population,
    int Buildings,
    double Happiness,
    double AscensionProgress,
    double[] Resources);

/// <summary>Výsledek celého běhu — křivky plus milníky, které hráče zajímají.</summary>
/// <param name="Samples">Odečty v čase.</param>
/// <param name="MinutesToFirstAscension">Za jak dlouho šlo poprvé Vzestoupit (null = nedosaženo).</param>
/// <param name="StalledAtMinutes">Kdy se růst zastavil na zbytek běhu (null = rostlo pořád).</param>
/// <param name="FinalBuildings">Z čeho se město nakonec skládá (ID budovy → počet), od nejčetnějších.</param>
public sealed record BalanceResult(
    IReadOnlyList<BalanceSample> Samples,
    double? MinutesToFirstAscension,
    double? StalledAtMinutes,
    IReadOnlyList<(string Id, int Count)> FinalBuildings);

/// <summary>
/// Odsimuluje hru bez okna a posbírá křivky. Simulace je deterministická, takže
/// stejný seed dá vždy stejný výsledek — balanc jde díky tomu porovnávat mezi
/// změnami dat místo aby se odhadoval „od oka".
///
/// <para>Hráče nesimuluje dokonale: staví, co jde a na co má, a klikáním sbírá
/// suroviny v pravidelném rytmu. Jde o REFERENČNÍ křivku, ne o predikci —
/// důležité je, že se stejnou metodikou porovnáš verzi před změnou a po ní.</para>
/// </summary>
public sealed class BalanceRun
{
    private readonly GameContent _content;

    /// <summary>Totéž posouzení potřeb, jaké používá guvernér ve hře.</summary>
    private readonly GovernorNeeds _needs;
    private readonly BalanceOptions _options;

    public BalanceRun(GameContent content, BalanceOptions options)
    {
        _content = content;
        _needs = new GovernorNeeds(content);
        _options = options;
    }

    public BalanceResult Run()
    {
        var preset = _content.WorldGen.Presets[_options.PresetIndex];
        var terrain = new ProceduralTerrain(_content.Biomes, preset, _options.Seed);
        var sim = new Simulation(_content, terrain, _options.Seed);

        var samples = new List<BalanceSample>();
        double? firstAscension = null;
        double? stalled = null;
        double lastPopulation = sim.Population;
        double lastGrowthMinutes = 0;

        long totalTicks = (long)(_options.Minutes * 60 * Simulation.TicksPerSecond);
        long sampleEvery = (long)(_options.SampleSeconds * Simulation.TicksPerSecond);

        for (long tick = 1; tick <= totalTicks; tick++)
        {
            sim.Tick();
            SimulatePlayer(sim, tick);

            if (firstAscension is null && sim.CanAscend())
            {
                firstAscension = Minutes(tick);
            }

            if (sim.Population > lastPopulation + 1e-6)
            {
                lastPopulation = sim.Population;
                lastGrowthMinutes = Minutes(tick);
                stalled = null;
            }
            else if (stalled is null && Minutes(tick) - lastGrowthMinutes > _options.StallMinutes)
            {
                stalled = lastGrowthMinutes;
            }

            if (tick % sampleEvery == 0)
            {
                samples.Add(Sample(sim, Minutes(tick)));
            }
        }

        return new BalanceResult(samples, firstAscension, stalled, CountBuildings(sim));
    }

    /// <summary>
    /// Z čeho se město nakonec skládá. Bez tohohle je zaseknutá křivka němá:
    /// „populace stojí na deseti" neřekne, jestli chybí pila, domy, nebo dělníci.
    /// </summary>
    private IReadOnlyList<(string Id, int Count)> CountBuildings(Simulation sim)
    {
        var counts = new int[_content.Buildings.Count];
        foreach (var building in sim.Buildings)
        {
            counts[building.DefIndex]++;
        }

        var result = new List<(string Id, int Count)>();
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] > 0)
            {
                result.Add((_content.Buildings[i].Id, counts[i]));
            }
        }

        result.Sort((a, b) => b.Count.CompareTo(a.Count));
        return result;
    }

    private static double Minutes(long tick) => tick / Simulation.TicksPerSecond / 60.0;

    private BalanceSample Sample(Simulation sim, double minutes)
    {
        var resources = new double[_content.Resources.Count];
        for (int i = 0; i < resources.Length; i++)
        {
            resources[i] = sim.GetResource(i);
        }

        long requirement = sim.AscensionRequirement();
        return new BalanceSample(
            minutes,
            sim.Population,
            sim.Buildings.Length,
            sim.Happiness,
            requirement <= 0 ? 1.0 : Math.Clamp(sim.AscensionProgress() / (double)requirement, 0, 1),
            resources);
    }

    /// <summary>
    /// Náhradní hráč: v pravidelném rytmu klikne na okolní dlaždice (sběr) a zkusí
    /// postavit nejlevnější dostupnou budovu poblíž města. Nic chytrého — jde
    /// o srovnávací základ, ne o optimální hru.
    /// </summary>
    private void SimulatePlayer(Simulation sim, long tick)
    {
        if (_options.ClicksPerMinute > 0 && tick % HarvestInterval() == 0)
        {
            int spread = 12;
            int x = sim.CityCenterX + (int)(tick / 7 % spread) - spread / 2;
            int y = sim.CityCenterY + (int)(tick / 13 % spread) - spread / 2;
            sim.TryHarvest(x, y, out _, out _, out _);
        }

        if (tick % _options.BuildIntervalTicks != 0)
        {
            return;
        }

        TryBuildSomething(sim, tick);
    }

    private long HarvestInterval() =>
        Math.Max(1, (long)(Simulation.TicksPerSecond * 60 / _options.ClicksPerMinute));

    /// <summary>
    /// Vybere, co postavit. Priorita napodobuje rozumného hráče, ne optimální hru:
    /// nejdřív zažehnat hlad, pak dostavět bydlení, pak služby a nakonec výrobu.
    ///
    /// <para>První verze stavěla prostě první budovu v pořadí registru — postavila
    /// 76 domů, ani jednu farmu a křivka pak vypadala jako zaseknutý balanc.
    /// Nesmyslná referenční křivka je horší než žádná.</para>
    /// </summary>
    private void TryBuildSomething(Simulation sim, long tick)
    {
        // Kandidáti od nejžádanějšího. Zkouší se POSTUPNĚ: když se nejlepší volba
        // nikam nevejde (špatný biom v okolí), musí přijít na řadu další — jinak
        // se běh zasekne a vypadá to jako chyba balancu, i když jde o hloupou
        // strategii náhradního hráče.
        var need = _needs.Assess(sim);
        int missingInput = need == CityNeed.Inputs ? _needs.DriedUpInput(sim) : -1;

        // Rozumný hráč nestaví dvacátou pilu pro osm lidí. Bez tohohle stropu
        // náhradní hráč zastavěl mapu a měření pak měřilo jeho hloupost, ne hru.
        bool overbuilt = sim.TotalWorkerSlots > sim.Population * 3;

        var candidates = new List<(int Index, int Score)>();
        for (int defIndex = 0; defIndex < _content.Buildings.Count; defIndex++)
        {
            if (!sim.IsBuildingBuildable(defIndex) || !sim.CanAfford(defIndex))
            {
                continue;
            }

            int score = Score(sim, _content.Buildings[defIndex], need, missingInput);
            if (score <= 0 || (overbuilt && _content.Buildings[defIndex].WorkerSlots > 0 && need != CityNeed.Food))
            {
                continue;
            }

            candidates.Add((defIndex, score));
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

        const int radius = 10;
        foreach (var (defIndex, _) in candidates)
        {
            for (int attempt = 0; attempt < 24; attempt++)
            {
                int x = sim.CityCenterX + (int)((tick + attempt * 31) % (radius * 2)) - radius;
                int y = sim.CityCenterY + (int)((tick / 3 + attempt * 17) % (radius * 2)) - radius;
                if (sim.TryPlaceBuilding(defIndex, x, y) == PlacementResult.Ok)
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Jak moc by tuhle budovu město teď chtělo.
    ///
    /// <para>Dřív dostaly všechny výrobny stejných 40 bodů, takže o pořadí
    /// rozhodovalo pořadí v souboru — náhradní hráč pak postavil šedesát pil
    /// a čtyřicet sýpek a měření vypadalo, že je rozbitý balanc. Teď se řídí
    /// týmž posouzením potřeb jako guvernér ve hře
    /// (<see cref="GovernorNeeds"/>), takže referenční křivka odpovídá tomu,
    /// jak se hra doopravdy chová.</para>
    /// </summary>
    private int Score(Simulation sim, BuildingDef def, CityNeed need, int missingInput)
    {
        int food = _content.Gameplay.FoodResourceIndex;

        switch (need)
        {
            case CityNeed.Food:
                return ProducesFood(def, food) ? 100 : 0;

            case CityNeed.Inputs:
                return missingInput >= 0 && Produces(def, missingInput) && !Consumes(def, missingInput) ? 90 : 0;

            case CityNeed.Services:
                return def.Services;

            case CityNeed.Housing:
                return def.HousingCapacity;

            default:
                // Nic akutního: rozšiřovat výrobu je legitimní, ale opatrně —
                // město bez lidí neutáhne ani to, co už stojí.
                return def.Recipe is { Outputs.Count: > 0 } ? 10 : 0;
        }
    }

    private static bool Produces(BuildingDef def, int resourceIndex)
    {
        if (def.Recipe is not { } recipe)
        {
            return false;
        }

        for (int i = 0; i < recipe.Outputs.Count; i++)
        {
            if (recipe.Outputs[i].ResourceIndex == resourceIndex)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Consumes(BuildingDef def, int resourceIndex)
    {
        if (def.Recipe is not { } recipe)
        {
            return false;
        }

        for (int i = 0; i < recipe.Inputs.Count; i++)
        {
            if (recipe.Inputs[i].ResourceIndex == resourceIndex)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ProducesFood(BuildingDef def, int foodIndex)
    {
        if (def.Recipe is not { } recipe)
        {
            return false;
        }

        for (int i = 0; i < recipe.Outputs.Count; i++)
        {
            if (recipe.Outputs[i].ResourceIndex == foodIndex)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Nastavení běhu — všechno, co má smysl měnit z příkazové řádky.</summary>
/// <param name="Minutes">Kolik herních minut odsimulovat.</param>
/// <param name="Seed">Seed světa (determinismus).</param>
/// <param name="PresetIndex">Index terénního presetu.</param>
/// <param name="SampleSeconds">Jak často se odečítá stav.</param>
/// <param name="ClicksPerMinute">Jak aktivně náhradní hráč klika (0 = vůbec).</param>
/// <param name="BuildIntervalTicks">Jak často zkusí něco postavit.</param>
/// <param name="StallMinutes">Po kolika minutách bez růstu se běh považuje za zaseknutý.</param>
public sealed record BalanceOptions(
    double Minutes = 60,
    long Seed = 12345,
    int PresetIndex = 0,
    double SampleSeconds = 60,
    double ClicksPerMinute = 30,
    int BuildIntervalTicks = 50,
    double StallMinutes = 5);
