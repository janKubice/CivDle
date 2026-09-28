namespace CivDle.Core.Config;

/// <summary>
/// Galaktická sbírka jednoho světa — co z něj hráč má v Muzeu světů
/// (svety-design.md 2.6): hvězdy, spatřenou zvěř, rekord populace a divy.
///
/// <para>Patří do profilu, ne do savu: sbírka je zkušenost hráče napříč
/// hrami (Nová hra+, archiv měst), stejně jako kronika. Proto se jen
/// přidává — horší hra ji nesmí zmenšit.</para>
/// </summary>
public sealed class WorldCollection
{
    /// <summary>ID splněných hvězd (úkoly skupin hvězda a mistrovská hvězda).</summary>
    public List<string> Stars { get; set; } = new();

    /// <summary>ID druhů zvěře, které hráč na světě viděl.</summary>
    public List<string> Fauna { get; set; } = new();

    /// <summary>Nejvíc obyvatel, kolik svět kdy měl.</summary>
    public double PeakPopulation { get; set; }

    /// <summary>Nejvíc dostavěných divů a megastruktur najednou.</summary>
    public long Wonders { get; set; }

    /// <summary>Přidá nové položky a posune rekordy nahoru; true = něco přibylo.</summary>
    public bool Record(IEnumerable<string> stars, IEnumerable<string> fauna, double population, long wonders)
    {
        bool changed = AddNew(Stars, stars) | AddNew(Fauna, fauna);
        if (population > PeakPopulation)
        {
            PeakPopulation = population;
            changed = true;
        }

        if (wonders > Wonders)
        {
            Wonders = wonders;
            changed = true;
        }

        return changed;
    }

    private static bool AddNew(List<string> target, IEnumerable<string> items)
    {
        bool added = false;
        foreach (string item in items)
        {
            if (!target.Contains(item))
            {
                target.Add(item);
                added = true;
            }
        }

        return added;
    }
}
