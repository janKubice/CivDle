using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Znečištění krajiny: kdo ho vyrábí, kam se rozlévá a komu vadí.
///
/// <para>Proč to v hře je: do industriální éry byl rozvoj čistě dobrý — každá další
/// budova znamenala jen větší čísla. Od hutí dál za sebou nechává stopu, kterou je
/// na mapě vidět, a hráč poprvé řeší, <b>kam</b> těžký průmysl postaví. Zároveň je
/// to jediná stopa, která jde vzít zpátky: čističky jsou v datech tytéž budovy se
/// záporným znaménkem, takže vyčištěná mapa je odměna, ne účetní operace.</para>
///
/// <para>Bronzová doba se čistá drží sama: emise nejsou v kódu, ale v datech budov
/// (<c>pollution</c> v <c>buildings.json</c>), a ty je mají až od průmyslu dál.</para>
///
/// <para>Výkon: pomalý systém na hrubé mřížce (CLAUDE.md). Běží jednou za
/// <see cref="PollutionConfig.IntervalTicks"/>, ne každý tik, a dopad na výrobu si
/// každá budova nese nacachovaný v <see cref="BuildingInstance.PollutionMult"/> —
/// v tikové smyčce výroby se do mřížky nesahá.</para>
/// </summary>
internal sealed class PollutionSystem
{
    private readonly GameContent _content;

    /// <summary>Emise jednotlivých definic (desítky), ne budov (statisíce).</summary>
    private readonly PollutionOutput[] _emission;

    /// <summary>Které definice vůbec sahají na znečištění — zkratka pro rychlý průchod.</summary>
    private readonly bool[] _affects;

    /// <summary>Kterým definicím vadí otrávená půda pod nimi (pole, lesy, doly).</summary>
    private readonly bool[] _soilSensitive;

    /// <summary>Kterým definicím vadí zkalená voda (přístavy, rybolov).</summary>
    private readonly bool[] _waterSensitive;

    /// <summary>Byla naposledy nějaká budova potrestaná? Řídí úklid násobičů po vyčištění.</summary>
    private bool _penaltiesApplied;

