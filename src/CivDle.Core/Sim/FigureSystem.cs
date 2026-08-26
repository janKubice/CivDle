using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>Osobnost, která právě žije.</summary>
/// <param name="FigureIndex">Kdo to je.</param>
/// <param name="BornTick">Kdy se narodila.</param>
public readonly record struct LivingFigure(int FigureIndex, long BornTick);

/// <summary>
/// Kdo se ve městě narodil, co po dobu života zlepšuje a co po něm zbude.
///
/// <para><b>Efekt nesmí přežít nositele.</b> To je ta jediná věc, která se
/// u téhle mechaniky kazí: bonus se přičte při narození a zapomene se odečíst
/// při úmrtí — a hráč pak má napořád dvojnásobný výzkum po člověku, který
/// zemřel před třemi érami. Proto se bonusy nesčítají do proměnné, ale
/// <b>počítají znovu ze seznamu žijících</b>; mrtvý ze seznamu zmizí a tím
/// zmizí i jeho vliv.</para>
///
/// <para><b>Socha není bonus navíc.</b> Nahradí ho — je to normální budova
/// s pasivním efektem, takže se po smrti sníží, ne zdvojí.</para>
///
/// <para>Vrstva: čistá simulace. Deterministické: kdo se narodí, plyne
/// z milníku a ze seedu, ne z náhody za běhu.</para>
/// </summary>
public sealed class FigureSystem
{
    private readonly FigureCatalog _catalog;
    private readonly List<LivingFigure> _living = new();
    private readonly List<int> _remembered = new();

    public FigureSystem(FigureCatalog catalog) => _catalog = catalog;

    /// <summary>Kdo právě žije.</summary>
    public IReadOnlyList<LivingFigure> Living => _living;

    /// <summary>Kdo se za celý běh narodil (i po smrti — kvůli kronice a sochám).</summary>
    public IReadOnlyList<int> Remembered => _remembered;

    /// <summary>Žije zrovna někdo?</summary>
    public bool HasLiving => _living.Count > 0;

    /// <summary>
    /// Narodí se osobnost vázaná na tenhle milník. Vrací její index, nebo −1,
    /// když na milník nikdo nečeká nebo už se narodila.
    /// </summary>
    public int OnMilestone(int milestoneIndex, long tick)
    {
        if (!_catalog.IsEnabled)
        {
            return -1;
        }

        for (int i = 0; i < _catalog.Count; i++)
        {
            if (_catalog[i].MilestoneIndex != milestoneIndex || _remembered.Contains(i))
            {
                continue;
            }

            _living.Add(new LivingFigure(i, tick));
            _remembered.Add(i);
            return i;
        }

        return -1;
    }

    /// <summary>
    /// Posune čas. Vrací index osobnosti, která právě zemřela, nebo −1.
    ///
    /// <para>Jedna za tik stačí: víc jich naráz nezemře, protože se rodí
    /// na různých milnících.</para>
    /// </summary>
    public int Tick(long tick)
    {
        for (int i = 0; i < _living.Count; i++)
        {
            if (tick - _living[i].BornTick < _catalog[_living[i].FigureIndex].LifeTicks)
            {
                continue;
            }

            int died = _living[i].FigureIndex;
            _living.RemoveAt(i);
            return died;
        }

        return -1;
    }

    /// <summary>
    /// Násobič od žijících osobností pro daný efekt.
    ///
    /// <para>Počítá se ze seznamu, ne z uložené sumy — mrtvý ze seznamu zmizel
    /// a tím zmizel i jeho vliv. Uložená suma je přesně ta cesta, kterou se
    /// bonus po mrtvém udrží napořád.</para>
    /// </summary>
    public double MultiplierFor(string effect)
    {
        double multiplier = 1.0;
        for (int i = 0; i < _living.Count; i++)
        {
            var def = _catalog[_living[i].FigureIndex];
            if (string.Equals(def.Effect, effect, StringComparison.Ordinal))
            {
                multiplier *= 1.0 + def.Magnitude;
            }
        }

        return multiplier;
    }

    /// <summary>Kolik jí zbývá života (0–1). Pro UI.</summary>
    public double LifeLeft(LivingFigure figure, long tick)
    {
        long life = Math.Max(1, _catalog[figure.FigureIndex].LifeTicks);
        return Math.Clamp(1.0 - ((tick - figure.BornTick) / (double)life), 0, 1);
    }

    /// <summary>Vyprázdní (Vzestup, nový svět).</summary>
    public void Reset()
    {
        _living.Clear();
        _remembered.Clear();
    }

    /// <summary>Obnova ze savu.</summary>
    public void Restore(IEnumerable<LivingFigure> living, IEnumerable<int> remembered)
    {
        _living.Clear();
        _remembered.Clear();

        foreach (var figure in living)
        {
            if (figure.FigureIndex >= 0 && figure.FigureIndex < _catalog.Count)
            {
                _living.Add(figure);
            }
        }

        foreach (int index in remembered)
        {
            if (index >= 0 && index < _catalog.Count && !_remembered.Contains(index))
            {
                _remembered.Add(index);
            }
        }
    }
}
