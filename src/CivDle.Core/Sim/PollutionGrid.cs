using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>Co přesně se kazí. Každý druh má jiný původ i jinou nápravu.</summary>
public enum PollutionKind
{
    /// <summary>Vzduch — kouř z hutí a továren. Kazí náladu ve městě.</summary>
    Air,

    /// <summary>Voda — splašky a odpad. Podvazuje rybolov a pobřežní výrobu.</summary>
    Water,

    /// <summary>Půda — hlušina a struska. Otravuje pole v okolí.</summary>
    Soil,
}

/// <summary>
/// Znečištění na hrubé mřížce: jedna hodnota na buňku o straně
/// <see cref="CellTiles"/> dlaždic, pro každý druh zvlášť.
///
/// <para>Proč hrubá mřížka a ne hodnota u budovy: znečištění je vlastnost
/// <b>místa</b>, ne stavby. Továrna otráví okolí, ne sama sebe — a když jich
/// stojí pět vedle sebe, sečte se to. Zároveň to drží výkon: mapa je nekonečná,
/// ale buněk je řádově míň než dlaždic a vedou se jen ty, kde se něco děje
/// (CLAUDE.md: pomalé systémy na hrubých mřížkách).</para>
///
/// <para>Šíření je záměrně pomalé a s odparem: bez rozlévání by šlo znečištění
/// obejít tím, že se továrna postaví o dlaždici vedle, a bez odparu by se svět
/// nedal nikdy uzdravit.</para>
/// </summary>
public sealed class PollutionGrid
{
    /// <summary>Kolik dlaždic má strana jedné buňky.</summary>
    public const int CellTiles = 8;

    /// <summary>Pod touhle hodnotou se buňka zapomene — jinak by seznam jen rostl.</summary>
    private const double ForgetBelow = 0.0005;

    /// <summary>Hodnoty tří kanálů v jedné buňce.</summary>
    private struct Cell
    {
        public double Air;
        public double Water;
        public double Soil;

        public double Get(PollutionKind kind) => kind switch
        {
            PollutionKind.Air => Air,
            PollutionKind.Water => Water,
            _ => Soil,
        };

        public double Max => Math.Max(Air, Math.Max(Water, Soil));
    }

    private readonly Dictionary<long, Cell> _cells = new();
    private readonly List<long> _scratchKeys = new();
    private readonly Dictionary<long, Cell> _spread = new();

    /// <summary>Kolik buněk je zrovna špinavých (statistika, sav).</summary>
    public int DirtyCellCount => _cells.Count;

    /// <summary>Souřadnice buňky, do které dlaždice patří.</summary>
    public static (int X, int Y) CellOf(int tileX, int tileY) =>
        (FloorDiv(tileX, CellTiles), FloorDiv(tileY, CellTiles));

    /// <summary>Hodnota daného druhu na dlaždici (0 = čisto).</summary>
    public double At(int tileX, int tileY, PollutionKind kind)
    {
        var (cx, cy) = CellOf(tileX, tileY);
        return _cells.TryGetValue(TileKey.Pack(cx, cy), out var cell) ? cell.Get(kind) : 0;
    }

    /// <summary>Nejhorší hodnota ze všech tří druhů na dlaždici — pro barvu na mapě.</summary>
    public double WorstAt(int tileX, int tileY)
    {
        var (cx, cy) = CellOf(tileX, tileY);
        return _cells.TryGetValue(TileKey.Pack(cx, cy), out var cell) ? cell.Max : 0;
    }

    /// <summary>Přičte (nebo odečte, u čističek) znečištění na místě budovy.</summary>
    public void Emit(int tileX, int tileY, PollutionKind kind, double amount)
    {
        if (amount == 0)
        {
            return;
        }

        var (cx, cy) = CellOf(tileX, tileY);
        long key = TileKey.Pack(cx, cy);
        _cells.TryGetValue(key, out var cell);

        switch (kind)
        {
            case PollutionKind.Air: cell.Air = Math.Max(0, cell.Air + amount); break;
            case PollutionKind.Water: cell.Water = Math.Max(0, cell.Water + amount); break;
            default: cell.Soil = Math.Max(0, cell.Soil + amount); break;
        }

        _cells[key] = cell;
    }