    public PollutionSystem(GameContent content)
    {
        _content = content;
        var defs = content.Buildings.All;
        _emission = new PollutionOutput[defs.Count];
        _affects = new bool[defs.Count];
        _soilSensitive = new bool[defs.Count];
        _waterSensitive = new bool[defs.Count];

        int foodIndex = content.Gameplay.FoodResourceIndex;
        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            _emission[i] = def.Pollution;
            _affects[i] = def.AffectsPollution;

            // Citlivost se neopisuje do JSON zvlášť — plyne z toho, co budova je.
            // Kdo bere surovinu přímo z půdy (les, ložisko) nebo pěstuje jídlo,
            // toho otrávená země bolí; kdo stojí na břehu, toho bolí kalná voda.
            _waterSensitive[i] = def.NeedsWaterAccess;
            _soilSensitive[i] = def.HarvestsTerrain || ProducesFood(def, foodIndex);
        }
    }

    /// <summary>Jeden přepočet znečištění — emise, rozliv, dopad na budovy.</summary>
    public void Tick(Simulation sim)
    {
        var config = _content.Gameplay.Pollution;
        if (!config.IsEnabled || sim.TickCount % config.IntervalTicks != 0)
        {
            return;
        }

        Emit(sim, config);
        sim.PollutionMap.Diffuse(config.SpreadRate, config.DecayRate);
        RefreshPenalties(sim, config);
    }

    /// <summary>
    /// Nechá špinící budovy vypustit svou dávku a čističky ji odsát. Čistička si
    /// přitom musí zaplatit údržbu — náprava je průběžný náklad, ne jednorázový
    /// nákup, jinak by stačilo postavit tři budovy a na znečištění zapomenout.
    /// </summary>
    private void Emit(Simulation sim, PollutionConfig config)
    {
        var buildings = sim.Buildings;
        var map = sim.PollutionMap;
        var resources = sim.Resources;
        double seconds = config.IntervalSeconds;

        for (int i = 0; i < buildings.Length; i++)
        {
            int defIndex = buildings[i].DefIndex;
            if (!_affects[defIndex] || !buildings[i].IsComplete)
            {
                continue; // staveniště ještě nedýmá ani nečistí
            }

            var def = _content.Buildings[defIndex];
            if (def.Pollution.IsCleaner && !TryPayUpkeep(resources, def.Upkeep))
            {
                continue; // vypnutá čistička nečistí (a hráč to na mapě uvidí)
            }

            // Doprostřed půdorysu, ne do rohu — u velkých budov by roh házel
            // špínu do vedlejší buňky a hráč by nechápal, proč zrovna tam.
            int x = buildings[i].X + def.FootprintWidth / 2;
            int y = buildings[i].Y + def.FootprintHeight / 2;
            var output = _emission[defIndex];

            // Stinná stránka čtvrti: soustředěný průmysl vyrábí líp, ale i víc
            // dýmá. Bez tohohle by byla synergie čistý zisk a shlukování by
            // nebylo rozhodnutí, jen správná odpověď. Čističek se to netýká —
            // trestat je za to, že stojí u sebe, by bylo obráceně.
            double districtMult = 1.0;
            if (!def.Pollution.IsCleaner && sim.DistrictOf(i) is { } district)
            {
                districtMult = _content.Districts.Types[district.TypeIndex].PollutionMult;
            }

            map.Emit(x, y, PollutionKind.Air, output.Air * seconds * districtMult);
            map.Emit(x, y, PollutionKind.Water, output.Water * seconds * districtMult);
            map.Emit(x, y, PollutionKind.Soil, output.Soil * seconds * districtMult);
        }
    }

    /// <summary>
    /// Přepíše budovám nacachovaný násobič výroby podle zamoření pod nimi.
    ///
    /// <para>Průchod se úplně přeskočí, dokud je mapa čistá — v bronzové době tenhle
    /// systém nestojí prakticky nic. Jakmile se svět vyčistí, proběhne ještě jednou,
    /// aby se násobiče vrátily na 1.0; jinak by po posledním zamoření zůstal trest
    /// viset navždy.</para>
    /// </summary>
    private void RefreshPenalties(Simulation sim, PollutionConfig config)
    {
        bool dirty = sim.PollutionMap.DirtyCellCount > 0;
        if (!dirty && !_penaltiesApplied)
        {
            return;
        }

        var buildings = sim.BuildingsMutable;
        for (int i = 0; i < buildings.Length; i++)
        {
            ref var building = ref buildings[i];
            building.PollutionMult = (float)MultiplierAt(sim, building.DefIndex, building.X, building.Y);
        }

        _penaltiesApplied = dirty;
    }

    /// <summary>
    /// Násobič výroby pro budovu daného typu na daném místě. Veřejné uvnitř
    /// simulace ze stejného důvodu jako u svozu: čerstvě položená budova má mít
    /// správné číslo hned, ne až po nejbližším pomalém přepočtu.
    /// </summary>
    public double MultiplierAt(Simulation sim, int defIndex, int x, int y)
    {
        var config = _content.Gameplay.Pollution;
        if (!config.IsEnabled || (!_soilSensitive[defIndex] && !_waterSensitive[defIndex]))
        {
            return 1.0;
        }

        var map = sim.PollutionMap;
        double worst = 0;
        if (_soilSensitive[defIndex])
        {
            worst = Math.Max(worst, map.At(x, y, PollutionKind.Soil));
        }

        if (_waterSensitive[defIndex])
        {
            worst = Math.Max(worst, map.At(x, y, PollutionKind.Water));
        }

        return config.ProductionMultiplier(worst);
    }

    /// <summary>Pěstuje budova jídlo? (Farmě vadí otrávená půda, huti ne.)</summary>
    private static bool ProducesFood(BuildingDef def, int foodIndex)
    {
        if (def.Recipe is not { } recipe)
        {
            return false;
        }

        for (int i = 0; i < recipe.Outputs.Count; i++)
        {
            if (recipe.Outputs[i].ResourceIndex == foodIndex)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Strhne údržbu, jde-li zaplatit celá. Půlka údržby by znamenala půlku
    /// čištění za plnou cenu — buď čistička jede, nebo stojí.
    /// </summary>
    private static bool TryPayUpkeep(double[] resources, IReadOnlyList<ResourceAmount> upkeep)
    {
        for (int i = 0; i < upkeep.Count; i++)
        {
            if (resources[upkeep[i].ResourceIndex] < upkeep[i].Amount)
            {
                return false;
            }
        }

        for (int i = 0; i < upkeep.Count; i++)
        {
            resources[upkeep[i].ResourceIndex] -= upkeep[i].Amount;
        }

        return true;
    }
}
