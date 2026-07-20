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
        bool fed = eaten >= demand - 1e-9;
        if (fed && sim.Population < sim.HousingCapacity)
        {
            sim.Population = Math.Min(
                sim.HousingCapacity,
                sim.Population + _config.PopulationGrowthPerSecond * dt);
        }
    }
}
