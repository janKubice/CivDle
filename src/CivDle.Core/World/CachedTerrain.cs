namespace CivDle.Core.World;

/// <summary>
/// Terén s pamětí biomů. Biom je čistá funkce souřadnic, jenže spočítat ho
/// znamená tři vrstvy šumu (výška, teplota, vlhkost) a řeku — a simulace se
/// na tutéž dlaždici ptá pořád znovu: hledání místa pro stavbu, těžba,
/// silnice, voda u přístavu.
///
/// <para><b>Proč:</b> ve velkém městě s guvernérem na maximu byl výpočet biomu
/// pětina ceny kola — a kolo se při přetočení času opakuje stokrát.</para>
///
/// <para><b>Jak:</b> po blocích 32×32 dlaždic, bajt na dlaždici (0 = ještě
/// nespočítáno, jinak biom + 1). Dotazy chodí v sousedství, takže před
/// slovníkem bloků stojí malá přímo mapovaná předsíň posledních bloků.
/// Bloků je nejvýš <see cref="MaxChunks"/>; když se zaplní, zapomene se
/// všechno. Nevadí to: je to jen paměť, ne stav — výsledky jsou pořád tytéž,
/// jen se znovu spočítají, a nekonečná mapa tak paměť nenafoukne.</para>
///
/// <para>Jen pro jedno vlákno. Drží ji simulace pro sebe; render a nástroje
/// dostávají původní terén (mají vlastní upečené chunky).</para>
/// </summary>
public sealed class CachedTerrain : ITerrain
{
    /// <summary>Kolik bloků se nejvýš pamatuje (4 MB).</summary>
    public const int MaxChunks = 4096;

    private const int Shift = 5;
    private const int Mask = (1 << Shift) - 1;
    private const int ChunkArea = 1 << (Shift * 2);

    /// <summary>Předsíň: 8×8 posledních bloků podle souřadnic bloku.</summary>
    private const int RecentSide = 8;

    private readonly ITerrain _inner;
    private readonly IReadOnlyDictionary<long, byte>? _overrides;
    private readonly Dictionary<long, byte[]> _chunks = new();
    private readonly long[] _recentKeys = new long[RecentSide * RecentSide];
    private readonly byte[]?[] _recent = new byte[]?[RecentSide * RecentSide];

    public CachedTerrain(ITerrain inner) => _inner = inner;

    /// <param name="inner">Terén, ze kterého se biomy počítají.</param>
    /// <param name="overrides">
    /// Přetvořené dlaždice (klíč <see cref="TileKey"/>): mají přednost před
    /// terénem. Bez nich by se na zúrodněné mělčině nedalo stavět — simulace
    /// by pořád viděla vodu, ačkoli hráč viděl louku.
    /// </param>
    public CachedTerrain(ITerrain inner, IReadOnlyDictionary<long, byte> overrides)
    {
        _inner = inner;
        _overrides = overrides;
    }

    /// <summary>
    /// Dlaždice se právě přetvořila — přepíše zapamatovaný biom. Stačí jen
    /// tady: kdyby se paměť mezitím vyprázdnila, přepis se při dalším dotazu
    /// vezme ze slovníku přepisů.
    /// </summary>
    public void Override(int x, int y, byte biome)
    {
        byte[] chunk = ChunkAt(x >> Shift, y >> Shift);
        chunk[((y & Mask) << Shift) | (x & Mask)] = biome == byte.MaxValue ? (byte)0 : (byte)(biome + 1);
    }

    /// <summary>Kolik bloků je teď v paměti (pro testy a ladění).</summary>
    public int ChunkCount => _chunks.Count;

    public byte BiomeAt(int x, int y)
    {
        byte[] chunk = ChunkAt(x >> Shift, y >> Shift);
        int cell = ((y & Mask) << Shift) | (x & Mask);
        byte stored = chunk[cell];
        if (stored != 0)
        {
            return (byte)(stored - 1);
        }

        byte biome = _overrides is not null && _overrides.TryGetValue(TileKey.Pack(x, y), out byte overridden)
            ? overridden
            : _inner.BiomeAt(x, y);
        if (biome != byte.MaxValue)
        {
            chunk[cell] = (byte)(biome + 1); // biom 255 se nevejde — ten se jen nepamatuje
        }

        return biome;
    }

    public float ElevationAt(int x, int y) => _inner.ElevationAt(x, y);

    public bool TryRiverFlow(int x, int y, out int dx, out int dy) => _inner.TryRiverFlow(x, y, out dx, out dy);

    private byte[] ChunkAt(int chunkX, int chunkY)
    {
        long key = ((long)chunkX << 32) | (uint)chunkY;
        int slot = ((chunkY & (RecentSide - 1)) * RecentSide) + (chunkX & (RecentSide - 1));
        byte[]? recent = _recent[slot];
        if (recent is not null && _recentKeys[slot] == key)
        {
            return recent;
        }

        if (!_chunks.TryGetValue(key, out var chunk))
        {
            if (_chunks.Count >= MaxChunks)
            {
                _chunks.Clear();
                Array.Clear(_recent);
            }

            chunk = new byte[ChunkArea];
            _chunks[key] = chunk;
        }

        _recent[slot] = chunk;
        _recentKeys[slot] = key;
        return chunk;
    }
}
