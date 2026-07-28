using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Spokojenost města (0–1) — jediná vrstva ve hře, kde stavění NENÍ zadarmo.
/// Bez ní bylo všechno monotónně dobré: víc budov = líp, a hráč nikdy nestál
/// před volbou, u které se dá udělat chyba.
///
/// <para>Skládá se ze dvou tlaků:</para>
/// <list type="bullet">
/// <item><b>Služby.</b> Obyvatelé potřebují obsloužit (trh, sýpka, lázně…). Budova
/// službu dodává, jen když má zaplacenou <b>údržbu</b> — služby tedy něco stojí
/// každou chvíli, ne jen jednou při stavbě.</item>
/// <item><b>Přelidnění.</b> Když se populace tlačí ke stropu bydlení, spokojenost
/// klesá — donutí to stavět dřív, než je pozdě.</item>
/// </list>
///
/// <para>Důsledek je měkký: nízká spokojenost brzdí RŮST, nikdy nikoho nezabíjí
/// ani neboří budovy (soft pressure dle mvp-roadmap.md). Volba zní „další výrobna,
/// nebo služby pro lidi?" — a obojí stojí zdroje.</para>
///
/// <para>Běží na nízké frekvenci (ne každý tik) — je to pomalý městský systém,
/// ne hot path.</para>
/// </summary>
internal sealed class HappinessSystem
{
    private readonly GameContent _content;

    public HappinessSystem(GameContent content)
    {
        _content = content;
    }

    /// <summary>Přepočítá spokojenost a strhne údržbu za obsluhující budovy.</summary>
    public void Tick(Simulation sim)
    {
        var config = _content.Gameplay.Happiness;
        if (!config.IsEnabled)
        {
            sim.Happiness = 1.0;
            return;
        }

        if (sim.TickCount % config.IntervalTicks != 0)
        {
            return;
        }

        sim.Happiness = Evaluate(sim, config, payUpkeep: true).Total;
    }

    /// <summary>
    /// Spočítá spokojenost rozepsanou na položky. <paramref name="payUpkeep"/> =
    /// false umožní systémům i UI se jen podívat, aniž by tím strhly suroviny —
    /// právě proto může UI ukázat rozpad, aniž by hru ovlivnilo.
    /// </summary>
    public HappinessBreakdown Evaluate(Simulation sim, HappinessConfig config, bool payUpkeep)
    {
        // Poptávku po službách tvoří až lidé NAD prahem soběstačnosti — malá
        // vesnice si vystačí sama a hra ji netrestá za chybějící trh, který se
        // stejně odemyká později.
        double demand = sim.Population - config.FreePopulation;
        double coverage = demand <= 0
            ? 1.0
            : Math.Clamp(ServedPeople(sim, config, payUpkeep) / demand, 0.0, 1.0);

        // Přelidnění: čím blíž je populace stropu bydlení, tím hůř se žije.
        double crowding = sim.HousingCapacity <= 0
            ? 1.0
            : Math.Clamp(sim.Population / sim.HousingCapacity, 0.0, 1.0);
        double crowdingPenalty = crowding * config.OvercrowdingPenalty;

        // Kouř se počítá tam, kde lidé bydlí — nad těžištěm města, ne jako průměr
        // přes celou mapu. Díky tomu je „postav hutě za kopcem" skutečné rozhodnutí
        // a ne kosmetika: vzdálená továrna zamoří svoje okolí, ne obývák.
        double smog = -_content.Gameplay.Pollution.HappinessDrop(sim.AirPollutionOverCity);

        // Zvolený program města se přičítá ke spokojenosti — reformátoři a slavnosti
        // nedělají nic jiného, než že lidem zlepší náladu.
        return new HappinessBreakdown(
            Base: config.BaseHappiness,
            Services: coverage * config.ServiceWeight,
            Crowding: -crowdingPenalty,
            Government: sim.ElectionHappinessBonus,
            ServiceCoverage: coverage,
            Pollution: smog);
    }

    /// <summary>
    /// Kolik lidí obslouží postavené budovy. Budova se počítá jen tehdy, když se
    /// za ni zaplatí údržba — to je ta opakovaná cena, která dělá ze služeb
    /// rozhodnutí, a ne samozřejmost.
    /// </summary>
    private double ServedPeople(Simulation sim, HappinessConfig config, bool payUpkeep)
    {
        var buildings = sim.Buildings;
        var resources = sim.Resources;
        double served = 0;

        for (int i = 0; i < buildings.Length; i++)
        {
            var def = _content.Buildings[buildings[i].DefIndex];
            if (def.ServiceValue <= 0)
            {
                continue;
            }

            if (def.Upkeep.Count > 0)
            {
                if (!CanPay(resources, def.Upkeep))
                {
                    continue; // nezaplacená údržba = budova neslouží
                }

                if (payUpkeep)
                {
                    for (int u = 0; u < def.Upkeep.Count; u++)
                    {
                        resources[def.Upkeep[u].ResourceIndex] -= def.Upkeep[u].Amount;
                    }
                }
            }

            served += def.ServiceValue * config.PeoplePerServicePoint;
        }

        return served;
    }

    private static bool CanPay(double[] resources, IReadOnlyList<ResourceAmount> upkeep)
    {
        for (int i = 0; i < upkeep.Count; i++)
        {
            if (resources[upkeep[i].ResourceIndex] < upkeep[i].Amount)
            {
                return false;
            }
        }

        return true;
    }
}
