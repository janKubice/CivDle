using CivDle.Core.Galaxy;
using CivDle.Core.Sim;

namespace CivDle.Core.Save;

/// <summary>
/// Přečtený save: aktivní svět, jeho metadata a stav galaxie.
/// </summary>
/// <param name="Simulation">Aktivní svět.</param>
/// <param name="Metadata">Seed, velikost a předvolba aktivního světa, čas uložení.</param>
/// <param name="Galaxy">Galaxie; <c>null</c> = save z doby před galaxií (jen Domovina).</param>
public sealed record LoadedSave(Simulation Simulation, SaveMetadata Metadata, GalaxyState? Galaxy);
