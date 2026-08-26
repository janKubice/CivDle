namespace CivDle.Core.Content;

/// <summary>
/// Jeden druh družice: co stojí, jak dlouho se staví, co dává a jak vypadá
/// na oběžné dráze.
/// </summary>
/// <param name="Id">Identifikátor do dat i do lokalizace (<c>satellite.&lt;id&gt;</c>).</param>
/// <param name="Sprite">ID spritu.</param>
/// <param name="Cost">Cena první družice tohohle druhu.</param>
/// <param name="CostGrowth">Kolikrát je každá další dražší.</param>
/// <param name="BuildTicks">Jak dlouho trvá stavba a start.</param>
/// <param name="Effect">Behavior-ID bonusu — tytéž efekty jako u Vzestupu a Velkého díla.</param>
/// <param name="Magnitude">Síla bonusu za jednu družici.</param>
/// <param name="MaxCount">Kolik jich smí být nahoře najednou.</param>
/// <param name="Altitude">Poloměr dráhy 0–1 (podíl poloměru obrazovky) — jen pro kreslení.</param>
/// <param name="Speed">Obrátek za sekundu — jen pro kreslení.</param>
public sealed record SatelliteDef(
    string Id,
    string Sprite,
    IReadOnlyList<ResourceAmount> Cost,
    double CostGrowth,
    int BuildTicks,
    string Effect,
    double Magnitude,
    int MaxCount,
    double Altitude,
    double Speed)
{
    /// <summary>
    /// Cena n-té družice tohohle druhu. Roste geometricky: první solární zrcadlo
    /// je meta, šesté je rozhodnutí, jestli se to ještě vyplatí.
    /// </summary>
    public double CostOf(int alreadyLaunched, int resourceIndex)
    {
        for (int i = 0; i < Cost.Count; i++)
        {
            if (Cost[i].ResourceIndex == resourceIndex)
            {
                return Cost[i].Amount * Math.Pow(CostGrowth, alreadyLaunched);
            }
        }

        return 0;
    }

    /// <summary>
    /// Násobič, který dává <paramref name="count"/> družic tohohle druhu.
    /// Skládá se mocninou — stejně jako vylepšení Vzestupu, aby se to chovalo
    /// předvídatelně a nemíchaly se dvě různé matematiky bonusů.
    /// </summary>
    public double MultiplierAt(int count) => Math.Pow(1.0 + Magnitude, count);
}

/// <summary>
/// Oběžná dráha — koncová meta hry.
///
/// <para><b>Proč to není další mapa:</b> je to <b>jiný pohled na tutéž</b>.
/// Družice se nestaví na dlaždici, <b>vypouští se</b> z kosmodromu, a co dělá,
/// dělá globálně. Tím se vyhneme druhému světu s vlastními pravidly a přitom
/// hráč dostane cíl, na který se dá dívat.</para>
///
/// <para>Poloha družice na dráze je <b>funkce tiku</b>, nepočítá se a neukládá.
/// Do savu jde jen kolik čeho je nahoře a co se zrovna staví.</para>
/// </summary>
/// <param name="Satellites">Druhy družic; prázdné = vrstva vypnutá.</param>
/// <param name="LaunchBuildingIndex">Kosmodrom, bez kterého se nedá vypustit nic; −1 = netřeba.</param>
public sealed record OrbitCatalog(
    IReadOnlyList<SatelliteDef> Satellites,
    int LaunchBuildingIndex = -1)
{
    /// <summary>Prázdná dráha — hra bez orbity (starší data, mody).</summary>
    public static OrbitCatalog Empty { get; } = new(Array.Empty<SatelliteDef>());

    /// <summary>Má hra orbitu vůbec zapnutou?</summary>
    public bool IsEnabled => Satellites.Count > 0;

    /// <summary>Potřebuje vypuštění postavený kosmodrom?</summary>
    public bool NeedsLaunchSite => LaunchBuildingIndex >= 0;

    public int Count => Satellites.Count;

    public SatelliteDef this[int index] => Satellites[index];

    /// <summary>Index druhu podle ID, nebo −1.</summary>
    public int IndexOf(string id)
    {
        for (int i = 0; i < Satellites.Count; i++)
        {
            if (string.Equals(Satellites[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
