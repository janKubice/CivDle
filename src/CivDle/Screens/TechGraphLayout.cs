using CivDle.Core.Content;
using Microsoft.Xna.Framework;

namespace CivDle.Screens;

/// <summary>
/// Rozmístění technologií do grafu („síť"): sloupec = hloubka v závislostech
/// (kořeny vlevo, pokročilé vpravo), řádek = pořadí uvnitř sloupce. Čistý výpočet
/// bez vykreslování — počítá se jednou a pak se z něj jen čte (SRP: layout zvlášť,
/// kreslení zvlášť).
///
/// <para>Hloubka = nejdelší cesta od kořene, takže hrana vede vždy zleva doprava
/// a šipky se nekříží zpětně. Cyklus (chybná data) se ošetří strážcem — uzel se
/// v takovém případě položí do sloupce 0, místo aby výpočet zacyklil.</para>
/// </summary>
public sealed class TechGraphLayout
{
    /// <summary>Rozměr uzlu a rozestupy (v pixelech).</summary>
    public const int NodeWidth = 168;
    public const int NodeHeight = 58;
    private const int ColumnSpacing = 216;
    private const int RowSpacing = 72;
    private const int Margin = 24;

    private readonly Rectangle[] _bounds;

    public TechGraphLayout(DefRegistry<TechDef> techs)
    {
        int count = techs.Count;
        _bounds = new Rectangle[count];

        var depth = new int[count];
        var state = new byte[count]; // 0 = nespočítáno, 1 = počítá se, 2 = hotovo
        for (int i = 0; i < count; i++)
        {
            depth[i] = ComputeDepth(techs, i, depth, state);
        }

        // Uvnitř sloupce jdou uzly pod sebe v pořadí definice — stabilní a čitelné.
        var rowInColumn = new Dictionary<int, int>();
        for (int i = 0; i < count; i++)
        {
            rowInColumn.TryGetValue(depth[i], out int row);
            rowInColumn[depth[i]] = row + 1;

            _bounds[i] = new Rectangle(
                Margin + depth[i] * ColumnSpacing,
                Margin + row * RowSpacing,
                NodeWidth,
                NodeHeight);
        }

        int maxColumn = 0, maxRow = 0;
        foreach (var (column, rows) in rowInColumn)
        {
            maxColumn = Math.Max(maxColumn, column);
            maxRow = Math.Max(maxRow, rows);
        }

        Width = Margin * 2 + (maxColumn + 1) * ColumnSpacing;
        Height = Margin * 2 + maxRow * RowSpacing;
    }

    /// <summary>Celková šířka grafu (pro omezení posunu).</summary>
    public int Width { get; }

    /// <summary>Celková výška grafu (pro omezení posunu).</summary>
    public int Height { get; }

    /// <summary>Obdélník uzlu v souřadnicích grafu.</summary>
    public Rectangle Bounds(int techIndex) => _bounds[techIndex];

    private static int ComputeDepth(DefRegistry<TechDef> techs, int index, int[] depth, byte[] state)
    {
        if (state[index] == 2)
        {
            return depth[index];
        }

        if (state[index] == 1)
        {
            return 0; // strážce cyklu — chybná data nesmí zacyklit vykreslení
        }

        state[index] = 1;
        int best = 0;
        foreach (int prereq in techs[index].PrerequisiteIndices)
        {
            best = Math.Max(best, ComputeDepth(techs, prereq, depth, state) + 1);
        }

        depth[index] = best;
        state[index] = 2;
        return best;
    }
}
