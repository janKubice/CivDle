using CivDle.Core.Content;
using Microsoft.Xna.Framework;

namespace CivDle.Rendering;

/// <summary>
/// Co dělá právě běžící období se zemí.
///
/// <para><b>Proč se to nese jako hodnota:</b> barva se zapéká do chunku, takže
/// musí být známá ve chvíli pečení a pak už se nemění. Kdyby si ji painter
/// držel jako měnitelné pole, přestala by být <c>Tile</c> čistá funkce a testy
/// stínování i skály by musely hlídat, v jakém stavu ho nechal test
/// předchozí.</para>
///
/// <para>Prázdná hodnota (<see cref="None"/>) znamená léto: neutrální
/// základ, ke kterému se ostatní období vztahují.</para>
/// </summary>
/// <param name="Tint">Barva, ke které se zem posune (podzimní hněď, jarní zeleň).</param>
/// <param name="Strength">Jak moc se posune (0 = vůbec, 1 = úplně).</param>
/// <param name="Snow">Kolik sněhu leží na zemi (0 = nic, 1 = bílá plocha).</param>
public readonly record struct SeasonGround(Color Tint, float Strength, float Snow)
{
    /// <summary>Léto: zem si drží vlastní barvu.</summary>
    public static SeasonGround None => new(Color.White, 0f, 0f);

    /// <summary>Přečte vzhled země z definice období. <c>null</c> = neutrální.</summary>
    public static SeasonGround From(SeasonDef? season)
    {
        if (season is null || !season.RepaintsGround)
        {
            return None;
        }

        var tint = season.GroundTint?.ToXna() ?? Color.White;
        return new SeasonGround(tint, (float)season.GroundTintStrength, (float)season.GroundSnow);
    }

    /// <summary>Dělá tohle období se zemí vůbec něco?</summary>
    public bool Changes => Strength > 0.001f || Snow > 0.001f;

    /// <summary>
    /// Klíč pro cache upečených chunků. Když se změní, musí se napéct znovu —
    /// jinak by na jaře zůstal na mapě podzim.
    ///
    /// <para>Kvantizuje se na setiny: období se mění po dnech, ne plynule,
    /// a přepékat celou mapu kvůli zaokrouhlení ve třetí desetině by bylo
    /// škubnutí bez důvodu.</para>
    /// </summary>
    public int CacheKey =>
        (Tint.PackedValue.GetHashCode() * 397) ^ ((int)(Strength * 100) * 31 + (int)(Snow * 100));
}
