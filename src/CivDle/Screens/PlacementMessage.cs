using CivDle.Core.Content;
using CivDle.Core.Sim;

namespace CivDle.Screens;

/// <summary>
/// Proč se budova sem postavit nedá — a hlavně <b>kam se tedy postavit dá</b>.
///
/// <para>„Špatný terén" byla slepá ulička: hráč se dozvěděl, že tady ne, ale ne
/// kde ano. Zkoušel tedy dlaždici po dlaždici, dokud budovu nevzdal — a to
/// vypadá úplně stejně jako rozbitá hra. U dvou nejčastějších odmítnutí se
/// proto rovnou doplní, co je potřeba: výčet biomů, nebo stupeň sídla, na který
/// se musí dorůst.</para>
///
/// <para>Text se skládá z DAT (biomy budovy, žebříček sídel), ne z ručně psané
/// věty pro každou budovu — nová budova v JSON má hlášku hned.</para>
///
/// <para>Vrstva: UI. Jen formátuje to, co simulace už rozhodla.</para>
/// </summary>
internal static class PlacementMessage
{
    /// <summary>Hláška pro odmítnuté místo pod kurzorem.</summary>
    public static string Describe(GameContent content, Localization loc, BuildingDef def, PlacementResult result)
    {
        switch (result)
        {
            case PlacementResult.WrongBiome:
            {
                string biomes = BuildingSummary.AllowedBiomes(content, loc, def);
                return biomes.Length > 0
                    ? loc.Format("build.error.wrongBiomeWhere", biomes)
                    : loc["build.error.wrongBiome"];
            }

            case PlacementResult.SettlementTooSmall:
            {
                var ranks = content.SettlementRanks.Ranks;
                return def.MinSettlementRank >= 0 && def.MinSettlementRank < ranks.Count
                    ? loc.Format("build.error.settlementNeeds", loc[ranks[def.MinSettlementRank].NameKey])
                    : loc["build.error.settlementTooSmall"];
            }

            default:
                return loc[Key(result)];
        }
    }

    /// <summary>Lokalizační klíč pro odmítnutí, které se dá vysvětlit jednou větou.</summary>
    private static string Key(PlacementResult result) => result switch
    {
        PlacementResult.Occupied => "build.error.occupied",
        PlacementResult.WrongBiome => "build.error.wrongBiome",
        PlacementResult.NotEnoughResources => "build.error.resources",
        PlacementResult.NeedsWaterAccess => "build.error.waterAccess",
        PlacementResult.NoSubseaLink => "build.error.subsea",
        PlacementResult.NeedsDefenceMode => "build.error.frontierOff",
        PlacementResult.SettlementTooSmall => "build.error.settlementTooSmall",
        PlacementResult.OutOfBounds => "build.error.outOfBounds",
        _ => "build.title",
    };
}
