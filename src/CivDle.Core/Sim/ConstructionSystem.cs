using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Staveniště: budovy s dobou stavby (divy světa) odpočítávají, než začnou
/// fungovat.
///
/// <para>Proč to v hře je: megastruktura, která vyroste jedním kliknutím, je jen
/// drahá budova. S odpočtem je z ní událost — na mapě stojí staveniště, hráč se
/// k němu vrací, sleduje postup a dokončení něco znamená. Právě proto se bonusy
/// (bydlení, pracovní místa, sklad, proud) připisují až na konci: kdyby platily
/// hned, odpočet by byl jen kosmetika.</para>
///
/// <para>Výkon: běží na nízké frekvenci a jen když se vůbec něco staví
/// (<see cref="Simulation.BuildingsUnderConstruction"/>) — jinak se pole budov
/// vůbec neprochází.</para>
/// </summary>
internal sealed class ConstructionSystem
{
    /// <summary>Jak často se na staveniště sáhne. Pomalý systém, ne hot path.</summary>
    public const int IntervalTicks = 5;

    private readonly GameContent _content;

    public ConstructionSystem(GameContent content) => _content = content;

    public void Tick(Simulation sim)
    {
        if (sim.BuildingsUnderConstruction == 0 || sim.TickCount % IntervalTicks != 0)
        {
            return;
        }

        var buildings = sim.BuildingsMutable;
        for (int i = 0; i < buildings.Length; i++)
        {
            ref var building = ref buildings[i];
            if (building.IsComplete)
            {
                continue;
            }

            building.BuildTicksRemaining -= IntervalTicks;
            if (building.BuildTicksRemaining > 0)
            {
                continue;
            }

            building.BuildTicksRemaining = 0;
            sim.CompleteConstruction(i, _content.Buildings[building.DefIndex]);
        }
    }
}
