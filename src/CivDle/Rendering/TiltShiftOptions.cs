namespace CivDle.Rendering;

/// <summary>
/// Kde je obrázek ostrý a jak moc se rozostřuje směrem od pásu ostrosti.
///
/// <para>Tilt-shift dělá z fotky města model na stole. Funguje to proto, že
/// malá hloubka ostrosti patří k makro-fotografii: mozek z ní usoudí, že
/// předmět je malý a blízko. Stačí tedy nechat ostrý úzký vodorovný pás a
/// zbytek rozmazat — víc v tom není.</para>
///
/// <para>Proč <c>record</c> třída a ne <c>readonly record struct</c>: u struktury
/// by <c>new TiltShiftOptions()</c> vyrobilo samé nuly místo výchozích hodnot
/// z hlavičky, což je past, kterou nikdo nečeká. Vzniká jednou za fotku,
/// takže na alokaci nezáleží.</para>
/// </summary>
/// <param name="FocusCenter">Střed ostrého pásu, 0 = horní hrana, 1 = dolní.</param>
/// <param name="FocusHalfHeight">Polovina výšky úplně ostrého pásu (v podílu výšky obrázku).</param>
/// <param name="Falloff">Na jak dlouhém úseku pod pásem přejde ostrost v plné rozostření.</param>
/// <param name="Strength">Strop rozostření, 0 = vypnuto, 1 = naplno.</param>
/// <param name="Punch">Kolik přidat kontrastu a sytosti. 0 = nechat barvy být.</param>
public sealed record TiltShiftOptions(
    float FocusCenter,
    float FocusHalfHeight,
    float Falloff,
    float Strength,
    float Punch)
{
    /// <summary>
    /// Nastavení, kterým se fotí. Pás je <b>pod</b> středem schválně: hráč se
    /// dívá shora dopředu, takže „u něj" je spodní část obrázku a tam má být
    /// ostro — ostrý pruh přesně uprostřed vypadá jako chyba, ne jako záměr.
    /// </summary>
    public static TiltShiftOptions Default { get; } = new(0.56f, 0.13f, 0.30f, 1f, 0.20f);

    /// <summary>Jemnější varianta do videa: rozmazání méně bije do očí při pohybu kamery.</summary>
    public static TiltShiftOptions Gentle { get; } = new(0.56f, 0.20f, 0.34f, 0.8f, 0.14f);

    /// <summary>Dělá tohle nastavení vůbec něco?</summary>
    public bool HasEffect => Strength > 0.001f || Punch > 0.001f;

    /// <summary>
    /// Jak moc je rozostřený řádek na svislé pozici <paramref name="y"/> (0…1).
    /// Vrací 0 v ostrém pásu a <see cref="Strength"/> na okrajích.
    ///
    /// <para>Přechod je hladký (smoothstep), ne lineární — lineární náběh má
    /// zlom na začátku i na konci a ten je na velké ploše vidět jako pruh.</para>
    /// </summary>
    public float BlurAt(float y)
    {
        float distance = Math.Abs(y - FocusCenter) - FocusHalfHeight;
        if (distance <= 0f)
        {
            return 0f;
        }

        float t = Math.Clamp(distance / Math.Max(0.0001f, Falloff), 0f, 1f);
        return t * t * (3f - 2f * t) * Strength;
    }
}
