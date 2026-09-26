namespace CivDle.Core.World;

/// <summary>
/// Dlaždice → celé číslo (0 = nic), uložené po blocích 32×32. Náhrada za
/// <c>Dictionary&lt;long, int&gt;</c> tam, kde se na dlaždice ptá hodně a
/// v sousedství — typicky „stojí tu budova?".
///
/// <para><b>Proč:</b> obsazenost dlaždic byla jeden slovník se stovkami tisíc
/// položek. Každý dotaz v něm jsou dva skoky náhodně do paměti (kbelík, pak
/// položka) — a hledání místa pro stavbu se ptá tisíckrát za stavbu. Ve
/// velkém městě s guvernérem na maximu to byla čtvrtina ceny kola. Tady
/// sousední dlaždice leží vedle sebe v jednom poli a poslední bloky drží
/// přímo mapovaná předsíň, takže dotaz v sousedství slovník vůbec nepotká.</para>
///
/// <para>Rozhraní kopíruje ten kus slovníku, který simulace používala
/// (<see cref="ContainsKey"/>, <see cref="TryGetValue"/>, indexer,
/// <see cref="Remove"/>, <see cref="Clear"/>), klíčem je <see cref="TileKey"/>.
/// Nula znamená „prázdno" — uložit nulu je totéž co smazat.</para>
///
/// <para>Bloky se po uvolnění nemažou (město se do nich obvykle vrátí);
/// pamětí to stojí 4 kB na blok, kde kdy něco stálo. Procházet obsah nejde
/// schválně: pořadí by záviselo na rozložení bloků a simulace musí zůstat
/// deterministická.</para>
/// </summary>
public sealed class TileMap
{
    private const int Shift = 5;
    private const int Mask = (1 << Shift) - 1;
    private const int ChunkArea = 1 << (Shift * 2);
    private const int RecentSide = 8;

    private readonly Dictionary<long, int[]> _chunks = new();
    private readonly long[] _recentKeys = new long[RecentSide * RecentSide];
    private readonly int[]?[] _recent = new int[]?[RecentSide * RecentSide];

    /// <summary>Kolik dlaždic má nenulovou hodnotu.</summary>
    public int Count { get; private set; }

    /// <summary>Hodnota na dlaždici; 0 = nic.</summary>
    public int this[long key]
    {
        get => Get(TileKey.X(key), TileKey.Y(key));
        set => Set(TileKey.X(key), TileKey.Y(key), value);
    }

    public bool ContainsKey(long key) => Get(TileKey.X(key), TileKey.Y(key)) != 0;

    public bool TryGetValue(long key, out int value)
    {
        value = Get(TileKey.X(key), TileKey.Y(key));
        return value != 0;
    }

    /// <summary>Smaže hodnotu; vrací, jestli tam nějaká byla.</summary>
    public bool Remove(long key)
    {
        int x = TileKey.X(key), y = TileKey.Y(key);
        int[]? chunk = FindChunk(x >> Shift, y >> Shift);
        int cell = Cell(x, y);
        if (chunk is null || chunk[cell] == 0)
        {
            return false;
        }

        chunk[cell] = 0;
        Count--;
        return true;
    }

    public void Clear()
    {
        _chunks.Clear();
        Array.Clear(_recent);
        Count = 0;
    }

    public int Get(int x, int y)
    {
        int[]? chunk = FindChunk(x >> Shift, y >> Shift);
        return chunk is null ? 0 : chunk[Cell(x, y)];
    }

    public void Set(int x, int y, int value)
    {
        if (value == 0)
        {
            Remove(TileKey.Pack(x, y));
            return;
        }

        int chunkX = x >> Shift, chunkY = y >> Shift;
        int[]? chunk = FindChunk(chunkX, chunkY);
        if (chunk is null)
        {
            chunk = new int[ChunkArea];
            _chunks[Key(chunkX, chunkY)] = chunk;
            Remember(chunkX, chunkY, chunk);
        }

        int cell = Cell(x, y);
        if (chunk[cell] == 0)
        {
            Count++;
        }

        chunk[cell] = value;
    }

    private int[]? FindChunk(int chunkX, int chunkY)
    {
        long key = Key(chunkX, chunkY);
        int slot = Slot(chunkX, chunkY);
        int[]? recent = _recent[slot];
        if (recent is not null && _recentKeys[slot] == key)
        {
            return recent;
        }

        if (!_chunks.TryGetValue(key, out var chunk))
        {
            return null; // prázdný blok se do předsíně nedává — zítra v něm může něco stát
        }

        Remember(chunkX, chunkY, chunk);
        return chunk;
    }

    private void Remember(int chunkX, int chunkY, int[] chunk)
    {
        int slot = Slot(chunkX, chunkY);
        _recent[slot] = chunk;
        _recentKeys[slot] = Key(chunkX, chunkY);
    }

    private static int Cell(int x, int y) => ((y & Mask) << Shift) | (x & Mask);

    private static int Slot(int chunkX, int chunkY) =>
        ((chunkY & (RecentSide - 1)) * RecentSide) + (chunkX & (RecentSide - 1));

    private static long Key(int chunkX, int chunkY) => ((long)chunkX << 32) | (uint)chunkY;
}
