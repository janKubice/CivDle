using CivDle.Core.Sim;

namespace CivDle.Core.Galaxy;

/// <summary>
/// Souhrn světa, na který se hráč nedívá (svety-design.md 2.4, 7.6): zásoby,
/// toky, sklady, lidé. Mimo aktivní svět se simulace nepouští — na souhrnu se
/// jen počítá, O(počet surovin).
///
/// <para><b>Neměnný.</b> Posun v čase (<see cref="Advance"/>) vrací nový souhrn,
/// takže je to čistá funkce: stejný souhrn a stejný čas dají vždy totéž — a jde
/// to otestovat bez simulace.</para>
///
/// <para>Suroviny se drží <b>jménem</b>: každý svět má vlastní obsah a index
/// „sklo" na Duně nic neříká o indexu na Domovině. Obchod mezi světy tak páruje
/// podle ID.</para>
/// </summary>
public sealed class WorldSummary
{
    private readonly string[] _resourceIds;
    private readonly double[] _stocks;
    private readonly double[] _flows;
    private readonly double[] _caps;

    public WorldSummary(
        IReadOnlyList<string> resourceIds, IReadOnlyList<double> stocks, IReadOnlyList<double> flows,
        IReadOnlyList<double> caps, double population, double housing, double growthPerSecond)
    {
        if (stocks.Count != resourceIds.Count || flows.Count != resourceIds.Count || caps.Count != resourceIds.Count)
        {
            throw new ArgumentException("Souhrn světa: délky polí se neshodují.");
        }

        _resourceIds = resourceIds.ToArray();
        _stocks = stocks.ToArray();
        _flows = flows.ToArray();
        _caps = caps.ToArray();
        Population = Math.Max(0, population);
        Housing = Math.Max(0, housing);
        GrowthPerSecond = growthPerSecond;
    }

    /// <summary>ID surovin světa (v pořadí jeho obsahu).</summary>
    public IReadOnlyList<string> ResourceIds => _resourceIds;

    /// <summary>Zásoby.</summary>
    public IReadOnlyList<double> Stocks => _stocks;

    /// <summary>Čistý tok za sekundu (bez staveb guvernéra).</summary>
    public IReadOnlyList<double> Flows => _flows;

    /// <summary>Kapacity skladů.</summary>
    public IReadOnlyList<double> Caps => _caps;

    /// <summary>Obyvatel.</summary>
    public double Population { get; }

    /// <summary>Bydlení (strop, ke kterému lidé dorůstají).</summary>
    public double Housing { get; }

    /// <summary>Kolik lidí za sekundu přibývalo, když hráč odcházel.</summary>
    public double GrowthPerSecond { get; }

    /// <summary>
    /// Změří souhrn živého světa: zásoby a sklady z něj, toky z měřiče.
    /// </summary>
    public static WorldSummary Measure(Simulation sim, FlowMeter meter)
    {
        var resources = sim.Content.Resources;
        var ids = new string[resources.Count];
        var stocks = new double[resources.Count];
        var caps = new double[resources.Count];
        var flows = new double[resources.Count];
        for (int r = 0; r < ids.Length; r++)
        {
            ids[r] = resources[r].Id;
            stocks[r] = sim.GetResource(r);
            caps[r] = sim.GetStorageCap(r);
        }

        double growth = meter.Measure(sim, flows);
        double housing = Math.Min(sim.HousingCapacity, sim.PopulationCap);
        return new WorldSummary(ids, stocks, flows, caps, sim.Population, housing, Math.Max(0, growth));
    }

    /// <summary>
    /// Svět o <paramref name="seconds"/> později: zásoby += tok × čas, mezi nulou
    /// a stropem skladu (co se nevejde, propadne jako ve hře); lidé dorůstají
    /// k bydlení, nikdy nad něj a nikdy neubývají — hladový svět za
    /// nepřítomnosti neroste, ale ani neumírá (měkký tlak).
    /// </summary>
    public WorldSummary Advance(double seconds)
    {
        if (seconds <= 0)
        {
            return this;
        }

        var stocks = new double[_stocks.Length];
        for (int r = 0; r < stocks.Length; r++)
        {
            double ceiling = Math.Max(_caps[r], _stocks[r]); // přeplněný sklad se neořezává, jen neroste
            stocks[r] = Math.Clamp(_stocks[r] + _flows[r] * seconds, 0, _flows[r] > 0 ? ceiling : _stocks[r]);
        }

        double population = Population < Housing
            ? Math.Min(Housing, Population + GrowthPerSecond * seconds)
            : Population;
        return new WorldSummary(_resourceIds, stocks, _flows, _caps, population, Housing, GrowthPerSecond);
    }

    /// <summary>
    /// Připíše nebo odepíše surovinu (obchod s neaktivním světem). Vrací nový
    /// souhrn a kolik se opravdu pohnulo — víc, než je na skladě, odejít nemůže,
    /// víc, než se vejde, přijít taky ne.
    /// </summary>
    public WorldSummary WithDelta(string resourceId, double delta, out double applied)
    {
        int r = Array.IndexOf(_resourceIds, resourceId);
        if (r < 0)
        {
            applied = 0;
            return this;
        }

        var stocks = (double[])_stocks.Clone();
        double before = stocks[r];
        stocks[r] = Math.Clamp(before + delta, 0, Math.Max(_caps[r], before));
        applied = stocks[r] - before;
        return new WorldSummary(_resourceIds, stocks, _flows, _caps, Population, Housing, GrowthPerSecond);
    }

    /// <summary>Zásoba suroviny podle ID (0 = svět ji nezná).</summary>
    public double StockOf(string resourceId)
    {
        int r = Array.IndexOf(_resourceIds, resourceId);
        return r < 0 ? 0 : _stocks[r];
    }

    /// <summary>Tok suroviny podle ID (0 = svět ji nezná).</summary>
    public double FlowOf(string resourceId)
    {
        int r = Array.IndexOf(_resourceIds, resourceId);
        return r < 0 ? 0 : _flows[r];
    }
}
