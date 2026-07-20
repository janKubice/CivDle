using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.WorldGen;

/// <summary>Zadání pro generátor: seed + rozměry + terénní preset (z katalogu nebo testu).</summary>
public sealed record WorldGenRequest(long Seed, int Width, int Height, TerrainPreset Preset);

/// <summary>
/// Materializuje konečný výřez nekonečného <see cref="ProceduralTerrain"/> do
/// <see cref="WorldMap"/>. Používá se pro náhledy a testy, které potřebují
/// konkrétní mřížku; samotná hra běží přímo nad nekonečným terénem.
/// </summary>
public sealed class MapGenerator
{
    /// <summary>Vygeneruje konečnou mapu vzorkováním procedurálního terénu.</summary>
    public WorldMap Generate(GameContent content, WorldGenRequest request)
    {
        var terrain = new ProceduralTerrain(content.Biomes, request.Preset, request.Seed);
        var map = new WorldMap(request.Width, request.Height);

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                int index = map.Index(x, y);
                map.BiomeIndices[index] = terrain.BiomeAt(x, y);
                map.Elevation[index] = terrain.ElevationAt(x, y);
                map.Moisture[index] = terrain.MoistureAt(x, y);
            }
        }

        return map;
    }
}
