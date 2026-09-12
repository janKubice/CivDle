namespace CivDle.Rendering.Effects;

/// <summary>Kam má chodec zrovna namířeno.</summary>
public enum Errand
{
    /// <summary>Do práce — dílny, pily, pole.</summary>
    Work,

    /// <summary>Domů — obytné budovy.</summary>
    Home,

    /// <summary>Nikam určitě: pochůzka, trh, návštěva.</summary>
    Anywhere,
}

/// <summary>
/// Denní rytmus města: co lidi zrovna dělají a kolik jich je venku.
///
/// <para><b>Proč to vzniklo:</b> chodci se pohybovali náhodnou procházkou a na
/// denní dobu se neptali vůbec. Město tak vypadalo ve tři ráno stejně jako
/// v poledne — a protože nikdo nikam nešel, ani nikdo nikam nedošel. Rytmus je
/// nejlevnější způsob, jak z pohybu udělat <i>chování</i>: ráno se jde do
/// práce, večer domů, v noci je klid.</para>
///
/// <para>Je to čistá funkce času, takže se dá testovat bez okna i bez
/// simulace — a renderu stačí <c>Simulation.TimeOfDay01</c>, do kterého
/// nezapisuje.</para>
///
/// <para>Čas běží 0 = půlnoc, 0,5 = poledne.</para>
/// </summary>
public static class DayRhythm
{
    private const double MorningStart = 0.22; // ~5:20 — svítá, lidi vyrážejí
    private const double MorningEnd = 0.42;   // ~10:00 — všichni jsou v práci
    private const double EveningStart = 0.68; // ~16:20 — padla
    private const double EveningEnd = 0.88;   // ~21:00 — doma

    /// <summary>
    /// Kam se jde v tuhle hodinu. Ráno do práce, večer domů, přes den a v noci
    /// nic určitého — kdo je venku v noci, ten nemá směnu, ale důvod.
    /// </summary>
    public static Errand ErrandAt(double timeOfDay01)
    {
        double t = Wrap(timeOfDay01);

        if (t >= MorningStart && t < MorningEnd)
        {
            return Errand.Work;
        }

        return t >= EveningStart && t < EveningEnd ? Errand.Home : Errand.Anywhere;
    }

    /// <summary>
    /// Jaká část obvyklého davu je venku (0–1).
    ///
    /// <para>V noci se neuklidí úplně: prázdná ulice vypadá jako vypnutá hra,
    /// ne jako spící město. Zůstane hlídka, opozdilec, někdo u vody.</para>
    /// </summary>
    public static float CrowdAt(double timeOfDay01)
    {
        double t = Wrap(timeOfDay01);

        // Náběh za svítání a doběh po setmění; mezi tím plný provoz.
        if (t < MorningStart)
        {
            return NightCrowd;
        }

        if (t < MorningEnd)
        {
            return Lerp(NightCrowd, 1f, (float)((t - MorningStart) / (MorningEnd - MorningStart)));
        }

        if (t < EveningStart)
        {
            return 1f;
        }

        if (t < EveningEnd)
        {
            return Lerp(1f, NightCrowd, (float)((t - EveningStart) / (EveningEnd - EveningStart)));
        }

        return NightCrowd;
    }

    /// <summary>Kolik lidí zůstane venku v hlubké noci.</summary>
    private const float NightCrowd = 0.18f;

    /// <summary>Je zrovna noc? (Pro ty, komu stačí ano/ne — třeba pro postávání.)</summary>
    public static bool IsNight(double timeOfDay01)
    {
        double t = Wrap(timeOfDay01);
        return t < MorningStart || t >= EveningEnd;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);

    /// <summary>Čas se může přetočit přes půlnoc i přijít záporný; tady se srovná.</summary>
    private static double Wrap(double t)
    {
        t %= 1.0;
        return t < 0 ? t + 1.0 : t;
    }
}
