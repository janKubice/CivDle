using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Co smí guvernér postavit a jak dobře co vyrábí — předpočítané z dat.
///
/// <para><b>Proč to vzniklo:</b> guvernér dřív smel stavět jen sedmnáct budov
/// se značkou <c>autoBuild</c>. Sklad, lesní školka, přístav, rybárna, doly ani
/// elektrárna mezi nimi nebyly, takže je nepostavil nikdy — a město, které vedl
/// sám, zůstalo u chalup a polí. Role se teď poznají z dat (recept, sklad,
/// služby, obnova krajiny, výkon), takže i budova z modu funguje bez kódu.</para>
///
/// <para><b>Co zůstává hráči:</b> divy a megastavby (velká rozhodnutí se
/// stavbou na minuty), podmořské stavby, terraformace (mění mapu) a stavby,
/// které se staví déle než okamžik. Zákaz kategorie v plánu guvernéra platí
/// dál — ten se kontroluje až u konkrétní dlaždice.</para>
///
/// <para><b>Bydlení</b> staví guvernér dál jen ze značky <c>autoBuild</c>:
/// větší domy vznikají povýšením menších, a to je obraz, kterým město
/// dospívá. Kdyby rovnou stavěl paneláky, vesnice by přeskočila celé mládí.</para>
///
/// <para>Bez bloku <c>governor</c> v datech (<see cref="GovernorConfig.Classic"/>)
/// platí dosavadní pravidlo: jen <c>autoBuild</c>.</para>
/// </summary>
internal sealed class GovernorRoles
{
    private readonly GameContent _content;

    /// <summary>Smí budovu stavět guvernér podle role (bez ohledu na odemčení)?</summary>
    private readonly bool[] _byRole;

    public GovernorRoles(GameContent content)
    {
        _content = content;
        BuildsByRole = content.Gameplay.Governor.BuildsByRole;
        _byRole = new bool[content.Buildings.Count];
        for (int i = 0; i < _byRole.Length; i++)
        {
            var def = content.Buildings[i];
            _byRole[i] = def.Buildable && !IsPlayersDecision(def);
        }
    }

    /// <summary>Staví guvernér podle rolí (jinak jen <c>autoBuild</c>)?</summary>
    public bool BuildsByRole { get; }

    /// <summary>Smí guvernér budovu postavit sám od sebe (a je odemčená)?</summary>
    public bool MayBuild(Simulation sim, int defIndex)
    {
        var def = _content.Buildings[defIndex];
        bool allowed = BuildsByRole ? _byRole[defIndex] || def.AutoBuild : def.AutoBuild;
        return allowed && sim.IsBuildingUnlocked(defIndex);
    }

    /// <summary>Smí guvernér budovu postavit jako bydlení? Jen <c>autoBuild</c> — viz popis třídy.</summary>
    public bool MayBuildHousing(Simulation sim, int defIndex) =>
        _content.Buildings[defIndex].AutoBuild && sim.IsBuildingUnlocked(defIndex);

    /// <summary>Smí guvernér budovu postavit, aby dokrmil řetězec (vyrábí něco)?</summary>
    public bool MayBuildForSupply(int defIndex) =>
        BuildsByRole && _byRole[defIndex] && _content.Buildings[defIndex].Recipe is not null;

    /// <summary>
    /// Kolik suroviny budova vyrobí za sekundu (při plném obsazení, bez bonusů).
    /// Slouží k pořadí kandidátů: když výzkum odemkne výkonnější výrobnu,
    /// guvernér ji má vzít přednostně, ne dál stavět tu první.
    /// </summary>
    public double RateOf(int defIndex, int resource)
    {
        if (_content.Buildings[defIndex].Recipe is not { } recipe || recipe.TimeTicks <= 0)
        {
            return 0;
        }

        double perSecond = Simulation.TicksPerSecond / recipe.TimeTicks;
        for (int i = 0; i < recipe.Outputs.Count; i++)
        {
            if (recipe.Outputs[i].ResourceIndex == resource)
            {
                return recipe.Outputs[i].Amount * perSecond;
            }
        }

        return 0;
    }

    /// <summary>
    /// Je stavba hráčovým rozhodnutím, které automatika dělat nemá? Viz popis
    /// třídy — velké, pomalé nebo krajinu měnící stavby.
    /// </summary>
    public static bool IsPlayersDecision(BuildingDef def) =>
        def.Category is "monument" or "megastructure"
        || def.IsSubsea
        || def.TerraformActionIndex >= 0
        || def.TakesTimeToBuild;
}