    /// <summary>
    /// Nechá znečištění rozlít do sousedních buněk a část odpařit.
    ///
    /// <para>Bez rozlévání by se čistička dala obejít tím, že se továrna postaví
    /// o kus dál; bez odparu by se svět nedal nikdy uzdravit a mechanika by byla
    /// jen trest.</para>
    /// </summary>
    public void Diffuse(double spreadRate, double decayRate)
    {
        if (_cells.Count == 0)
        {
            return;
        }

        _spread.Clear();
        _scratchKeys.Clear();
        _scratchKeys.AddRange(_cells.Keys);

        foreach (long key in _scratchKeys)
        {
            var cell = _cells[key];
            int cx = TileKey.X(key);
            int cy = TileKey.Y(key);

            // Čtvrtina rozlité dávky do každého ze čtyř sousedů.
            var share = new Cell
            {
                Air = cell.Air * spreadRate * 0.25,
                Water = cell.Water * spreadRate * 0.25,
                Soil = cell.Soil * spreadRate * 0.25,
            };

            AddSpread(cx + 1, cy, share);
            AddSpread(cx - 1, cy, share);
            AddSpread(cx, cy + 1, share);
            AddSpread(cx, cy - 1, share);

            double keep = (1 - spreadRate) * (1 - decayRate);
            _cells[key] = new Cell
            {
                Air = cell.Air * keep,
                Water = cell.Water * keep,
                Soil = cell.Soil * keep,
            };
        }

        foreach (var (key, share) in _spread)
        {
            _cells.TryGetValue(key, out var cell);
            _cells[key] = new Cell
            {
                Air = cell.Air + share.Air,
                Water = cell.Water + share.Water,
                Soil = cell.Soil + share.Soil,
            };
        }

        Forget();
    }

    /// <summary>Průměrné znečištění daného druhu přes všechny špinavé buňky.</summary>
    public double Average(PollutionKind kind)
    {
        if (_cells.Count == 0)
        {
            return 0;
        }

        double sum = 0;
        foreach (var cell in _cells.Values)
        {
            sum += cell.Get(kind);
        }

        return sum / _cells.Count;
    }

    /// <summary>Nejvyšší hodnota daného druhu kdekoli na mapě (pro HUD a varování).</summary>
    public double Peak(PollutionKind kind)
    {
        double peak = 0;
        foreach (var cell in _cells.Values)
        {
            peak = Math.Max(peak, cell.Get(kind));
        }

        return peak;
    }

    /// <summary>Záznamy k uložení do savu (jen špinavé buňky).</summary>
    public IEnumerable<(int CellX, int CellY, double Air, double Water, double Soil)> Entries()
    {
        foreach (var (key, cell) in _cells)
        {
            yield return (TileKey.X(key), TileKey.Y(key), cell.Air, cell.Water, cell.Soil);
        }
    }

    /// <summary>Obnoví buňku ze savu.</summary>
    public void RestoreCell(int cellX, int cellY, double air, double water, double soil) =>
        _cells[TileKey.Pack(cellX, cellY)] = new Cell { Air = air, Water = water, Soil = soil };

    /// <summary>Vyčistí celou mapu (Vzestup staví svět znovu).</summary>
    public void Clear()
    {
        _cells.Clear();
        _spread.Clear();
    }

    private void AddSpread(int cellX, int cellY, in Cell share)
    {
        long key = TileKey.Pack(cellX, cellY);
        _spread.TryGetValue(key, out var existing);
        _spread[key] = new Cell
        {
            Air = existing.Air + share.Air,
            Water = existing.Water + share.Water,
            Soil = existing.Soil + share.Soil,
        };
    }

    /// <summary>Zahodí buňky, které se odparem dostaly pod práh znatelnosti.</summary>
    private void Forget()
    {
        _scratchKeys.Clear();
        foreach (var (key, cell) in _cells)
        {
            if (cell.Max < ForgetBelow)
            {
                _scratchKeys.Add(key);
            }
        }

        foreach (long key in _scratchKeys)
        {
            _cells.Remove(key);
        }
    }

    private static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        return value % divisor != 0 && (value < 0) != (divisor < 0) ? quotient - 1 : quotient;
    }
}
