using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Odkaz: druhá prestižní vrstva. Drží, kolikrát už hráč Odkaz zanechal,
/// kolik má bodů a jaké úrovně upgradů koupil.
///
/// <para>Zodpovědnost je záměrně úzká — <b>měna a úrovně</b>. Kolik z toho
/// plyne bonusů do výroby si počítá simulace (má na to jedno místo pro všechny
/// vrstvy), systém umí jen dvě věci, které jsou vlastní jen téhle vrstvě:
/// násobič bodů Vzestupu a slevu na jeho práh.</para>
///
/// <para>Vrstva: čistá simulace, nezná render.</para>
/// </summary>
public sealed class LegacySystem
{
    /// <summary>
    /// Kam až smí sleva stlačit práh Vzestupu (podíl původního prahu).
    ///
    /// <para>Bez dolní meze by po dost úrovních vyšel práh pod startovní stav
    /// a Vzestup by šel dělat donekonečna jedním klikáním — z vrcholu progrese
    /// by se stalo tlačítko.</para>
    /// </summary>
    private const double MinRequirementShare = 0.05;

    private readonly LegacyConfig _config;
    private readonly IReadOnlyList<PrestigeUpgradeDef> _upgrades;
    private readonly int[] _levels;

    public LegacySystem(LegacyConfig config, IReadOnlyList<PrestigeUpgradeDef> upgrades)
    {
        _config = config;
        _upgrades = upgrades;
        _levels = new int[upgrades.Count];
    }

    /// <summary>Kolikrát už hráč Odkaz zanechal (0 = ještě ani jednou).</summary>
    public int Depth { get; private set; }

    /// <summary>Nevyužité body Odkazu.</summary>
    public long Points { get; private set; }

    /// <summary>Kolikátou úroveň upgradu Odkazu hráč koupil (0 = žádnou).</summary>
    public int Level(int upgradeIndex) => _levels[upgradeIndex];

    /// <summary>Cena další úrovně upgradu.</summary>
    public long Cost(int upgradeIndex) => _upgrades[upgradeIndex].CostAtLevel(_levels[upgradeIndex]);

    /// <summary>Je upgrade vykoupený na doraz?</summary>
    public bool IsMaxed(int upgradeIndex) => _levels[upgradeIndex] >= _upgrades[upgradeIndex].MaxLevel;

    /// <summary>Práh dalšího Odkazu. Roste s hloubkou, jinak by druhý přišel hned po prvním.</summary>
    public long Requirement() =>
        (long)Math.Min(
            _config.Requirement.Target * Math.Pow(_config.RequirementGrowth, Depth),
            long.MaxValue / 2);

    /// <summary>Kolik bodů Odkazu by teď zanechání Odkazu udělilo.</summary>
    /// <summary>Ladicí přídavek bodů Odkazu (cheat menu; hra sama je jinudy nedává).</summary>
    internal void DebugGrant(long amount)
    {
        if (amount > 0)
        {
            Points += amount;
        }
    }

    public long PendingPoints(long metricValue)
    {
        if (metricValue <= 0 || _config.PointsDivisor <= 0)
        {
            return 0;
        }

        double ratio = metricValue / (double)_config.PointsDivisor;
        return (long)Math.Min(Math.Pow(ratio, _config.PointsExponent), long.MaxValue / 2);
    }

    /// <summary>Připíše body a posune hloubku — volá se při zanechání Odkazu.</summary>
    public void Leave(long points)
    {
        Points += points;
        Depth++;
    }

    /// <summary>Lze upgrade koupit (prereky, dost bodů, není na maximu)?</summary>
    public PlacementResult CanBuy(int upgradeIndex)
    {
        if (IsMaxed(upgradeIndex))
        {
            return PlacementResult.Occupied;
        }

        foreach (int prereq in _upgrades[upgradeIndex].PrerequisiteIndices)
        {
            if (_levels[prereq] <= 0)
            {
                return PlacementResult.NotUnlocked;
            }
        }

        return Points < Cost(upgradeIndex) ? PlacementResult.NotEnoughResources : PlacementResult.Ok;
    }

    /// <summary>Koupí úroveň upgradu. Vrací <c>true</c>, když se opravdu koupila.</summary>
    public bool TryBuy(int upgradeIndex)
    {
        if (CanBuy(upgradeIndex) != PlacementResult.Ok)
        {
            return false;
        }

        Points -= Cost(upgradeIndex);
        _levels[upgradeIndex]++;
        return true;
    }

    /// <summary>
    /// Násobič bodů Vzestupu z upgradů Odkazu. Tohle je hlavní osa vrstvy:
    /// nezrychluje výrobu, ale <b>samotné vzestupování</b> — proto má smysl
    /// Odkaz udělat, i když tím hráč přijde o všechny upgrady Vzestupu.
    /// </summary>
    public double AscensionPointsMult() => Compose("ascension_points_mult");

    /// <summary>
    /// Kolikrát nižší je práh Vzestupu. Výsledek je omezený
    /// <see cref="MinRequirementShare"/>, aby se z Vzestupu nestala formalita.
    /// </summary>
    public double AscensionRequirementShare() => Math.Max(MinRequirementShare, 1.0 / Compose("ascension_discount"));

    /// <summary>Součin úrovní všech upgradů s daným efektem (stejné skládání jako u Vzestupu).</summary>
    private double Compose(string effect)
    {
        double mult = 1.0;
        for (int i = 0; i < _levels.Length; i++)
        {
            if (_levels[i] > 0 && _upgrades[i].Effect == effect)
            {
                mult *= _upgrades[i].MultiplierAtLevel(_levels[i]);
            }
        }

        return mult;
    }

    /// <summary>Obnova ze savu: hloubka, body a jednotlivé úrovně.</summary>
    public void Restore(int depth, long points)
    {
        Depth = Math.Max(0, depth);
        Points = Math.Max(0, points);
        Array.Clear(_levels);
    }

    /// <summary>Obnova jedné úrovně upgradu ze savu (sav zapisuje ID jednou za každou úroveň).</summary>
    public void RestoreLevel(int upgradeIndex)
    {
        if (_levels[upgradeIndex] < _upgrades[upgradeIndex].MaxLevel)
        {
            _levels[upgradeIndex]++;
        }
    }

    /// <summary>Indexy koupených úrovní pro sav — jednou za každou úroveň.</summary>
    public IEnumerable<int> PurchasedLevels()
    {
        for (int i = 0; i < _levels.Length; i++)
        {
            for (int level = 0; level < _levels[i]; level++)
            {
                yield return i;
            }
        }
    }
}
