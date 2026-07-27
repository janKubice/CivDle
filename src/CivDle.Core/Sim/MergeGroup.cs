namespace CivDle.Core.Sim;

/// <summary>
/// Blok 2×2 stejných jednodlaždicových budov, který jde sloučit v jednu velkou.
/// Nese levý horní roh a indexy všech čtyř budov, aby se nemusely hledat znovu
/// při samotném sloučení.
/// </summary>
/// <param name="X">Levý horní roh bloku v dlaždicích.</param>
/// <param name="Y">Levý horní roh bloku v dlaždicích.</param>
/// <param name="DefIndex">Definice budov, které blok tvoří.</param>
/// <param name="A">Index budovy vlevo nahoře.</param>
/// <param name="B">Index budovy vpravo nahoře.</param>
/// <param name="C">Index budovy vlevo dole.</param>
/// <param name="D">Index budovy vpravo dole.</param>
public readonly record struct MergeGroup(int X, int Y, int DefIndex, int A, int B, int C, int D)
{
    /// <summary>Indexy čtyř budov bloku sestupně — mazat se musí od nejvyššího.</summary>
    /// <remarks>
    /// Odebrání budovy přesouvá poslední prvek plochého pole na uvolněné místo,
    /// takže mazání od nejvyššího indexu zaručí, že se zbylé tři neposunou pod
    /// rukama. Řadí se jen čtyři čísla, proto ručně a bez alokace.
    /// </remarks>
    public (int, int, int, int) DescendingIndices()
    {
        Span<int> values = stackalloc int[4] { A, B, C, D };
        for (int i = 1; i < 4; i++)
        {
            for (int j = i; j > 0 && values[j] > values[j - 1]; j--)
            {
                (values[j], values[j - 1]) = (values[j - 1], values[j]);
            }
        }

        return (values[0], values[1], values[2], values[3]);
    }
}
