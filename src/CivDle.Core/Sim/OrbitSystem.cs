using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Co má hráč na oběžné dráze a co se zrovna chystá vypustit.
///
/// <para><b>Proč to není budova:</b> družice nemá dlaždici. Nedá se u ní
/// rozhodnout „kam", jen „jestli", a co dělá, dělá pro celé impérium naráz.
/// Kdyby se stavěla na mapu, byla by to jen drahá budova s velkým bonusem —
/// takhle je to koncová meta, na kterou se dá dívat.</para>
///
/// <para><b>Stav je pole čísel.</b> Kolik čeho je nahoře a co se staví. Poloha
/// družice na dráze se dopočítá z tiku (<see cref="AngleAt"/>), takže se
/// neukládá a nemůže se rozejít — tentýž tik dá tentýž obrázek.</para>
///
/// <para>Vrstva: čistá simulace. Placení a přepočet bonusů dělá
/// <see cref="Simulation"/>; tenhle systém drží stav a pravidla.</para>
/// </summary>
public sealed class OrbitSystem
{
    private readonly OrbitCatalog _catalog;
    private readonly int[] _launched;

    private int _buildingIndex = -1;
    private int _ticksLeft;

    public OrbitSystem(OrbitCatalog catalog)
    {
        _catalog = catalog;
        _launched = new int[catalog.Count];
    }

    /// <summary>Staví se zrovna nějaká družice? (−1 = ne.)</summary>
    public int UnderConstruction => _buildingIndex;

    /// <summary>Kolik tiků do startu zbývá.</summary>
    public int TicksLeft => _ticksLeft;

    /// <summary>Kolik družic tohohle druhu je nahoře.</summary>
    public int CountOf(int satelliteIndex) => _launched[satelliteIndex];

    /// <summary>Kolik družic je nahoře celkem (pro HUD a achievementy).</summary>
    public int TotalLaunched
    {
        get
        {
            int total = 0;
            for (int i = 0; i < _launched.Length; i++)
            {
                total += _launched[i];
            }

            return total;
        }
    }

    /// <summary>Postup rozestavěné družice 0–1; 0, když se nic nestaví.</summary>
    public double Progress
    {
        get
        {
            if (_buildingIndex < 0)
            {
                return 0;
            }

            int total = Math.Max(1, _catalog[_buildingIndex].BuildTicks);
            return Math.Clamp(1.0 - (_ticksLeft / (double)total), 0, 1);
        }
    }

    /// <summary>Je tenhle druh na svém stropu?</summary>
    public bool IsFull(int satelliteIndex) => _launched[satelliteIndex] >= _catalog[satelliteIndex].MaxCount;

    /// <summary>
    /// Cena další družice tohohle druhu. Počítá se z toho, kolik jich už je
    /// nahoře <b>i s tou rozestavěnou</b>: kdyby se rozestavěná nepočítala,
    /// dala by se cena obejít tím, že si hráč vypustí dvě naráz.
    /// </summary>
    public IReadOnlyList<ResourceAmount> NextCost(int satelliteIndex)
    {
        var def = _catalog[satelliteIndex];
        var cost = new ResourceAmount[def.Cost.Count];
        for (int i = 0; i < cost.Length; i++)
        {
            int resource = def.Cost[i].ResourceIndex;
            cost[i] = new ResourceAmount(
                resource, (int)Math.Round(def.CostOf(_launched[satelliteIndex], resource)));
        }

        return cost;
    }

    /// <summary>Zahájí stavbu. Volá se až po zaplacení — cenu řeší simulace.</summary>
    public void BeginLaunch(int satelliteIndex)
    {
        _buildingIndex = satelliteIndex;
        _ticksLeft = _catalog[satelliteIndex].BuildTicks;
    }

    /// <summary>
    /// Posune stavbu o tik. Vrací index družice, která právě vzlétla, jinak −1 —
    /// simulace podle toho přepočítá bonusy a vyhlásí to hráči.
    /// </summary>
    public int Tick()
    {
        if (_buildingIndex < 0)
        {
            return -1;
        }

        if (--_ticksLeft > 0)
        {
            return -1;
        }

        int launched = _buildingIndex;
        _launched[launched]++;
        _buildingIndex = -1;
        _ticksLeft = 0;
        return launched;
    }

    /// <summary>
    /// Sundá jednu družici z dráhy. Vrací false, když tam žádná není.
    /// </summary>
    public bool Dismantle(int satelliteIndex)
    {
        if (_launched[satelliteIndex] <= 0)
        {
            return false;
        }

        _launched[satelliteIndex]--;
        return true;
    }

    /// <summary>Zruší rozestavěnou družici (bez vrácení surovin — start už běží).</summary>
    public void CancelConstruction()
    {
        _buildingIndex = -1;
        _ticksLeft = 0;
    }

    /// <summary>Vyprázdní dráhu. Vzestup je nový svět, ne pokračování.</summary>
    public void Reset()
    {
        Array.Clear(_launched);
        CancelConstruction();
    }

    /// <summary>
    /// Kde je i-tá družice daného druhu v tiku <paramref name="tick"/> (úhel
    /// v radiánech).
    ///
    /// <para>Funkce tiku, ne stav: obrázek se tím nikdy nerozejde se savem
    /// a orbitální pohled se dá otevřít kdykoli bez „dopočítávání".
    /// Družice téhož druhu se rozestoupí rovnoměrně po dráze, aby se
    /// nepřekrývaly v jednom bodě.</para>
    /// </summary>
    public double AngleAt(int satelliteIndex, int ordinal, long tick, double ticksPerSecond)
    {
        var def = _catalog[satelliteIndex];
        double seconds = tick / Math.Max(1.0, ticksPerSecond);
        double spread = Math.Tau * ordinal / Math.Max(1, def.MaxCount);

        // Sudé dráhy obíhají opačným směrem — jinak se všechny družice hýbou
        // jako jeden kus a obrázek působí jako otáčející se tapeta.
        double direction = satelliteIndex % 2 == 0 ? 1.0 : -1.0;
        return (seconds * def.Speed * Math.Tau * direction) + spread + (satelliteIndex * 0.7);
    }

    /// <summary>Obnoví stav ze savu.</summary>
    public void Restore(IReadOnlyList<int> counts, int buildingIndex, int ticksLeft)
    {
        Array.Clear(_launched);
        for (int i = 0; i < _launched.Length && i < counts.Count; i++)
        {
            _launched[i] = Math.Max(0, Math.Min(counts[i], _catalog[i].MaxCount));
        }

        // Neplatný index (save z jiného obsahu nebo z modu, který družici ubral)
        // znamená „nic se nestaví" — ne pád při načtení.
        _buildingIndex = buildingIndex >= 0 && buildingIndex < _launched.Length ? buildingIndex : -1;
        _ticksLeft = _buildingIndex >= 0 ? Math.Max(1, ticksLeft) : 0;
    }

    /// <summary>Počty pro save (v pořadí druhů).</summary>
    public IReadOnlyList<int> CountsForSave() => _launched;
}
