namespace CivDle.Core.Sim;

/// <summary>
/// Obdélníková zóna namalovaná hráčem (automatizace, stupeň 3 dle living-city.md).
/// Je to jen designace záměru — uvnitř svých hranic ji <see cref="ZoneFillSystem"/>
/// postupně zaplňuje budovami svého typu. Levý horní roh je (X, Y), rozměry
/// Width×Height v dlaždicích. Neměnná hodnota (řídká data, ne hot path).
/// </summary>
/// <param name="TypeIndex">Index typu zóny (viz <see cref="Content.ZoneTypeDef"/>).</param>
/// <param name="X">Levý okraj (dlaždice).</param>
/// <param name="Y">Horní okraj (dlaždice).</param>
/// <param name="Width">Šířka v dlaždicích (≥ 1).</param>
/// <param name="Height">Výška v dlaždicích (≥ 1).</param>
public readonly record struct Zone(int TypeIndex, int X, int Y, int Width, int Height)
{
    /// <summary>Leží dlaždice (tileX, tileY) uvnitř zóny?</summary>
    public bool Contains(int tileX, int tileY) =>
        tileX >= X && tileX < X + Width && tileY >= Y && tileY < Y + Height;
}
