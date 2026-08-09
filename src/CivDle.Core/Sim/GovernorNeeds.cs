using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>Co městu zrovna nejvíc chybí. Pořadí = naléhavost.</summary>
public enum CityNeed
{
    /// <summary>Nic akutního — město si vystačí.</summary>
    None,

    /// <summary>Dochází jídlo. Bez něj se zastaví růst, takže tohle je vždycky první.</summary>
    Food,

    /// <summary>Výrobní řetězec stojí, protože nemá vstup (pila bez dřeva).</summary>
    Inputs,

    /// <summary>Lidem chybí služby a spokojenost drží růst při zemi.</summary>
    Services,

    /// <summary>Populace naráží na strop bydlení.</summary>
    Housing,
}

/// <summary>
/// Co může která budova městu nabídnout. Předpočítá se jednou v konstruktoru
/// guvernéra: rozhodování pak nesahá na definice a recepty, jen na pole.
///
/// <para>Proč vlastní typ: guvernér potřebuje odpověď na „umí tahle budova
/// nakrmit / ubytovat / obsloužit / vyrobit X" a bez předpočtu by ji hledal
/// v receptech u každé volby znovu.</para>
/// </summary>
/// <param name="ProducesFood">Vyrábí jídlo (řeší hlad).</param>
/// <param name="Housing">O kolik zvedne kapacitu bydlení.</param>
/// <param name="Services">Kolik bodů služby dodá.</param>
/// <param name="Outputs">Které suroviny umí vyrobit (indexy).</param>
/// <param name="NeedsInputs">Které suroviny ke své výrobě potřebuje (indexy).</param>
public readonly record struct BuildingCapability(
    bool ProducesFood,
    int Housing,
    int Services,
    IReadOnlyList<int> Outputs,
    IReadOnlyList<int> NeedsInputs)
{
    /// <summary>Je budova k něčemu, co guvernér řeší?</summary>
    public bool IsUseful => ProducesFood || Housing > 0 || Services > 0 || Outputs.Count > 0;
}

/// <summary>
/// Posouzení stavu města: co chybí a jak moc.
///
/// <para>Proč to stojí za vlastní třídu: dřív guvernér stavěl „první budovu
/// s <c>autoBuild</c>, která se vejde" — a protože tu značku měla jediná
/// budova (chalupa), automatické město rostlo v lidech, ale nikdo ho nekrmil
/// ani neobsluhoval. Rostlo tak dlouho, dokud nedošlo jídlo, a pak stálo.
/// Tohle je ta chybějící úvaha „co teď město doopravdy potřebuje".</para>
///
/// <para>Čistá funkce nad stavem simulace — nic nemění, takže se dá testovat
/// samostatně a použít i pro nápovědu v UI.</para>
/// </summary>
public sealed class GovernorNeeds
{
    /// <summary>Na kolik sekund dopředu musí být jídlo, aby se nepovažovalo za nedostatek.</summary>
    private const double FoodBufferSeconds = 60;

    /// <summary>Pod tímhle pokrytím služeb se město bere jako neobsloužené.</summary>
    private const double ServiceCoverageFloor = 0.75;

    /// <summary>Pod tímhle naplněním skladu se vstup považuje za vyschlý.</summary>
    private const double InputDryBelow = 0.02;

    private readonly GameContent _content;

    public GovernorNeeds(GameContent content) => _content = content;

    /// <summary>
    /// Co je teď nejnaléhavější. Pořadí není libovolné:
    /// <list type="number">
    /// <item>Hlad zastaví růst úplně, takže jde první.</item>
    /// <item>Vyschlý vstup zastaví celou větev výroby — a často i tu, co dělá jídlo.</item>
    /// <item>Služby drží spokojenost, ta škrtí růst.</item>
    /// <item>Bydlení je „luxusní" problém: bez něj město jen přestane růst, ale žije.</item>
    /// </list>
    /// </summary>
    public CityNeed Assess(Simulation sim)
    {
        Span<CityNeed> all = stackalloc CityNeed[4];
        int count = AssessAll(sim, all);
        return count > 0 ? all[0] : CityNeed.None;
    }

    /// <summary>
    /// Všechny potřeby v pořadí naléhavosti. Vrací, kolik jich je.
    ///
    /// <para>Proč seznam a ne jen ta nejnaléhavější: guvernér musí umět
    /// pokračovat. Když vyschne surovina, kterou žádná auto-stavitelná budova
    /// nevyrábí, nesmí se na ní zaseknout a přestat stavět úplně — jinak město
    /// zůstane stát na stropu bydlení s plným skladem jídla, protože se nikdy
    /// nedostane k tomu postavit dům. (Přesně tohle se stalo, když se rozhodovalo
    /// jen podle první potřeby.)</para>
    /// </summary>
    public int AssessAll(Simulation sim, Span<CityNeed> needs)
    {
        int count = 0;
        if (IsHungry(sim))
        {
            needs[count++] = CityNeed.Food;
        }

        if (DriedUpInput(sim) >= 0)
        {
            needs[count++] = CityNeed.Inputs;
        }

        if (LacksServices(sim))
        {
            needs[count++] = CityNeed.Services;
        }

        if (NeedsHousing(sim))
        {
            needs[count++] = CityNeed.Housing;
        }

        return count;
    }

