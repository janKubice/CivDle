namespace CivDle.Core.Content;

/// <summary>
/// Jeden stupeň sídla z <c>data/settlement-ranks.json</c>: osada → vesnice →
/// městečko → město → velkoměsto → metropole.
///
/// <para>Proč to ve hře je (living-city.md §6): všech dvanáct milníků hry bylo
/// globální číslo — populace, budovy, Vzestupy. Nic nebylo vázané na <b>místo</b>.
/// Se stupni má každé sídlo vlastní žebříček a růst se dá číst přímo na mapě:
/// „tady je Zkouškovice, městečko o dvaceti budovách". To je ta civilizační
/// fantazie, kvůli které se oddaluje.</para>
///
/// <para>Práh je počet budov ve shluku, ne populace: populace je v téhle hře
/// globální agregát (CLAUDE.md — nikdy nesimulovat miliony jednotlivců), takže
/// „kolik lidí bydlí zrovna tady" neexistuje. Počet budov je poctivé měřítko
/// místa a hráč ho vidí na první pohled.</para>
/// </summary>
/// <param name="Id">Stabilní ID (do lokalizace).</param>
/// <param name="MinBuildings">Od kolika budov ve shluku stupeň platí.</param>
public sealed record SettlementRankDef(string Id, int MinBuildings)
{
    /// <summary>Lokalizační klíč jména stupně.</summary>
    public string NameKey => $"rank.{Id}";
}

/// <summary>
/// Žebříček stupňů sídel, seřazený od nejmenšího. Prázdný je legitimní stav —
/// hra bez hierarchie se chová jako dřív (sídlo je prostě „sídlo").
/// </summary>
public sealed record SettlementRankLadder(IReadOnlyList<SettlementRankDef> Ranks)
{
    /// <summary>Žádná hierarchie — pro starší data i pro testy, které ji neřeší.</summary>
    public static SettlementRankLadder Empty { get; } = new(Array.Empty<SettlementRankDef>());

    /// <summary>Má smysl stupně vůbec počítat?</summary>
    public bool IsEnabled => Ranks.Count > 0;

    /// <summary>
    /// Nejvyšší stupeň, na který shluk dané velikosti dosáhne; −1 = ani na
    /// nejnižší (shluk je moc malý, nebo je žebříček prázdný).
    /// </summary>
    public int RankFor(int buildingCount)
    {
        int rank = -1;
        for (int i = 0; i < Ranks.Count; i++)
        {
            if (buildingCount >= Ranks[i].MinBuildings)
            {
                rank = i;
            }
        }

        return rank;
    }

    /// <summary>Definice stupně, nebo <c>null</c> u neplatného indexu.</summary>
    public SettlementRankDef? At(int rankIndex) =>
        rankIndex >= 0 && rankIndex < Ranks.Count ? Ranks[rankIndex] : null;

    /// <summary>Index stupně podle ID; −1 = neznámý (pro odkazy z jiných dat).</summary>
    public int IndexOf(string id)
    {
        for (int i = 0; i < Ranks.Count; i++)
        {
            if (string.Equals(Ranks[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
