namespace CivDle.Core.Sim;

/// <summary>
/// Mlha války: co hráč ještě neviděl, je tma.
///
/// <para>Proč to ve hře je: mapa je nekonečná a bez mlhy je celá vidět od první
/// vteřiny. Objevování pak nemá odměnu — hráč ví, kde je moře a kde hory, aniž
/// by tam kdy poslal jedinou budovu. Mlha vrací důvod expandovat: za tmou může
/// být cokoli, a jediný způsob, jak to zjistit, je jít tam.</para>
///
/// <para>Ukládá se po <b>čtvercích</b> (viz <see cref="ChunkTiles"/>), ne po
/// dlaždicích. Nekonečná mapa nemá kolik dlaždic uložit, ale čtverců je i po
/// hodinách hraní řádově tisíce — a hrubší zrno je tu zadarmo, protože mlha se
/// stejně kreslí jako měkký přechod, ne jako mřížka.</para>
///
/// <para>Vrstva: čistá simulace, nezná render. Renderer se jen ptá
/// <see cref="IsExplored"/>.</para>
/// </summary>
public sealed class FogOfWar
{
    /// <summary>Hrana odhaleného čtverce v dlaždicích.</summary>
    public const int ChunkTiles = 8;

    private readonly HashSet<long> _explored = new();

    /// <summary>Kolik čtverců už hráč viděl (statistika i test).</summary>
    public int ExploredChunks => _explored.Count;

    /// <summary>Je čtverec s touhle dlaždicí odhalený?</summary>
    public bool IsExplored(int tileX, int tileY) => _explored.Contains(Key(tileX, tileY));

    /// <summary>
    /// Odhalí okolí dlaždice do daného poloměru (v dlaždicích).
    ///
    /// <para>Vrací true, když se opravdu něco nového ukázalo — volající z toho
    /// pozná, že stojí za to hráči ohlásit „objevil jsi kus světa", a zároveň se
    /// tím dá levně přeskočit práce, když se nic nezměnilo.</para>
    /// </summary>
    public bool Reveal(int tileX, int tileY, int radiusTiles)
    {
        bool changed = false;
        int radius = Math.Max(0, radiusTiles);
        for (int y = tileY - radius; y <= tileY + radius; y += ChunkTiles)
        {
            for (int x = tileX - radius; x <= tileX + radius; x += ChunkTiles)
            {
                changed |= _explored.Add(Key(x, y));
            }
        }

        // Okraje: krok po ChunkTiles může poslední pruh minout, takže se rohy
        // dorovnají zvlášť. Bez toho by u velkých poloměrů zůstával tmavý lem.
        changed |= _explored.Add(Key(tileX + radius, tileY + radius));
        changed |= _explored.Add(Key(tileX - radius, tileY + radius));
        changed |= _explored.Add(Key(tileX + radius, tileY - radius));
        changed |= _explored.Add(Key(tileX - radius, tileY - radius));
        return changed;
    }

    /// <summary>Zapomene všechno — nový svět po Vzestupu se objevuje znovu.</summary>
    public void Clear() => _explored.Clear();

    /// <summary>Klíče odhalených čtverců pro uložení.</summary>
    public IReadOnlyCollection<long> ExploredKeys => _explored;

    /// <summary>Obnoví odhalené čtverce ze savu.</summary>
    public void Restore(IEnumerable<long> keys)
    {
        _explored.Clear();
        foreach (long key in keys)
        {
            _explored.Add(key);
        }
    }

    /// <summary>Klíč čtverce, do kterého dlaždice spadá.</summary>
    private static long Key(int tileX, int tileY)
    {
        int chunkX = FloorDiv(tileX, ChunkTiles);
        int chunkY = FloorDiv(tileY, ChunkTiles);
        return ((long)(uint)chunkX << 32) | (uint)chunkY;
    }

    /// <summary>Dělení dolů i pro záporné souřadnice (mapa jde na obě strany).</summary>
    private static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        return value % divisor != 0 && (value < 0) != (divisor < 0) ? quotient - 1 : quotient;
    }
}
