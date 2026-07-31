using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Milníky za počet budov: čím víc jich stojí, tím líp všechny vyrábějí.
///
/// <para>Proč to ve hře je: dvacátá farma byla do téhle chvíle stejně zajímavá
/// jako první — přibyla porce výroby a nic víc. Tohle je motor, na kterém stojí
/// celý žánr: stavět tutéž budovu dokola má smysl, protože každý kus posouvá
/// k viditelnému prahu, a po jeho překročení se zlepší <b>všechny</b> budovy
/// toho typu naráz.</para>
///
/// <para>Výkon: jeden průchod budovami na nízké frekvenci — spočítat počty po
/// typech a rozdat násobiče. Výsledek si každá budova nese nacachovaný
/// (<see cref="BuildingInstance.MilestoneMult"/>), takže tiková smyčka výroby
/// nic nepočítá (stejný vzor jako svoz, čtvrti a znečištění).</para>
/// </summary>
internal sealed class BuildingMilestoneSystem
{
    /// <summary>Jak často se milníky přepočítávají. Počet budov se mění pomalu.</summary>
    private const int IntervalTicks = 20;

    private readonly GameContent _content;

    /// <summary>Milníky podle indexu definice — v cyklu se pak nesahá na registr.</summary>
    private readonly BuildingMilestones?[] _milestones;

    /// <summary>Kolik budov daného typu stojí. Drží se mezi tiky, ať se nealokuje.</summary>
    private readonly long[] _counts;

    /// <summary>Násobič pro daný typ, spočítaný z počtu.</summary>
    private readonly float[] _multipliers;

    /// <summary>Je vůbec co počítat? Bez milníků v datech systém neudělá nic.</summary>
    private readonly bool _anyMilestones;

    public BuildingMilestoneSystem(GameContent content)
    {
        _content = content;
        var defs = content.Buildings.All;
        _milestones = new BuildingMilestones?[defs.Count];
        _counts = new long[defs.Count];
        _multipliers = new float[defs.Count];

        for (int i = 0; i < defs.Count; i++)
        {
            _milestones[i] = defs[i].Milestones;
            _multipliers[i] = 1f;
            _anyMilestones |= defs[i].HasMilestones;
        }
    }

    public void Tick(Simulation sim)
    {
        if (!_anyMilestones || sim.TickCount % IntervalTicks != 0)
        {
            return;
        }

        Recompute(sim);
    }

    /// <summary>Přepočítá počty i násobiče a rozdá je budovám.</summary>
    public void Recompute(Simulation sim)
    {
        if (!_anyMilestones)
        {
            return;
        }

        Array.Clear(_counts);

        var buildings = sim.BuildingsMutable;
        for (int i = 0; i < buildings.Length; i++)
        {
            // Staveniště se nepočítá: milník má být odměna za hotové město,
            // ne za rozestavěné. Jinak by šlo bonus „půjčit" rozkopanou plochou.
            if (buildings[i].IsComplete)
            {
                _counts[buildings[i].DefIndex]++;
            }
        }

        for (int i = 0; i < _multipliers.Length; i++)
        {
            _multipliers[i] = _milestones[i] is { } milestone
                ? (float)milestone.MultiplierFor(_counts[i])
                : 1f;
        }

        for (int i = 0; i < buildings.Length; i++)
        {
            buildings[i].MilestoneMult = _multipliers[buildings[i].DefIndex];
        }
    }

    /// <summary>Kolik budov daného typu se do milníků počítá.</summary>
    public long CountOf(int defIndex) =>
        defIndex >= 0 && defIndex < _counts.Length ? _counts[defIndex] : 0;

    /// <summary>Násobič výroby, který typ zrovna má.</summary>
    public double MultiplierOf(int defIndex) =>
        defIndex >= 0 && defIndex < _multipliers.Length ? _multipliers[defIndex] : 1.0;
}
