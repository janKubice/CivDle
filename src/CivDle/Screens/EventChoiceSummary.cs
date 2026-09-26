using CivDle.Core.Content;
using CivDle.Core.Sim;

namespace CivDle.Screens;

/// <summary>
/// Co volba v události udělá, jedním řádkem: „−15 Jídlo  +25 Kámen  Jídlo −25 % na 3 min".
///
/// <para>Dřív tlačítko ukazovalo jen popisek („Hospodařit výš") a co za ním
/// je, se hráč dozvěděl až po kliknutí — nebo vůbec. Rozhodnutí bez znalosti
/// následků není rozhodnutí, takže cena, zisk i dozvuk jsou vidět předem.</para>
/// </summary>
internal static class EventChoiceSummary
{
    /// <summary>Cena, zisk a dozvuk volby; prázdný řetězec, když volba nic nedělá.</summary>
    public static string Line(GameContent content, Localization loc, EventChoiceDef choice)
    {
        var parts = new List<string>(choice.Cost.Count + choice.Gain.Count + 1);
        foreach (var cost in choice.Cost)
        {
            parts.Add($"−{cost.Amount} {loc[content.Resources[cost.ResourceIndex].NameKey]}");
        }

        foreach (var gain in choice.Gain)
        {
            parts.Add($"+{gain.Amount} {loc[content.Resources[gain.ResourceIndex].NameKey]}");
        }

        if (choice.Effect is { } effect)
        {
            parts.Add(Effect(content, loc, effect));
        }

        return string.Join("  ", parts);
    }

    /// <summary>Dozvuk slovy: „Jídlo −25 % na 3 min", „Růst obyvatel +30 % na 2 min".</summary>
    public static string Effect(GameContent content, Localization loc, EventEffectDef effect)
    {
        string percent = SignedPercent(effect.Multiplier);
        string duration = Duration(loc, effect.Seconds);
        return effect.Kind switch
        {
            EventEffectKind.Growth => loc.Format("event.effect.growth", percent, duration),
            _ when effect.ResourceIndex < 0 => loc.Format("event.effect.productionAll", percent, duration),
            _ => loc.Format("event.effect.production",
                loc[content.Resources[effect.ResourceIndex].NameKey], percent, duration),
        };
    }

    /// <summary>
    /// Běžící dozvuky pro HUD: „Jídlo −25 % (2:30)   Růst +30 % (0:45)".
    /// Prázdné, když nic neběží — řádek pak v HUD nezabírá místo.
    /// </summary>
    public static string ActiveLine(GameContent content, Localization loc, IReadOnlyList<ActiveEventEffect> active, long tick)
    {
        if (active.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>(active.Count);
        foreach (var effect in active)
        {
            string percent = SignedPercent(effect.Multiplier);
            string what = effect.Kind switch
            {
                EventEffectKind.Growth => loc.Format("event.active.growth", percent),
                _ when effect.ResourceIndex < 0 => loc.Format("event.active.productionAll", percent),
                _ => loc.Format("event.active.production", loc[content.Resources[effect.ResourceIndex].NameKey], percent),
            };
            long seconds = (long)Math.Ceiling(EventEffects.TicksLeft(effect, tick) / Simulation.TicksPerSecond);
            parts.Add($"{what} ({seconds / 60}:{seconds % 60:00})");
        }

        return string.Join("   ", parts);
    }

    /// <summary>Je mezi běžícími dozvuky aspoň jeden postih? (HUD ho pak barví jako varování.)</summary>
    public static bool AnyPenalty(IReadOnlyList<ActiveEventEffect> active)
    {
        foreach (var effect in active)
        {
            if (effect.Multiplier < 1.0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Násobič jako znaménková změna v procentech: 0,75 → „−25", 1,3 → „+30".</summary>
    public static string SignedPercent(double multiplier)
    {
        int percent = (int)Math.Round((multiplier - 1.0) * 100);
        return percent >= 0 ? $"+{percent}" : $"−{-percent}";
    }

    /// <summary>Celé minuty jako minuty, jinak sekundy — „1,5 min" by se v češtině a němčině četlo jinak než v angličtině.</summary>
    private static string Duration(Localization loc, double seconds)
    {
        int whole = (int)Math.Round(seconds);
        return whole % 60 == 0
            ? loc.Format("event.effect.minutes", whole / 60)
            : loc.Format("event.effect.seconds", whole);
    }
}
