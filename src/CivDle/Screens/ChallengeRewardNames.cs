using CivDle.Core.Content;

namespace CivDle.Screens;

/// <summary>
/// Jména odměn výzvy pro hráče („Kámen zakladatelů, Úsporná správa").
///
/// <para>Odměnu výzvy nese obsah (<c>unlockedBy</c> u budov a politik), ne
/// výzva — tady se jen posbírá, co na klíč míří. Stejné jméno tak hráč uvidí
/// v nabídce výzev i v oslavě po výhře.</para>
/// </summary>
public static class ChallengeRewardNames
{
    /// <summary>
    /// Co odemkne klíč (<c>challenge:&lt;id&gt;</c>), jménem, oddělené čárkou.
    /// Prázdné, když na klíč nic nemíří.
    /// </summary>
    public static string ForKey(GameContent content, Localization loc, string unlockKey)
    {
        var names = new List<string>();
        foreach (var building in content.Buildings.All)
        {
            if (building.UnlockedBy == unlockKey)
            {
                names.Add(loc[building.NameKey]);
            }
        }

        foreach (var policy in content.Policies.All)
        {
            if (policy.UnlockedBy == unlockKey)
            {
                names.Add(loc[policy.NameKey]);
            }
        }

        return string.Join(", ", names);
    }

    /// <summary>
    /// Co právě odemkla výhra výzvy — její vlastní odměna, a byla-li to
    /// poslední chybějící výzva, i odměna Mistra.
    /// </summary>
    public static string Of(GameContent content, Localization loc, string scenarioId, IEnumerable<string> won)
    {
        string own = ForKey(content, loc, ChallengeRewards.KeyOf(scenarioId));
        bool allWon = ChallengeRewards.WonCount(content.Scenarios, won) == content.Scenarios.Count;
        if (!allWon)
        {
            return own;
        }

        string master = ForKey(content, loc, ChallengeRewards.KeyOf(ChallengeRewards.AllChallengesId));
        return own.Length == 0 ? master : master.Length == 0 ? own : $"{own}, {master}";
    }
}
