using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Populace jako agregát: spotřebovává jídlo a roste, když je co jíst a kde bydlet.
/// Došlé jídlo růst jen zastaví (soft pressure z mvp-roadmap.md) — nikdy nikdo neumírá.
/// </summary>
internal sealed class PopulationSystem
{
    private readonly GameplayConfig _config;

    public PopulationSystem(GameplayConfig config)
    {
        _config = config;
    }

    public void Tick(Simulation sim)
    {
        double dt = 1.0 / Simulation.TicksPerSecond;
        var resources = sim.Resources;

        double food = resources[_config.FoodResourceIndex];
        double demand = sim.Population * _config.FoodPerPersonPerSecond * dt;
        double eaten = Math.Min(food, demand);
        resources[_config.FoodResourceIndex] = food - eaten;

        // Roste se jen s plným břichem a volnou kapacitou bydlení.
        // Trvalý bonus Vzestupu růst zrychluje.
        //
        // Strop měřítka (stupeň Vzestupu) je druhá, MĚKKÁ brzda: růst se u něj
        // zastaví, ale nic se neboří — je to pobídka k Vzestupu, ne trest
        // (progression-prestige.md §6).
        double ceiling = Math.Min(sim.HousingCapacity, sim.PopulationCap);
        bool fed = eaten >= demand - 1e-9;
        if (fed && sim.Population < ceiling)
        {
            sim.Population = Math.Min(
                ceiling,
                sim.Population + _config.PopulationGrowthPerSecond * dt * sim.Bonuses.GrowthMult);
        }
    }
}
