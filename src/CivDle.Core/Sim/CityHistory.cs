namespace CivDle.Core.Sim;

/// <summary>
/// Jeden snímek kroniky: čísla, kterými se dá okamžik popsat.
/// </summary>
/// <param name="Tick">Kdy byl snímek pořízen (tik simulace).</param>
/// <param name="Population">Kolik lidí tehdy ve městě žilo.</param>
/// <param name="Buildings">Kolik budov tehdy stálo.</param>
/// <param name="EraIndex">V jaké éře město bylo; −1 = éry nejsou v datech.</param>
/// <param name="Happiness">Jak se ve městě žilo (0–1).</param>
/// <param name="HousingCapacity">Kolik lidí se tehdy mělo kam vejít.</param>
/// <param name="Pollution">Kolik špíny viselo nad městem.</param>
/// <param name="Settlements">Kolik sídel civilizace měla.</param>
public readonly record struct HistoryFrame(
    long Tick,
    long Population,
    int Buildings,
    int EraIndex,
    double Happiness = 1.0,
    long HousingCapacity = 0,
    double Pollution = 0,
    int Settlements = 0)
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
/// <see cref="TilesPerCell"/>×<see cref="TilesPerCell"/> dlaždic a nese jeden
/// bajt — odkaz do palety barev (0 = prázdno). Přehrávka tak může kreslit
/// buňky v barvách skutečných budov nad skutečným terénem (terén se
/// rekonstruuje ze seedu, ten se ukládat nemusí), a snímek pořád váží pár
/// kilobajtů, které gzip savu složí na zlomek.</para>

/// <para>Paleta patří kronice, ne obsahu hry: buňky odkazují do ní, takže
/// uložený časosběr zůstane barevně věrný, i když se herní data mezitím
/// změní nebo hráč odinstaluje mod.</para>
///
/// <para>Kruhový buffer: po naplnění se zahazuje <b>každý druhý starý snímek</b>,
/// ne ten nejstarší. Kdyby se zahazoval nejstarší, časosběr by časem začínal
/// uprostřed příběhu a začátek — ta nejzajímavější část — by zmizel.</para>
/// </summary>
public sealed class CityHistory
{
    /// <summary>Kolik dlaždic pokryje jedna buňka mřížky.</summary>
    public const int TilesPerCell = 2;

    /// <summary>Kolik buněk má mřížka na stranu (čtverec kolem počátku světa).</summary>
    public const int GridSize = 256;

    /// <summary>Kolik bajtů zabere mřížka jednoho snímku (bajt na buňku).</summary>
    public const int CellBytes = GridSize * GridSize;

    /// <summary>Nejvyšší počet barev palety (0 v buňce znamená prázdno).</summary>
    public const int MaxPaletteColors = 255;

    /// <summary>Kolik dlaždic na stranu časosběr celkem pokryje.</summary>
    public const int CoveredTiles = GridSize * TilesPerCell;

    private readonly List<HistoryFrame> _frames = new();
    private readonly List<byte[]> _cells = new();
    private readonly List<Content.RgbColor> _palette = new();
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

    /// <summary>Mřížka buněk k danému snímku (bajt na buňku, jen ke čtení).</summary>
    public ReadOnlySpan<byte> CellsAt(int index) => _cells[index];

    /// <summary>Barvy, na které buňky odkazují (hodnota buňky − 1 = index sem).</summary>
    public IReadOnlyList<Content.RgbColor> Palette => _palette;

    /// <summary>Stálo v téhle buňce mřížky v daném snímku něco?</summary>
    public bool IsOccupied(int index, int cellX, int cellY) => CellAt(index, cellX, cellY) != 0;

    /// <summary>Hodnota buňky: 0 = prázdno, jinak index do palety + 1.</summary>
    public byte CellAt(int index, int cellX, int cellY)
    {
        if (index < 0 || index >= _cells.Count || (uint)cellX >= GridSize || (uint)cellY >= GridSize)
        {
            return 0;
        }

        return _cells[index][cellY * GridSize + cellX];
    }

    /// <summary>Barva buňky; prázdná buňka a poškozený odkaz vrací null.</summary>
    public Content.RgbColor? ColorAt(int index, int cellX, int cellY)
    {
        byte cell = CellAt(index, cellX, cellY);
        return cell != 0 && cell <= _palette.Count ? _palette[cell - 1] : null;
    }

    /// <summary>
    /// Vrátí index barvy v paletě (buňka pak nese index + 1); barvu přidá,
    /// když ještě není. Po naplnění palety vrací poslední barvu — kreslit
    /// trochu špatnou barvou je lepší než nekreslit vůbec.
    /// </summary>
    public int PaletteIndexOf(Content.RgbColor color)
    {
        for (int i = 0; i < _palette.Count; i++)
        {
            if (_palette[i] == color)
            {
                return i;
            }
        }

        if (_palette.Count >= MaxPaletteColors)
        {
            return _palette.Count - 1;
        }

        _palette.Add(color);
        return _palette.Count - 1;
    }

    /// <summary>Obnoví paletu ze souboru (save/export). Jen pro čtecí vrstvu.</summary>
    public void RestorePalette(IEnumerable<Content.RgbColor> colors)
    {
        _palette.Clear();
        foreach (var color in colors.Take(MaxPaletteColors))
        {
            _palette.Add(color);
        }
    }

    /// <summary>Zahodí celou kroniku (Vzestup — nový svět začíná s prázdným listem).</summary>
    public void Clear()
    {
        _frames.Clear();
        _cells.Clear();
        _palette.Clear();
    }

    /// <summary>
    /// Přidá snímek. Mřížka musí mít přesně <see cref="CellBytes"/> bajtů; kopíruje
    /// se, takže volající si smí svůj buffer dál přepisovat.
    /// </summary>
    public void Add(HistoryFrame frame, ReadOnlySpan<byte> cells)
    {
        if (cells.Length != CellBytes)
        {
            throw new ArgumentException($"Mřížka snímku má mít {CellBytes} bajtů, má {cells.Length}.", nameof(cells));
        }

        if (_frames.Count >= _maxFrames)
        {
            Thin();
        }

        _frames.Add(frame);
        _cells.Add(cells.ToArray());
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
            _cells[write] = _cells[read];
            write++;
        }

        _frames.RemoveRange(write, _frames.Count - write);
        _cells.RemoveRange(write, _cells.Count - write);
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
