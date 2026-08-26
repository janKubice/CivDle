namespace CivDle.Core.Content;

/// <summary>
/// Relikvie z výpravy — trvalý bonus, který si hráč přinesl z místa na mapě.
///
/// <para>Efekt jde tímtéž slovníkem jako Vzestup, družice a osobnosti. Kdyby
/// měly relikvie vlastní cestu k násobičům, byly by ve hře dvě soustavy
/// bonusů a dřív nebo později by se rozešly.</para>
/// </summary>
/// <param name="Id">Identifikátor do dat i do lokalizace (<c>relic.&lt;id&gt;</c>).</param>
/// <param name="Effect">Behavior-ID bonusu.</param>
/// <param name="Magnitude">Síla bonusu.</param>
public sealed record PoiRelicDef(string Id, string Effect, double Magnitude)
{
    /// <summary>Lokalizační klíč jména.</summary>
    public string NameKey => $"relic.{Id}";
}

/// <summary>
/// Jedna možná odměna z výpravy.
/// </summary>
/// <param name="Weight">Váha při losování; větší = častější.</param>
/// <param name="Resources">Co se přiveze.</param>
/// <param name="RelicIndex">Relikvie, nebo −1.</param>
public sealed record PoiRewardDef(int Weight, IReadOnlyList<ResourceAmount> Resources, int RelicIndex)
{
    /// <summary>Přiveze tahle odměna relikvii?</summary>
    public bool HasRelic => RelicIndex >= 0;
}

/// <summary>
/// Druh anomálie: co to je, kde to leží a co stojí tam poslat výpravu.
/// </summary>
/// <param name="Id">Identifikátor do dat i do lokalizace (<c>poi.&lt;id&gt;</c>).</param>
/// <param name="AllowedBiomes">Na kterých biomech se objevuje.</param>
/// <param name="MinDistanceFromStart">
/// Jak daleko od místa, kde civilizace začala. Měří se od počátku světa, ne od
/// těžiště města — to se během hry hýbe a anomálie by se pak objevovaly
/// a mizely podle toho, kam hráč zrovna staví.
/// </param>
/// <param name="Cost">Co stojí vypravit se tam.</param>
/// <param name="DurationTicks">Jak dlouho výprava trvá.</param>
/// <param name="Rewards">Z čeho se losuje odměna.</param>
public sealed record PoiDef(
    string Id,
    bool[] AllowedBiomes,
    int MinDistanceFromStart,
    IReadOnlyList<ResourceAmount> Cost,
    long DurationTicks,
    IReadOnlyList<PoiRewardDef> Rewards)
{
    /// <summary>Lokalizační klíč jména.</summary>
    public string NameKey => $"poi.{Id}";

    /// <summary>Lokalizační klíč popisu.</summary>
    public string DescriptionKey => $"poi.{Id}.desc";

    /// <summary>Smí tenhle druh ležet na tomhle biomu?</summary>
    public bool IsBiomeAllowed(int biomeIndex) =>
        biomeIndex >= 0 && biomeIndex < AllowedBiomes.Length && AllowedBiomes[biomeIndex];

    /// <summary>Součet vah odměn — kolik stran má kostka.</summary>
    public int TotalWeight
    {
        get
        {
            int total = 0;
            for (int i = 0; i < Rewards.Count; i++)
            {
                total += Rewards[i].Weight;
            }

            return total;
        }
    }
}

/// <summary>
/// Anomálie na mapě. Prázdný katalog = mechanika vypnutá.
/// </summary>
/// <param name="Kinds">Druhy anomálií.</param>
/// <param name="Relics">Relikvie, které se z nich dají přivézt.</param>
/// <param name="RegionTiles">
/// Jak velký kus mapy připadá na jednu možnou anomálii. Mřížka je jediný důvod,
/// proč se pozice dají dopočítat z hashe místo držení seznamu — bez ní by se
/// muselo pro každý dotaz procházet celé okolí dlaždici po dlaždici.
/// </param>
/// <param name="ChancePercent">S jakou pravděpodobností v kusu mapy něco je.</param>
public sealed record PoiCatalog(
    IReadOnlyList<PoiDef> Kinds,
    IReadOnlyList<PoiRelicDef> Relics,
    int RegionTiles,
    int ChancePercent)
{
    /// <summary>Hra bez anomálií.</summary>
    public static PoiCatalog Empty { get; } =
        new(Array.Empty<PoiDef>(), Array.Empty<PoiRelicDef>(), 64, 0);

    /// <summary>Objeví se ve světě vůbec něco?</summary>
    public bool IsEnabled => Kinds.Count > 0 && ChancePercent > 0;

    public int Count => Kinds.Count;

    public PoiDef this[int index] => Kinds[index];

    /// <summary>Index druhu podle ID, nebo −1.</summary>
    public int IndexOf(string id)
    {
        for (int i = 0; i < Kinds.Count; i++)
        {
            if (string.Equals(Kinds[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Index relikvie podle ID, nebo −1.</summary>
    public int RelicIndexOf(string id)
    {
        for (int i = 0; i < Relics.Count; i++)
        {
            if (string.Equals(Relics[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
