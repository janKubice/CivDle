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

    /// <summary>
    /// Poslední ohlášený stupeň typu. Bez něj by nešlo poznat, kdy hráč práh
    /// právě překročil — a překročení je ten okamžik, který stojí za oslavu.
    /// </summary>
    private readonly int[] _announcedTiers;

    /// <summary>Je vůbec co počítat? Bez milníků v datech systém neudělá nic.</summary>
    private readonly bool _anyMilestones;

    public BuildingMilestoneSystem(GameContent content)
    {
        _content = content;
        var defs = content.Buildings.All;
        _milestones = new BuildingMilestones?[defs.Count];
        _counts = new long[defs.Count];
        _multipliers = new float[defs.Count];
        _announcedTiers = new int[defs.Count];

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

    /// <summary>
    /// Přepočítá počty i násobiče a rozdá je budovám. <paramref name="announce"/>
    /// říká, jestli se má překročení prahu ohlásit — při načtení savu se ohlašovat
    /// nesmí, jinak by hráče po každém spuštění zasypaly oslavy věcí, které
    /// zvládl už minule.
    /// </summary>
    public void Recompute(Simulation sim, bool announce = true)
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

        Announce(sim, announce);
    }

    /// <summary>
    /// Ohlásí typy, které právě překročily práh. Bez tohohle byl milník tichý:
    /// výroba poskočila a hráč se o tom nedozvěděl, dokud sám neotevřel panel
    /// budovy — přesně ta odměna, kterou nikdo nezažije.
    ///
    /// <para>Klesnutí (hráč budovy zbořil) se stejným způsobem <b>nehlásí</b>,
    /// jen se tiše zapamatuje: hra netrestá a rozhodně o tom nedělá slávu.</para>
    /// </summary>
    private void Announce(Simulation sim, bool announce)
    {
        for (int i = 0; i < _milestones.Length; i++)
        {
            if (_milestones[i] is not { } milestone)
            {
                continue;
            }

            int tier = milestone.TierFor(_counts[i]);
            if (tier == _announcedTiers[i])
            {
                continue;
            }

            bool climbed = tier > _announcedTiers[i];
            _announcedTiers[i] = tier;
            if (!announce || !climbed)
            {
                continue;
            }

            // Přesný stupeň se dočte v panelu budovy; toast má říct jen „tenhle
            // typ se právě zlepšil", aby se dal přečíst koutkem oka.
            sim.EnqueueNotification(new GameNotification(
                NotificationKind.BuildingMilestone,
                "toast.buildingMilestone",
                _content.Buildings[i].NameKey));

            // Ohňostroj patří nad město, ne nad tu jednu budovu, která práh
            // shodou okolností dorazila — je to úspěch celé civilizace.
            sim.ReportVisual(VisualEventKind.MilestoneReached, sim.CityCenterX, sim.CityCenterY);
        }
    }

    /// <summary>Kolik budov daného typu se do milníků počítá.</summary>
    public long CountOf(int defIndex) =>
        defIndex >= 0 && defIndex < _counts.Length ? _counts[defIndex] : 0;

    /// <summary>Násobič výroby, který typ zrovna má.</summary>
    public double MultiplierOf(int defIndex) =>
        defIndex >= 0 && defIndex < _multipliers.Length ? _multipliers[defIndex] : 1.0;
}
