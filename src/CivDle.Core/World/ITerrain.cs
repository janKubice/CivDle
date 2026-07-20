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
}
