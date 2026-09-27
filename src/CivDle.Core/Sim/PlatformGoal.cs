using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Paluba dřív než stavba (Nebesa, svety-design.md 4.5, 7.10): na světě bez
/// země se staví jen na plošině, a když na ní dochází volné místo, guvernér
/// ji rozšíří — jako silnici, po dlaždicích, od středu města ven.
///
/// <para><b>Proč vlastní cíl a ne budova:</b> paluba není budova, je to terén
/// (<see cref="PlatformDef"/>, terraformace z oblaků). Bez tohohle cíle by
/// guvernér hledal místo pro dům, nenašel ho a stál — paluba je předpoklad
/// všeho ostatního, proto je naléhavější než bydlení.</para>
///
/// <para>Posouzení počítá palubu jen v okolí města (čtverec do nejvzdálenější
/// budovy), jednou za kolo guvernéra — žádná hot path.</para>
/// </summary>
internal sealed class PlatformGoal : IGovernorGoal
{
    /// <summary>Nad bydlením: bez paluby není kam dům postavit.</summary>
    public const int Urgency = 60;

    /// <summary>Tolik volné paluby chce guvernér mít vždycky (pár domů a dílna).</summary>
    public const int MinFreeTiles = 24;

    /// <summary>A aspoň takový podíl paluby volný — velké město staví víc naráz.</summary>
    public const double MinFreeShare = 0.2;

    /// <summary>Dál od středu se palubou nepočítá (a nekreslí).</summary>
    public const int MaxRadius = 64;

    private readonly GameContent _content;

    public PlatformGoal(GameContent content) => _content = content;

    public CityNeed Need => CityNeed.Platform;

    public GoalAssessment Assess(Simulation sim)
    {
        if (_content.World.Platform is not { } platform || sim.Buildings.Length == 0)
        {
            return GoalAssessment.Idle(Need); // svět má pevnou zem
        }

        var (free, total) = CountDeck(sim, platform);
        return free < Math.Max(MinFreeTiles, total * MinFreeShare)
            ? new GoalAssessment(Need, Urgency, -1, -1)
            : GoalAssessment.Idle(Need);
    }

    /// <summary>Palubu klade <see cref="AutoBuildSystem"/> sám, žádná budova ji nesplní.</summary>
    public int Score(Simulation sim, int defIndex, in GoalAssessment goal) => 0;

    /// <summary>Kolik paluby je v okolí města a kolik z ní je volné (bez budovy a cesty).</summary>
    public static (int Free, int Total) CountDeck(Simulation sim, PlatformDef platform)
    {
        int radius = CityRadius(sim);
        int cx = sim.CityCenterX;
        int cy = sim.CityCenterY;
        int free = 0;
        int total = 0;
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (sim.BiomeAt(x, y) != platform.BiomeIndex)
                {
                    continue;
                }

                total++;
                if (!sim.TryGetBuildingAt(x, y, out _) && !sim.IsRoad(x, y))
                {
                    free++;
                }
            }
        }

        return (free, total);
    }

    /// <summary>Do jaké vzdálenosti od středu město sahá (+ okraj na novou palubu).</summary>
    public static int CityRadius(Simulation sim)
    {
        int cx = sim.CityCenterX;
        int cy = sim.CityCenterY;
        int radius = 0;
        foreach (var building in sim.Buildings)
        {
            radius = Math.Max(radius, Math.Max(Math.Abs(building.X - cx), Math.Abs(building.Y - cy)));
        }

        return Math.Min(MaxRadius, radius + 4);
    }

    /// <summary>
    /// Kam položit další kus paluby: dlaždice oblaků, která sousedí s palubou,
    /// co nejblíž středu města (prsten po prstenu). Deterministické pořadí —
    /// paluba roste jako kruh, ne jako výběžky.
    /// </summary>
    /// <returns>Kolik dlaždic zapsal do <paramref name="into"/>.</returns>
    public static int FindFrontier(Simulation sim, PlatformDef platform, TerraformDef action, Span<long> into)
    {
        int count = 0;
        int cx = sim.CityCenterX;
        int cy = sim.CityCenterY;
        int limit = CityRadius(sim) + 2;
        for (int r = 1; r <= limit && count < into.Length; r++)
        {
            for (int dy = -r; dy <= r && count < into.Length; dy++)
            {
                for (int dx = -r; dx <= r && count < into.Length; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                    {
                        continue; // jen obvod prstenu
                    }

                    int x = cx + dx;
                    int y = cy + dy;
                    if (action.AppliesTo(sim.BiomeAt(x, y)) && TouchesDeck(sim, platform, x, y))
                    {
                        into[count++] = World.TileKey.Pack(x, y);
                    }
                }
            }
        }

        return count;
    }

    private static bool TouchesDeck(Simulation sim, PlatformDef platform, int x, int y) =>
        sim.BiomeAt(x + 1, y) == platform.BiomeIndex || sim.BiomeAt(x - 1, y) == platform.BiomeIndex
        || sim.BiomeAt(x, y + 1) == platform.BiomeIndex || sim.BiomeAt(x, y - 1) == platform.BiomeIndex;
}
