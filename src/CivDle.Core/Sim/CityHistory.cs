namespace CivDle.Core.Sim;

/// <summary>
/// Jeden snímek kroniky: čísla, kterými se dá okamžik popsat.
/// </summary>
/// <param name="Tick">Kdy byl snímek pořízen (tik simulace).</param>
/// <param name="Population">Kolik lidí tehdy ve městě žilo.</param>
/// <param name="Buildings">Kolik budov tehdy stálo.</param>
/// <param name="EraIndex">V jaké éře město bylo; −1 = éry nejsou v datech.</param>
public readonly record struct HistoryFrame(long Tick, long Population, int Buildings, int EraIndex)
{
    /// <summary>Kolik sekund herního času uplynulo od začátku běhu.</summary>
    public double Seconds => Tick / (double)Simulation.TicksPerSecond;
}

/// <summary>
/// Časosběr města: hrubý půdorys zástavby zaznamenaný co pár minut, ze kterého
/// se dá přehrát celý růst od první chalupy po metropoli.
///
/// <para>Proč to ve hře je: hráč staví hodiny a pak Vzestupem všechno smaže.
/// Zůstane mu číslo v bilanci — ale ne <b>obraz</b> toho, co postavil. Časosběr
/// je ta jediná věc, která z dlouhé tiché práce dělá příběh, na který jde ukázat
/// prstem: „tady jsem měl tři chalupy, tady už tohle".</para>
///
/// <para>Proč hrubá mřížka a ne skutečná mapa: snímek musí být tak malý, aby se
/// dal ukládat po stovkách. Jedna buňka je
/// <see cref="TilesPerCell"/>×<see cref="TilesPerCell"/> dlaždic a celý snímek
/// je bitová maska o <see cref="MaskBytes"/> bajtech — na tvar města to stačí
/// a na velikost savu se to nepozná.</para>
///
/// <para>Kruhový buffer: po naplnění se zahazuje <b>každý druhý starý snímek</b>,
/// ne ten nejstarší. Kdyby se zahazoval nejstarší, časosběr by časem začínal
/// uprostřed příběhu a začátek — ta nejzajímavější část — by zmizel.</para>
/// </summary>
public sealed class CityHistory
{
    /// <summary>Kolik dlaždic pokryje jedna buňka mřížky.</summary>
    public const int TilesPerCell = 8;

    /// <summary>Kolik buněk má mřížka na stranu (čtverec kolem počátku světa).</summary>
    public const int GridSize = 64;

    /// <summary>Kolik bajtů zabere maska jednoho snímku.</summary>
    public const int MaskBytes = GridSize * GridSize / 8;

    /// <summary>Kolik dlaždic na stranu časosběr celkem pokryje.</summary>
    public const int CoveredTiles = GridSize * TilesPerCell;

    private readonly List<HistoryFrame> _frames = new();
    private readonly List<byte[]> _masks = new();
    private readonly int _maxFrames;

    /// <param name="maxFrames">Kolik snímků se nejvýš drží (kruhový buffer).</param>
    public CityHistory(int maxFrames)
    {
        _maxFrames = Math.Max(2, maxFrames);
    }

    /// <summary>Kolik snímků kronika drží.</summary>
    public int Count => _frames.Count;

    /// <summary>Nejvyšší počet snímků, který se udrží.</summary>
    public int Capacity => _maxFrames;

    /// <summary>Čísla k danému snímku.</summary>
    public HistoryFrame FrameAt(int index) => _frames[index];

    /// <summary>Bitová maska zástavby k danému snímku (jen ke čtení).</summary>
    public ReadOnlySpan<byte> MaskAt(int index) => _masks[index];

    /// <summary>Stálo v téhle buňce mřížky v daném snímku něco?</summary>
    public bool IsOccupied(int index, int cellX, int cellY)
    {
        if (index < 0 || index >= _masks.Count || (uint)cellX >= GridSize || (uint)cellY >= GridSize)
        {
            return false;
        }

        int bit = cellY * GridSize + cellX;
        return (_masks[index][bit >> 3] & (1 << (bit & 7))) != 0;
    }

    /// <summary>Zahodí celou kroniku (Vzestup — nový svět začíná s prázdným listem).</summary>
    public void Clear()
    {
        _frames.Clear();
        _masks.Clear();
    }

    /// <summary>
    /// Přidá snímek. Maska musí mít přesně <see cref="MaskBytes"/> bajtů; kopíruje
    /// se, takže volající si smí svůj buffer dál přepisovat.
    /// </summary>
    public void Add(HistoryFrame frame, ReadOnlySpan<byte> mask)
    {
        if (mask.Length != MaskBytes)
        {
            throw new ArgumentException($"Maska snímku má mít {MaskBytes} bajtů, má {mask.Length}.", nameof(mask));
        }

        if (_frames.Count >= _maxFrames)
        {
            Thin();
        }

        _frames.Add(frame);
        _masks.Add(mask.ToArray());
    }

    /// <summary>
    /// Prořídí historii na polovinu — vyhodí každý druhý snímek a tím zdvojnásobí
    /// odstup mezi nimi. Časosběr tak pokryje celý běh, jen s hrubším krokem;
    /// začátek příběhu zůstane.
    /// </summary>
    private void Thin()
    {
        int write = 0;
        for (int read = 0; read < _frames.Count; read += 2)
        {
            _frames[write] = _frames[read];
            _masks[write] = _masks[read];
            write++;
        }

        _frames.RemoveRange(write, _frames.Count - write);
        _masks.RemoveRange(write, _masks.Count - write);
    }

    /// <summary>Převede dlaždici na buňku mřížky; false = leží mimo pokrytou plochu.</summary>
    public static bool TryCellOf(int tileX, int tileY, out int cellX, out int cellY)
    {
        cellX = FloorDiv(tileX, TilesPerCell) + GridSize / 2;
        cellY = FloorDiv(tileY, TilesPerCell) + GridSize / 2;
        return (uint)cellX < GridSize && (uint)cellY < GridSize;
    }

    private static int FloorDiv(int value, int divisor) =>
        value >= 0 ? value / divisor : (value - divisor + 1) / divisor;
}
