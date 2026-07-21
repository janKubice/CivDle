namespace CivDle.Core.Sim;

/// <summary>
/// Agregované trvalé násobiče z koupených upgradů Vzestupu. Systémy simulace je
/// jen čtou (výroba, sběr, růst, bydlení, sklady, startovní suroviny). Základ je
/// vždy 1.0 (bez bonusu). Neměnná — přepočítá se při koupi upgradu / Vzestupu.
/// </summary>
/// <param name="ProductionMult">Násobič výstupu výroby.</param>
/// <param name="HarvestMult">Násobič ručního sběru klikáním.</param>
/// <param name="GrowthMult">Násobič rychlosti růstu populace.</param>
/// <param name="HousingMult">Násobič bydlení přidaného budovami.</param>
/// <param name="StorageMult">Násobič kapacity skladů (základ i budovy).</param>
/// <param name="StartResourceMult">Násobič startovních surovin nové éry.</param>
/// <param name="OfflineMult">Násobič efektivity offline postupu (co běží, když nehraješ).</param>
public readonly record struct PrestigeBonuses(
    double ProductionMult,
    double HarvestMult,
    double GrowthMult,
    double HousingMult,
    double StorageMult,
    double StartResourceMult,
    double OfflineMult)
{
    /// <summary>Neutrální bonusy (vše 1.0) — žádné upgrady.</summary>
    public static PrestigeBonuses None { get; } = new(1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0);
}
