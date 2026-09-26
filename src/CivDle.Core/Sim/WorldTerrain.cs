using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>
/// Terén světa z předvolby a seedu — jedno místo pro založení i načtení.
///
/// <para><b>Proč jedno místo:</b> terén se neukládá, skládá se znovu. Kdyby ho
/// založení kolonie skládalo jinak než čtečka savu, změnila by se po načtení
/// mapa pod stojícím městem. Světy s vlastní vrstvou terénu (zvodně Duny,
/// oblačné moře Nebes) ji přidají tady.</para>
/// </summary>
public static class WorldTerrain
{
    /// <summary>Terén světa, jehož je <paramref name="content"/> obsahem.</summary>
    public static ITerrain Create(GameContent content, TerrainPreset preset, long seed) =>
        new ProceduralTerrain(content.Biomes, preset, seed);
}
