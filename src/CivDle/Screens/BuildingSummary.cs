using System.Text;
using CivDle.Core.Content;
using CivDle.Core.Sim;

namespace CivDle.Screens;

/// <summary>
/// Složí z definice budovy čitelný popisek („co ta budova dělá") pro tooltip
/// u kurzoru. Skládá se z DAT (cena, recept, bydlení, sklad, elektřina, biomy),
/// ne z ručně psaného textu — nová budova v JSON tak má popisek hned a nemusí
/// se k ní dopisovat lokalizační klíč.
///
/// <para>Patří do UI vrstvy: jen formátuje to, co simulace a obsah už vědí.</para>
/// </summary>
internal static class BuildingSummary
{
    /// <summary>Víceřádkový popis budovy pro bublinu u kurzoru.</summary>
    public static string Describe(GameContent content, Localization loc, BuildingDef def)
    {
        var text = new StringBuilder();
        text.Append(loc.Format("tip.build.cost", CostFormat.Line(content, loc, def.BuildCost)));

        if (def.Recipe is { } recipe)
        {
            double perSecond = Simulation.TicksPerSecond / recipe.TimeTicks;
            if (recipe.Inputs.Count > 0)
            {
                text.Append('\n').Append(loc.Format("tip.build.consumes", CostFormat.Line(content, loc, recipe.Inputs)));
            }

            if (recipe.Outputs.Count > 0)
            {
                text.Append('\n').Append(loc.Format("tip.build.produces",
                    CostFormat.Line(content, loc, recipe.Outputs), Rate(perSecond)));
            }
        }

        if (def.HousingCapacity > 0)
        {
            text.Append('\n').Append(loc.Format("tip.build.housing", def.HousingCapacity));
        }

        if (def.WorkerSlots > 0)
        {
            text.Append('\n').Append(loc.Format("tip.build.workers", def.WorkerSlots));
        }

        if (def.Services > 0)
        {
            text.Append('\n').Append(loc.Format("tip.build.services",
                (int)Math.Round(def.Services * content.Gameplay.Happiness.PeoplePerServicePoint)));
        }

        if (def.Upkeep.Count > 0)
        {
            text.Append('\n').Append(loc.Format("tip.build.upkeep", CostFormat.Line(content, loc, def.Upkeep)));
        }

        if (def.StorageBonus.Count > 0)
        {
            text.Append('\n').Append(loc.Format("tip.build.storage", CostFormat.Line(content, loc, def.StorageBonus)));
        }

        if (def.PowerSupply > 0)
        {
            text.Append('\n').Append(loc.Format("tip.build.power", def.PowerSupply));
        }

        if (def.NeedsPower)
        {
            text.Append('\n').Append(loc.Format("tip.build.needsPower", def.PowerDemand));
        }

        if (def.NeedsWaterAccess)
        {
            text.Append('\n').Append(loc["tip.build.needsWater"]);
        }

        // Svoz se týká každé výrobny, takže se zmiňuje jen jednou obecně —
        // konkrétní číslo pro místo pod kurzorem ukazuje stavební režim.
        if (def.Recipe is not null && content.Gameplay.Haul.IsEnabled)
        {
            text.Append('\n').Append(loc["tip.build.haul"]);
        }

        if (def.Adjacency is { } adjacency)
        {
            text.Append('\n').Append(loc.Format("tip.build.adjacency",
                AdjacencyBiomes(content, loc, adjacency),
                Percent(adjacency.MaxBonus)));
        }

        string biomes = AllowedBiomes(content, loc, def);
        if (biomes.Length > 0)
        {
            text.Append('\n').Append(loc.Format("tip.build.biomes", biomes));
        }

        return text.ToString();
    }

    /// <summary>
    /// Seznam biomů, kde budova smí stát — vypíše se, jen když je omezená.
    /// Když může všude, mlčení je informativnější než dlouhý výčet.
    /// </summary>
    private static string AllowedBiomes(GameContent content, Localization loc, BuildingDef def)
    {
        var allowed = new List<string>();
        for (int i = 0; i < content.Biomes.Count; i++)
        {
            if (def.IsBiomeAllowed(i))
            {
                allowed.Add(loc[content.Biomes[i].NameKey]);
            }
        }

        return allowed.Count == content.Biomes.Count ? string.Empty : string.Join(", ", allowed);
    }

    /// <summary>Biomy, které se počítají do bonusu za okolí.</summary>
    private static string AdjacencyBiomes(GameContent content, Localization loc, AdjacencyRule rule)
    {
        var counted = new List<string>();
        for (int i = 0; i < content.Biomes.Count; i++)
        {
            if (rule.Counts(i))
            {
                counted.Add(loc[content.Biomes[i].NameKey]);
            }
        }

        return string.Join(", ", counted);
    }

    /// <summary>Bonus jako procenta („+35 %") — v procentech je čitelnější než jako násobič.</summary>
    public static string Percent(double bonus) => $"{Math.Round(bonus * 100)}";

    private static string Rate(double perSecond) =>
        perSecond >= 1 ? perSecond.ToString("0.#") : perSecond.ToString("0.##");
}
