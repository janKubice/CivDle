namespace CivDle.Core.Content;

/// <summary>
/// Veškerý načtený a zvalidovaný herní obsah (definice typů). Vzniká jednou při startu
/// v <see cref="ContentLoader"/> a dál se jen čte — systémy ho dostávají závislostí (DI),
/// žádný globální singleton.
/// </summary>
public sealed class GameContent
{
    public GameContent(BiomeRegistry biomes, WorldGenCatalog worldGen)
    {
        Biomes = biomes;
        WorldGen = worldGen;
    }

    /// <summary>Definice biomů z <c>data/biomes.json</c>.</summary>
    public BiomeRegistry Biomes { get; }

    /// <summary>Nastavení generátoru světa z <c>data/worldgen.json</c>.</summary>
    public WorldGenCatalog WorldGen { get; }
}
