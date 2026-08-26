using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>
/// Kde která budova stojí — aby se renderer nemusel ptát celého města.
///
/// <para><b>Proč to vzniklo:</b> vykreslování projelo pole budov od nuly do
/// konce každý snímek a u každé se ptalo, jestli je vidět. Při deseti tisících
/// budovách je to deset tisíc dotazů na to, aby se nakreslilo dvě stě — a při
/// milionu obyvatel je to jediná věc, kterou je znát.</para>
///
/// <para><b>Jak:</b> budovy jsou nasypané do chunků 32×32 dlaždic. Dotaz na
/// výřez projde jen chunky, které do něj zasahují. Chunk je tak velký, aby jich
/// při běžném přiblížení bylo v záběru pár desítek, a tak malý, aby v jednom
/// nebylo půl města.</para>
///
/// <para><b>Vrstva:</b> index drží simulace, protože jediná ví o každé změně
/// zástavby. Render z něj jen čte. Kdyby si ho stavěl renderer sám, musel by
/// hlídat, kdy se zástavba změnila — a to je přesně ta věc, na kterou se
/// zapomene.</para>
/// </summary>
public sealed class BuildingIndex
{
    /// <summary>Hrana chunku v dlaždicích. Mocnina dvojky — posun je levnější než dělení.</summary>
    public const int ChunkShift = 5;

    /// <summary>Hrana chunku v dlaždicích (32).</summary>
    public const int ChunkSize = 1 << ChunkShift;

    private readonly Dictionary<long, List<int>> _chunks = new();

    /// <summary>Kolik chunků má nějakou budovu. Pro testy a diagnostiku.</summary>
    public int ChunkCount => _chunks.Count;

    /// <summary>Zapíše budovu do všech chunků, do kterých zasahuje její půdorys.</summary>
    public void Add(int buildingIndex, int x, int y, int width, int height)
    {
        foreach (long chunk in ChunksOf(x, y, width, height))
        {
            if (!_chunks.TryGetValue(chunk, out var list))
            {
                list = new List<int>();
                _chunks[chunk] = list;
            }

            if (!list.Contains(buildingIndex))
            {
                list.Add(buildingIndex);
            }
        }
    }

    /// <summary>Vyškrtne budovu z chunků, do kterých zasahovala.</summary>
    public void Remove(int buildingIndex, int x, int y, int width, int height)
    {
        foreach (long chunk in ChunksOf(x, y, width, height))
        {
            if (_chunks.TryGetValue(chunk, out var list))
            {
                list.Remove(buildingIndex);
            }
        }
    }

    /// <summary>
    /// Přečísluje budovu: index <paramref name="from"/> se nově jmenuje
    /// <paramref name="to"/>.
    ///
    /// <para>Existuje kvůli tomu, jak simulace maže z plochého pole — poslední
    /// budova se přesune na uvolněné místo. Bez přečíslování by index ukazoval
    /// na budovu, která tam už není, a renderer by kreslil cizí dům.</para>
    /// </summary>
    public void Rename(int from, int to, int x, int y, int width, int height)
    {
        foreach (long chunk in ChunksOf(x, y, width, height))
        {
            if (!_chunks.TryGetValue(chunk, out var list))
            {
                continue;
            }

            int at = list.IndexOf(from);
            if (at >= 0)
            {
                list[at] = to;
            }
        }
    }

    /// <summary>Zapomene všechno (nový svět, Vzestup).</summary>
    public void Clear() => _chunks.Clear();

    /// <summary>
    /// Nasype do <paramref name="results"/> indexy budov, jejichž chunk zasahuje
    /// do obdélníku dlaždic.
    ///
    /// <para>Buffer je <b>předaný</b>, ne vrácený: dotaz se volá jednou za
    /// snímek a nová kolekce pokaždé by byla přesně ta alokace za snímek,
    /// kterou CLAUDE.md zakazuje.</para>
    ///
    /// <para>Vrací i budovy, které do výřezu nezasahují — jen leží v témž
    /// chunku. Přesné ořezání dělá volající, který jediný ví, jak velký sprite
    /// kreslí.</para>
    /// </summary>
    public void Query(int minX, int minY, int maxX, int maxY, List<int> results)
    {
        results.Clear();

        int fromChunkX = minX >> ChunkShift;
        int toChunkX = maxX >> ChunkShift;
        int fromChunkY = minY >> ChunkShift;
        int toChunkY = maxY >> ChunkShift;

        for (int chunkY = fromChunkY; chunkY <= toChunkY; chunkY++)
        {
            for (int chunkX = fromChunkX; chunkX <= toChunkX; chunkX++)
            {
                if (_chunks.TryGetValue(TileKey.Pack(chunkX, chunkY), out var list))
                {
                    results.AddRange(list);
                }
            }
        }
    }

    /// <summary>Chunky, do kterých zasahuje půdorys. Velká budova jich má víc.</summary>
    private static IEnumerable<long> ChunksOf(int x, int y, int width, int height)
    {
        int fromChunkX = x >> ChunkShift;
        int toChunkX = (x + Math.Max(1, width) - 1) >> ChunkShift;
        int fromChunkY = y >> ChunkShift;
        int toChunkY = (y + Math.Max(1, height) - 1) >> ChunkShift;

        for (int chunkY = fromChunkY; chunkY <= toChunkY; chunkY++)
        {
            for (int chunkX = fromChunkX; chunkX <= toChunkX; chunkX++)
            {
                yield return TileKey.Pack(chunkX, chunkY);
            }
        }
    }
}
