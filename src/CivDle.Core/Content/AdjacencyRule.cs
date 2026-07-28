namespace CivDle.Core.Content;

/// <summary>
/// Pravidlo „na okolí záleží": budova vyrábí víc, když kolem sebe má správný terén.
/// Pila u lesa, lom u hor, rybolov u vody.
///
/// <para>Proč to v hře je: bez tohohle pravidla je umístění budovy čistě otázka
/// volného místa — hráč klikne kamkoli a nic se nezmění. S ním se z rozhledu po
/// mapě stává rozhodnutí, a to je jediné rozhodnutí, které idle hra při stavbě
/// nabízí (living-map.md §5 — biomy mají mít ekonomickou identitu).</para>
///
/// <para>Definice, ne instance: pravidlo je neměnný <c>record</c> z JSON, výsledný
/// násobič se u budovy spočítá jednou při položení a cachuje se
/// (<c>BuildingInstance.AdjacencyMult</c>) — v tikové smyčce se terén nevzorkuje.</para>
/// </summary>
/// <param name="Biomes">Maska biomů, které se počítají, indexovaná indexem biomu.</param>
/// <param name="Radius">Do jaké vzdálenosti od půdorysu se okolí prohlíží (v dlaždicích).</param>
/// <param name="BonusPerTile">Kolik přidá jedna vyhovující dlaždice (0.02 = +2 %).</param>
/// <param name="MaxBonus">Strop bonusu (0.4 = nejvýš +40 %) — jinak by ideální místo bylo bezkonkurenční.</param>
public sealed record AdjacencyRule(
    bool[] Biomes,
    int Radius,
    double BonusPerTile,
    double MaxBonus)
{
    /// <summary>Počítá se dlaždice tohoto biomu do bonusu?</summary>
    public bool Counts(int biomeIndex) => Biomes[biomeIndex];

    /// <summary>
    /// Násobič výroby pro daný počet vyhovujících dlaždic v okolí.
    /// 0 dlaždic = 1.0 (žádný trest — bonus je odměna za dobré místo, ne pokuta za špatné).
    /// </summary>
    public double Multiplier(int matchingTiles) =>
        1.0 + Math.Min(MaxBonus, Math.Max(0, matchingTiles) * BonusPerTile);

    /// <summary>Kolik dlaždic je potřeba na plný bonus (pro nápovědu v UI).</summary>
    public int TilesForFullBonus =>
        BonusPerTile <= 0 ? 0 : (int)Math.Ceiling(MaxBonus / BonusPerTile);
}