    /// <summary>
    /// Má město jídlo aspoň na minutu dopředu? Zásoba se poměřuje se spotřebou,
    /// ne s pevným číslem — velkoměsto sní za minutu tolik co vesnice za hodinu.
    /// </summary>
    public bool IsHungry(Simulation sim)
    {
        var gameplay = _content.Gameplay;
        double perSecond = gameplay.FoodPerPersonPerSecond * sim.Population;
        if (perSecond <= 0)
        {
            return false;
        }

        return sim.GetResource(gameplay.FoodResourceIndex) < perSecond * FoodBufferSeconds;
    }

    /// <summary>
    /// Surovina, kterou nějaká postavená budova potřebuje a která došla; −1 = žádná.
    ///
    /// <para>Tohle je ten rozdíl mezi „město stojí" a „město ví, proč stojí":
    /// prázdná pila si řekne o dřevo, ne o další pilu.</para>
    /// </summary>
    public int DriedUpInput(Simulation sim)
    {
        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (_content.Buildings[buildings[i].DefIndex].Recipe is not { } recipe)
            {
                continue;
            }

            for (int j = 0; j < recipe.Inputs.Count; j++)
            {
                int index = recipe.Inputs[j].ResourceIndex;
                double cap = sim.GetStorageCap(index);
                if (sim.GetResource(index) < Math.Max(recipe.Inputs[j].Amount, cap * InputDryBelow))
                {
                    return index;
                }
            }
        }

        return -1;
    }

    /// <summary>Drží spokojenost dole chybějící služby?</summary>
    public bool LacksServices(Simulation sim)
    {
        var happiness = _content.Gameplay.Happiness;
        if (!happiness.IsEnabled || sim.Population <= happiness.FreePopulation)
        {
            return false;
        }

        return sim.HappinessParts.ServiceCoverage < ServiceCoverageFloor;
    }

    /// <summary>
    /// Naráží populace na strop bydlení?
    ///
    /// <para>Na stropu MĚŘÍTKA ne: tam už další dům nikoho nepřivede a guvernér
    /// by donekonečna stavěl prázdné čtvrti. Když je město na stropu, ať radši
    /// řeší služby a výrobu — město se aspoň dál viditelně mění.</para>
    /// </summary>
    public bool NeedsHousing(Simulation sim) =>
        !sim.IsAtScaleCap
        && sim.Population >= sim.HousingCapacity - _content.Gameplay.AutoBuild.PopulationHeadroom;

    /// <summary>
    /// Surovina, která městu chybí na postavení dané budovy; −1 = má na všechno.
    ///
    /// <para>Proč to existuje: <see cref="DriedUpInput"/> hlídá jen vstupy do
    /// receptů, jenže růst zastavuje i <b>cena stavby</b>. Změřeno: město stálo
    /// na stropu bydlení se 170 dřeva a 147 jídla, protože dům stojí prkna,
    /// prkna byla na nule — a protože je žádný recept nespotřebovává, guvernéra
    /// nikdy nenapadlo postavit pilu. Chtěl dům, neměl na něj a jen čekal.</para>
    /// </summary>
    public int MissingBuildMaterial(Simulation sim, int defIndex)
    {
        var cost = _content.Buildings[defIndex].BuildCost;
        for (int i = 0; i < cost.Count; i++)
        {
            if (sim.GetResource(cost[i].ResourceIndex) < cost[i].Amount)
            {
                return cost[i].ResourceIndex;
            }
        }

        return -1;
    }

    /// <summary>Předpočítá, co která definice umí. Volá se jednou při startu.</summary>
    public static BuildingCapability[] Capabilities(GameContent content)
    {
        var defs = content.Buildings.All;
        int foodIndex = content.Gameplay.FoodResourceIndex;
        var result = new BuildingCapability[defs.Count];

        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            var outputs = new List<int>();
            var inputs = new List<int>();
            bool food = false;

            if (def.Recipe is { } recipe)
            {
                for (int j = 0; j < recipe.Outputs.Count; j++)
                {
                    outputs.Add(recipe.Outputs[j].ResourceIndex);
                    food |= recipe.Outputs[j].ResourceIndex == foodIndex;
                }

                for (int j = 0; j < recipe.Inputs.Count; j++)
                {
                    inputs.Add(recipe.Inputs[j].ResourceIndex);
                }
            }

            result[i] = new BuildingCapability(food, def.HousingCapacity, def.ServiceValue, outputs, inputs);
        }

        return result;
    }
}
