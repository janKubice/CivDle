namespace CivDle.Core.World;

/// <summary>
/// Pohled na terén světa. Terén je čistá deterministická funkce (seed + preset),
/// takže se pro nekonečnou mapu nikdy neukládá — počítá se on-demand pro libovolnou
/// dlaždici (viz tech-stack.md: „nekonečná" mapa). Souřadnice mohou být záporné.
/// </summary>
public interface ITerrain
{
    /// <summary>Index biomu na dlaždici (odkaz do <c>BiomeRegistry</c>).</summary>
    byte BiomeAt(int x, int y);

    /// <summary>
    /// Kudy na téhle dlaždici teče řeka. Vrací false, když tu žádná není nebo
    /// nemá kam téct (jezero, konec toku).
    ///
    /// <para><b>Směr se nikam neukládá.</b> Původní plán počítal s dvěma bity
    /// na dlaždici, jenže mapa je nekonečná a terén je čistá funkce souřadnic —
    /// ukládat cokoli na dlaždici by znamenalo generovat svět dopředu. Směr je
    /// proto taky funkce: řeka teče z kopce, takže stačí porovnat výšku
    /// sousedů. Vyjde to nastejno a nestojí to ani bajt.</para>
    ///
    /// <para>Výchozí implementace říká „žádná řeka" — plochý testovací terén
    /// ani starší implementace se kvůli plavení dřeva nemusí měnit.</para>
    /// </summary>
    /// <param name="x">Dlaždice vodorovně.</param>
    /// <param name="y">Dlaždice svisle.</param>
    /// <param name="dx">Výstup: −1, 0 nebo 1.</param>
    /// <param name="dy">Výstup: −1, 0 nebo 1.</param>
    bool TryRiverFlow(int x, int y, out int dx, out int dy)
    {
        dx = 0;
        dy = 0;
        return false;
    }
}
