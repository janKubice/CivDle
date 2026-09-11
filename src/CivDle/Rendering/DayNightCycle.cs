using CivDle.Core.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Denní/noční cyklus jako levný overlay (living-map.md: jedna barevná vrstva
/// přes scénu, žádné přepočítávání spritů). Čas dodává simulace, barvy a síly
/// jsou v datech; tady je jen křivka dne — kód = jak, data = co.
/// </summary>
public static class DayNightCycle
{
    // Svítání ~06:00 (t 0.25), soumrak ~19:00 (t 0.79); rampy ±7 % dne.
    private const float SunriseStart = 0.18f;
    private const float SunriseEnd = 0.32f;
    private const float SunsetStart = 0.72f;
    private const float SunsetEnd = 0.86f;

    // Kotvy gradingu: kdy je světlo teplé, kdy bílé a kdy studené.
    private const float Morning = 0.28f;
    private const float Noon = 0.50f;
    private const float Evening = 0.78f;

    /// <summary>Nejsilnější teplý nádech (ráno). Nad 15 % už to vypadá jako filtr.</summary>
    private const float MorningAlpha = 0.10f;

    /// <summary>Nejsilnější studený nádech (večer).</summary>
    private const float EveningAlpha = 0.12f;

    /// <summary>Síla noci 0–1 (1 = hluboká noc). Plynulé rampy při svítání a soumraku.</summary>
    public static float NightFactor(double timeOfDay01)
    {
        float t = (float)timeOfDay01;
        if (t < SunriseStart || t >= SunsetEnd)
        {
            return 1f;
        }

        if (t < SunriseEnd)
        {
            return 1f - SmoothStep((t - SunriseStart) / (SunriseEnd - SunriseStart));
        }

        if (t < SunsetStart)
        {
            return 0f;
        }

        return SmoothStep((t - SunsetStart) / (SunsetEnd - SunsetStart));
    }

    /// <summary>Síla zlatavého nádechu 0–1 — vrcholí při svítání a soumraku.</summary>
    public static float DuskFactor(double timeOfDay01)
    {
        float t = (float)timeOfDay01;
        return MathF.Max(Bump(t, center: 0.25f, width: 0.09f), Bump(t, center: 0.79f, width: 0.09f));
    }

    /// <summary>
    /// Barevný nádech podle denní doby — to, čemu se ve filmu říká grading.
    ///
    /// <para>Proč to hra potřebuje: den a noc se dosud lišily jen tím, jak moc
    /// je tma. Ráno, poledne a večer měly tutéž barvu, takže hra vypadala
    /// pořád stejně a jediné, co se měnilo, byl jas. Skutečné světlo mění
    /// <b>teplotu</b>: ráno je teplé a nízké, poledne bílé, večer studený
    /// domodra. Tenhle jediný posun udělá z téhož obrazu tři různé nálady.</para>
    ///
    /// <para>Vrací barvu a její krytí; krytí je schválně malé (do 12 %) —
    /// grading má být vidět na screenshotu vedle sebe, ne při hraní.</para>
    /// </summary>
    public static (Color Color, float Alpha) Grade(double timeOfDay01)
    {
        float t = (float)(timeOfDay01 - Math.Floor(timeOfDay01));

        // Tři kotvy přes den. Mezi nimi se plynule přechází, takže se barva
        // nikde neláme.
        var morning = new Color(255, 196, 128);  // teplé nízké slunce
        var noon = new Color(255, 252, 240);     // bílé polední světlo
        var evening = new Color(126, 118, 210);  // modrofialový večer

        // V noci se grading vytrácí ÚPLNĚ. Nejde jen o to, že přes tmu není co
        // gradovat: mezi večerem a ránem je v křivce zlom (modrá → teplá) a
        // kdyby v tu chvíli zbývalo krytí, bylo by o půlnoci vidět bliknutí.
        // Takhle je na obou stranách nula a přechod je neviditelný.
        float daylight = 1f - NightFactor(t);

        if (t < Noon)
        {
            float k = Math.Clamp((t - Morning) / (Noon - Morning), 0f, 1f);
            return (Color.Lerp(morning, noon, k), Lerp(MorningAlpha, 0f, k) * daylight);
        }

        float e = Math.Clamp((t - Noon) / (Evening - Noon), 0f, 1f);
        return (Color.Lerp(noon, evening, e), Lerp(0f, EveningAlpha, e) * daylight);
    }

    /// <summary>
    /// Nakreslí nádech denní doby. Kreslí se pod overlay noci — noc je tma,
    /// tohle je barva světla, které ještě zbývá.
    /// </summary>
    public static void DrawGrade(
        SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, double timeOfDay01)
    {
        var (color, alpha) = Grade(timeOfDay01);
        if (alpha <= 0.001f)
        {
            return;
        }

        spriteBatch.Begin();
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), color * alpha);
        spriteBatch.End();
    }

    /// <summary>Nakreslí barevný overlay podle času — přes svět, pod UI.</summary>
    public static void DrawOverlay(
        SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, DayNightConfig config, double timeOfDay01)
    {
        float night = NightFactor(timeOfDay01);
        float dusk = DuskFactor(timeOfDay01);
        if (night <= 0.001f && dusk <= 0.001f)
        {
            return;
        }

        var bounds = new Rectangle(0, 0, viewport.Width, viewport.Height);
        spriteBatch.Begin();
        if (dusk > 0.001f)
        {
            spriteBatch.Draw(pixel, bounds, config.DuskColor.ToXna() * (float)(config.DuskAlpha * dusk));
        }

        if (night > 0.001f)
        {
            spriteBatch.Draw(pixel, bounds, config.NightColor.ToXna() * (float)(config.NightAlpha * night));
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Nádech ročního období přes scénu — modravá zima, zlatý podzim. Kreslí se
    /// pod overlay dne/noci, aby noc zůstala noc.
    ///
    /// <para>Je to čistě atmosféra: mechaniku období nese simulace, tohle jen
    /// dává hráči poznat, že se něco změnilo, dřív než si přečte HUD.</para>
    /// </summary>
    public static void DrawSeasonTint(
        SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, SeasonDef? season)
    {
        if (season is null || season.TintAlpha <= 0.001)
        {
            return;
        }

        spriteBatch.Begin();
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height),
            season.TintColor.ToXna() * (float)season.TintAlpha);
        spriteBatch.End();
    }

    /// <summary>
    /// Barva světla, kterou se <b>násobí</b> hotová scéna — grading, noc
    /// i roční období v jednom čísle.
    ///
    /// <para><b>Proč násobit a ne překrývat.</b> Dosud se přes obraz táhly tři
    /// průhledné obdélníky (nádech období, grading, tma noci). Průhledný
    /// obdélník míchá každý pixel směrem k sobě, takže tmavá místa zesvětlí
    /// stejně jako světlá ztmaví — kontrast plošně klesá a obraz zmléční.
    /// Násobení dělá pravý opak: tmavé ztmaví víc než světlé, přesně jako když
    /// zapadne slunce. Táž barva, opačný účinek na obraz.</para>
    ///
    /// <para>Tři vrstvy se skládají do jedné proto, že tři průhledné obdélníky
    /// znamenaly tři plné přetažení obrazovky za snímek — a hlavně se jejich
    /// pořadí muselo hlídat ručně.</para>
    /// </summary>
    public static Color LightColor(double timeOfDay01, DayNightConfig config, SeasonDef? season)
    {
        var light = Vector3.One;

        // Grading: barva světla, které zbývá. Síla z Grade() je krytí závoje,
        // takže se používá jako míra, jak daleko od bílé se posunout.
        var (gradeColor, gradeAlpha) = Grade(timeOfDay01);
        if (gradeAlpha > 0.001f)
        {
            light *= Vector3.Lerp(Vector3.One, gradeColor.ToVector3(), gradeAlpha * GradeReach);
        }

        // Roční období.
        if (season is { TintAlpha: > 0.001 })
        {
            light *= Vector3.Lerp(Vector3.One, season.TintColor.ToXna().ToVector3(), (float)season.TintAlpha);
        }

        // Soumrak a noc: nejdřív zlatavý nádech, pak ztmavení do modra.
        float dusk = DuskFactor(timeOfDay01);
        if (dusk > 0.001f)
        {
            light *= Vector3.Lerp(Vector3.One, config.DuskColor.ToXna().ToVector3(), (float)(config.DuskAlpha * dusk));
        }

        float night = NightFactor(timeOfDay01);
        if (night > 0.001f)
        {
            // Noc nesmí spadnout na čerň: z násobení nulou už nic nevytáhne ani
            // pouliční lampa. Zbytek světla je měsíc.
            var nightColor = config.NightColor.ToXna().ToVector3();
            float strength = (float)(config.NightAlpha * night);
            light *= Vector3.Lerp(Vector3.One, Vector3.Lerp(nightColor, Vector3.Zero, 0.35f), strength);
            light = Vector3.Max(light, new Vector3(MinimumLight));
        }

        return new Color(light);
    }

    /// <summary>
    /// Vynásobí hotovou scénu barvou světla.
    ///
    /// <para>Jedno místo pro celou hru: totéž potřebuje herní obrazovka
    /// i focení do obchodu, a kdyby si to každá dělala po svém, vypadal by
    /// screenshot jinak než hra, ze které vznikl.</para>
    /// </summary>
    public static void DrawLight(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, Color light)
    {
        if (light == Color.White)
        {
            return; // poledne: násobit jedničkou je zbytečné přetažení obrazovky
        }

        spriteBatch.Begin(blendState: MultiplyBlend, samplerState: SamplerState.PointClamp);
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), light);
        spriteBatch.End();
    }

    /// <summary>
    /// Vynásobí cíl zdrojem — z barvy světla a scény udělá osvětlenou scénu.
    /// Průhledný obdélník by místo toho kontrast rozpustil.
    /// </summary>
    public static BlendState MultiplyBlend { get; } = new()
    {
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.Zero,
        AlphaSourceBlend = Blend.DestinationAlpha,
        AlphaDestinationBlend = Blend.Zero,
    };

    /// <summary>
    /// Kolikrát silněji se grading projeví jako násobič než jako závoj.
    ///
    /// <para>Krytí závoje bylo schválně malé (do 12 %), protože závoj kontrast
    /// ničí. Násobič ho naopak drží, takže si může dovolit víc — jinak by po
    /// převodu byla zlatá hodina sotva vidět.</para>
    /// </summary>
    private const float GradeReach = 2.2f;

    /// <summary>Nejtmavší, na co smí noc scénu stáhnout. Pod tím přestane být vidět tvar.</summary>
    private const float MinimumLight = 0.22f;

    /// <summary>
    /// Jak silná má být záře. V noci nejvíc — okna a lampy jsou pak jediné
    /// světlo v obraze a mají zářit; v poledni by ze všeho dělala mlhu.
    /// </summary>
    public static float BloomStrength(double timeOfDay01) =>
        0.12f + 0.38f * NightFactor(timeOfDay01);

    private static float SmoothStep(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);

    private static float Bump(float t, float center, float width) =>
        MathF.Max(0f, 1f - MathF.Abs(t - center) / width);
}
