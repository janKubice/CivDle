namespace CivDle.Core.Sim;

/// <summary>Co se ve světě stalo — render si podle druhu vybere efekt.</summary>
public enum VisualEventKind
{
    /// <summary>Budova dokončila výrobní cyklus.</summary>
    Produced,

    /// <summary>Vyrostla nová budova (hráč, guvernér nebo auto-stavba).</summary>
    BuildingPlaced,

    /// <summary>Budova se povýšila na vyšší úroveň.</summary>
    BuildingUpgraded,

    /// <summary>Blok 2×2 se sloučil v jednu velkou budovu.</summary>
    BuildingMerged,

    /// <summary>Přibyl kus silnice.</summary>
    RoadBuilt,
}

/// <summary>
/// Drobná zpráva ze simulace pro vizuální vrstvu: „na téhle dlaždici se stalo
/// tohle". Neměnná struktura, žádné reference — jde jen o pár čísel.
/// </summary>
/// <param name="Kind">Druh události.</param>
/// <param name="X">Dlaždice, kde se to stalo.</param>
/// <param name="Y">Dlaždice, kde se to stalo.</param>
/// <param name="ResourceIndex">Surovina u <see cref="VisualEventKind.Produced"/>, jinak −1.</param>
public readonly record struct VisualEvent(VisualEventKind Kind, int X, int Y, int ResourceIndex);

/// <summary>
/// Omezená fronta vizuálních událostí mezi simulací a renderem.
///
/// <para>Existuje proto, že simulace nesmí znát render (viz CLAUDE.md), ale
/// hráč potřebuje vidět, že město pracuje i když nikam neklika. Simulace sem
/// jen odloží „stalo se tohle" a render si to vyzvedne, nebo taky ne.</para>
///
/// <para><b>Přeteklé události se zahazují a to je záměr.</b> Při statisících
/// budov se za tik dokončí tisíce cyklů — vykreslit je všechny nejde ani to
/// nemá smysl. Vzorek stačí: hráč vidí, že se ve městě něco děje, ne že se
/// děje přesně 8 412 věcí. Pevná kapacita navíc znamená nulové alokace
/// v tikové smyčce.</para>
/// </summary>
public sealed class VisualEventQueue
{
    /// <summary>Kolik událostí se vejde mezi dvěma snímky renderu.</summary>
    public const int Capacity = 96;

    private readonly VisualEvent[] _events = new VisualEvent[Capacity];
    private int _count;

    /// <summary>Kolik událostí čeká na vyzvednutí.</summary>
    public int Count => _count;

    /// <summary>Kolik událostí se od posledního výběru zahodilo (plná fronta).</summary>
    public int Dropped { get; private set; }

    /// <summary>Přidá událost; při plné frontě ji zahodí (viz poznámka u třídy).</summary>
    public void Add(in VisualEvent visualEvent)
    {
        if (_count == Capacity)
        {
            Dropped++;
            return;
        }

        _events[_count++] = visualEvent;
    }

    /// <summary>Událost na daném místě fronty (render ji čte, nemaže po jedné).</summary>
    public VisualEvent this[int index] => _events[index];

    /// <summary>Vyprázdní frontu — volá render po zpracování snímku.</summary>
    public void Clear()
    {
        _count = 0;
        Dropped = 0;
    }
}
