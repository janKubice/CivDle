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
/// <param name="CritChanceBonus">Přičítá se k šanci na krit při ručním sběru (0.05 = +5 p. b.).</param>
/// <param name="JackpotChance">Šance na „úlovek života" — vzácný obří výnos (0 = nikdy).</param>
/// <param name="DiscoveryLuck">Násobič hustoty nálezů na mapě (1.0 = beze změny).</param>
/// <param name="FestivalPower">Násobič síly slavnosti nad rámec základního boostu.</param>
/// <param name="ResearchDiscount">Podíl, o který zlevní výzkum (0.25 = −25 %).</param>
/// <param name="AutoBuildSpeed">
/// Násobič tempa automatické výstavby. Je to jeden z mála bonusů, který hráč
/// <b>vidí</b> na mapě: město se po Vzestupu rozrůstá viditelně rychleji.
/// </param>
public readonly record struct PrestigeBonuses(
    double ProductionMult,
    double HarvestMult,
    double GrowthMult,
    double HousingMult,
    double StorageMult,
    double StartResourceMult,
    double OfflineMult,
    double CritChanceBonus = 0.0,
    double JackpotChance = 0.0,
    double DiscoveryLuck = 1.0,
    double FestivalPower = 1.0,
    double ResearchDiscount = 0.0,
    double AutoBuildSpeed = 1.0)
{
    /// <summary>Neutrální bonusy (vše 1.0 / 0) — žádné upgrady.</summary>
    public static PrestigeBonuses None { get; } = new(1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0);
}
