namespace CivDle.Core.Sim;

/// <summary>Druh hráčovy akce, kterou jde vzít zpět.</summary>
public enum PlayerActionKind
{
    /// <summary>Postavil budovu.</summary>
    Build,

    /// <summary>Zbořil budovu.</summary>
    Demolish,

    /// <summary>Postavil silnici.</summary>
    Road,

    /// <summary>Zbořil silnici.</summary>
    RemoveRoad,
}

/// <summary>
/// Jedna hráčova akce a všechno, co je potřeba k jejímu vrácení.
/// </summary>
/// <param name="Kind">Co hráč udělal.</param>
/// <param name="DefIndex">Které budovy se to týkalo; −1 u silnic.</param>
/// <param name="X">Dlaždice vodorovně.</param>
/// <param name="Y">Dlaždice svisle.</param>
/// <param name="Progress">Rozestavěnost budovy v okamžiku akce (kvůli vrácení bourání).</param>
public readonly record struct PlayerAction(
    PlayerActionKind Kind, int DefIndex, int X, int Y, float Progress);

/// <summary>Proč se vrácení nepovedlo.</summary>
public enum UndoResult
{
    /// <summary>Vráceno.</summary>
    Ok,

    /// <summary>Není co vracet.</summary>
    Empty,

    /// <summary>Svět se pod akcí změnil — vracet by znamenalo rozbít, co tam mezitím vzniklo.</summary>
    WorldChanged,

    /// <summary>Na vrácení nejsou suroviny (vrácení bourání bere zpátky vrácenou půlku ceny).</summary>
    NotEnoughResources,
}

/// <summary>
/// Zásobník posledních hráčových akcí.
///
/// <para><b>Proč jen dvacet a jen hráčových.</b> Postavení domu zároveň
/// dotáhne silnice, odhalí mlhu, přepočítá sousedské bonusy, zapíše do
/// statistik a může spustit milník — „vrátit poslední akci" tedy není jedna
/// operace. Plný command-log s reverzibilní simulací je jiná liga. Tohle je
/// poctivá varianta: pamatuje si, co udělal <b>hráč</b>, umí k tomu inverzi,
/// a když se pod akcí svět změnil (guvernér tam mezitím postavil), vrácení
/// <b>odmítne</b> místo aby stav rozbilo.</para>
///
/// <para>Do savu nepatří: je to pomůcka relace, ne stav světa. Po načtení hry
/// se začíná s prázdným zásobníkem, což je poctivější než slibovat vrácení
/// akce, po které se mezitím odehrály hodiny offline dohonu.</para>
///
/// <para>Vrstva: čistá simulace, žádná znalost UI.</para>
/// </summary>
public sealed class UndoStack
{
    /// <summary>Kolik akcí se pamatuje.</summary>
    public const int Capacity = 20;

    private readonly PlayerAction[] _actions = new PlayerAction[Capacity];
    private int _count;

    /// <summary>Kolik akcí je k dispozici.</summary>
    public int Count => _count;

    /// <summary>Je co vracet?</summary>
    public bool CanUndo => _count > 0;

    /// <summary>Poslední akce, aniž by se odebrala (UI z ní píše, co se vrátí).</summary>
    public PlayerAction? Peek => _count > 0 ? _actions[_count - 1] : null;

    /// <summary>
    /// Zapíše akci. Nejstarší vypadne — dvacet kroků zpátky je hranice, za
    /// kterou už hráč stejně neví, co dělal.
    /// </summary>
    public void Push(PlayerAction action)
    {
        if (_count == Capacity)
        {
            Array.Copy(_actions, 1, _actions, 0, Capacity - 1);
            _count--;
        }

        _actions[_count++] = action;
    }

    /// <summary>Vyzvedne poslední akci.</summary>
    public bool TryPop(out PlayerAction action)
    {
        if (_count == 0)
        {
            action = default;
            return false;
        }

        action = _actions[--_count];
        return true;
    }

    /// <summary>Vyprázdní (Vzestup, načtení savu).</summary>
    public void Clear() => _count = 0;
}
