namespace CivDle.Rendering;

/// <summary>
/// Vítr, který nikam nefouká — jen dává scéně dech.
///
/// <para>Proč to hra potřebuje: idle hra se dívá sama na sebe většinu času.
/// Když hráč nic nedělá, obraz se úplně zastaví, a zastavený obraz mozek
/// přestane vnímat jako místo a začne ho vnímat jako obrázek. Stačí přitom
/// málo: aby se stromy sotva znatelně kolébaly a kouř stoupal. Rozdíl mezi
/// „screenshot" a „scéna" je v pohybu, ne v detailu.</para>
///
/// <para>Sdílený zdroj: stromy, kouř i chvění nad hutěmi se hýbou <b>podle
/// téhož větru</b>. Kdyby měl každý svůj vlastní, viděl by hráč tři nezávislé
/// animace místo jednoho počasí.</para>
///
/// <para>Statické a bez stavu — čas přichází parametrem, takže je to
/// testovatelné a nikdy se to nerozejde mezi vrstvami.</para>
/// </summary>
public static class AmbientWind
{
    /// <summary>Největší náklon stromu v radiánech. Víc už je bouře, ne dech.</summary>
    public const float MaxSway = 0.055f;

    /// <summary>
    /// Síla poryvu 0–1, společná pro celou scénu.
    ///
    /// <para>Dvě vlny s nesoudělnou periodou: vítr má chvílemi zesílit
    /// a chvílemi skoro ustat. Jedna sinusovka by dýchala jako metronom
    /// a oko na pravidelnost přijde do pár vteřin.</para>
    /// </summary>
    public static float Gust(float time)
    {
        float slow = MathF.Sin(time * 0.21f);
        float faster = MathF.Sin(time * 0.53f + 1.7f);
        return Math.Clamp(0.5f + 0.3f * slow + 0.2f * faster, 0f, 1f);
    }

    /// <summary>
    /// Náklon stromu na dané dlaždici (radiány, kladné = doprava).
    ///
    /// <para>Fáze se odvozuje z polohy, takže se sousední stromy nekolébají
    /// v zákrytu — les jako jeden kus by vypadal jako chyba animace.</para>
    /// </summary>
    public static float Sway(int tileX, int tileY, float time)
    {
        float phase = (Hash(tileX, tileY) % 628) / 100f; // 0–2π
        float strength = 0.35f + 0.65f * Gust(time);
        return MathF.Sin(time * 1.35f + phase) * MaxSway * strength;
    }

    /// <summary>
    /// Vodorovný snos stoupajícího kouře v pixelech ve výšce <paramref name="rise01"/>.
    ///
    /// <para>Kouř se musí snášet <b>na tutéž stranu</b> jako se naklánějí
    /// stromy, jinak by v jedné scéně foukalo dvěma směry. A snos musí růst
    /// s výškou — u komína je kouř ještě rovně, nahoře už ho vítr odnáší.</para>
    /// </summary>
    public static float Drift(float time, float rise01, float phase)
    {
        float strength = 0.35f + 0.65f * Gust(time);
        return (2.5f + 6f * strength) * rise01 + MathF.Sin(time * 0.9f + phase) * 1.5f * rise01;
    }

    private static ulong Hash(int x, int y)
    {
        ulong h = (ulong)(uint)x * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)y * 0xBF58476D1CE4E5B9UL;
        h = (h ^ (h >> 30)) * 0xBF58476D1CE4E5B9UL;
        return h ^ (h >> 31);
    }
}
