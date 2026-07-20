namespace CivDle.Core.Content;

/// <summary>
/// Veškerý načtený a zvalidovaný herní obsah (definice typů). Vzniká jednou při startu
/// v <see cref="ContentLoader"/> a dál se jen čte — systémy ho dostávají závislostí (DI),
/// žádný globální singleton.
/// </summary>
public sealed class GameContent
{
    public GameContent(
        BiomeRegistry biomes,
        DefRegistry<Resource> resources,
        DefRegistry<BuildingDef> buildings,
        WorldGenCatalog worldGen,
        GameplayConfig gameplay,
        DefRegistry<LanguageDef> languages)
    {
        Biomes = biomes;
        Resources = resources;
        Buildings = buildings;
        WorldGen = worldGen;
        Gameplay = gameplay;
        Languages = languages;
    }

    /// <summary>Definice biomů z <c>data/biomes.json</c>.</summary>
    public BiomeRegistry Biomes { get; }

    /// <summary>Definice surovin z <c>data/resources.json</c>.</summary>
    public DefRegistry<Resource> Resources { get; }

    /// <summary>Definice budov z <c>data/buildings.json</c>.</summary>
    public DefRegistry<BuildingDef> Buildings { get; }

    /// <summary>Nastavení generátoru světa z <c>data/worldgen.json</c>.</summary>
    public WorldGenCatalog WorldGen { get; }

    /// <summary>Parametry herní smyčky z <c>data/gameplay.json</c>.</summary>
    public GameplayConfig Gameplay { get; }

    /// <summary>Jazyky z <c>data/lang/*.json</c>.</summary>
    public DefRegistry<LanguageDef> Languages { get; }
}
