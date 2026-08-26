using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Jedna hotová věta kroniky — klíč textu a čísla, která se do něj doplní.
///
/// <para>Čísla jsou syrová schválně: formátování („12 tis.", „2 h 15 min")
/// závisí na jazyce a na tom, kde se věta ukáže, a to je věc UI, ne simulace.
/// </para>
/// </summary>
/// <param name="TextKey">Lokalizační klíč věty.</param>
/// <param name="Value">Hlavní číslo věty (obyvatelé, budovy, sídla, procenta).</param>
/// <param name="Tick">Ke kterému okamžiku běhu se věta vztahuje.</param>
/// <param name="EraIndex">Éra, o které věta mluví; −1 = o žádné.</param>
public readonly record struct ChronicleLine(string TextKey, long Value, long Tick, int EraIndex = -1)
{
    /// <summary>Kolik sekund herního času od začátku běhu.</summary>
    public double Seconds => Tick / (double)Simulation.TicksPerSecond;
}

/// <summary>
/// Sepisovatel kroniky: z časosběru udělá pár vět, které se dají přečíst.
///
/// <para>Proč to hra potřebuje: časosběr je pole čísel a graf. Hráč po pěti
/// hodinách stavění nechce histogram, chce <b>větu</b> — „za dvě hodiny
/// z jedné chalupy vyrostlo město o čtyřiceti tisících". To je jediná podoba
/// dlouhé tiché práce, kterou jde ukázat někomu, kdo hru nehraje.</para>
///
/// <para>Vrstva: čistě odvozený pohled na <see cref="CityHistory"/>. Nic
/// nezapisuje, nic nemění a nemá vlastní stav — dá se zavolat kdykoli a dvakrát
/// po sobě dá totéž.</para>
/// </summary>
public static class ChronicleWriter
{
    /// <summary>
    /// Sepíše kroniku z časosběru. Věty jsou seřazené podle času, takže se
    /// stránka čte odshora dolů jako příběh, ne jako tabulka.
    /// </summary>
    /// <param name="history">Časosběr běhu.</param>
    /// <param name="catalog">Které věty se smějí objevit (z dat).</param>
    public static IReadOnlyList<ChronicleLine> Write(CityHistory history, ChronicleCatalog catalog)
    {
        var lines = new List<ChronicleLine>();
        if (!catalog.IsEnabled || history.Count == 0)
        {
            return lines;
        }

        for (int i = 0; i < catalog.Count; i++)
        {
            AddLinesFor(lines, history, catalog[i]);
        }

        // Řazení podle času, ne podle pořadí v datech: jinak by „dnes" mohlo
        // stát nad „a pak přišla éra páry".
        lines.Sort(static (a, b) => a.Tick.CompareTo(b.Tick));
        return lines;
    }

    private static void AddLinesFor(List<ChronicleLine> lines, CityHistory history, ChronicleTemplateDef template)
    {
        switch (template.Moment)
        {
            case ChronicleMoment.Founded:
                AddFounded(lines, history, template);
                break;
            case ChronicleMoment.Growth:
                AddBiggestGrowth(lines, history, template);
                break;
            case ChronicleMoment.EraChange:
                AddEraChanges(lines, history, template);
                break;
            case ChronicleMoment.Peak:
                AddPeak(lines, history, template);
                break;
            case ChronicleMoment.Settlements:
                AddFirstFrameWhere(lines, history, template,
                    static (frame, threshold) => frame.Settlements >= threshold,
                    static frame => frame.Settlements);
                break;
            case ChronicleMoment.Hardship:
                AddFirstFrameWhere(lines, history, template,
                    static (frame, threshold) => frame.Happiness <= threshold,
                    static frame => (long)Math.Round(frame.Happiness * 100));
                break;
            case ChronicleMoment.Pollution:
                AddFirstFrameWhere(lines, history, template,
                    static (frame, threshold) => frame.Pollution >= threshold,
                    static frame => (long)Math.Round(frame.Pollution * 100));
                break;
            case ChronicleMoment.Today:
                AddToday(lines, history, template);
                break;
        }
    }

    private static void AddFounded(List<ChronicleLine> lines, CityHistory history, ChronicleTemplateDef template)
    {
        var first = history.FrameAt(0);
        lines.Add(new ChronicleLine(template.TextKey, first.Buildings, first.Tick, first.EraIndex));
    }

    private static void AddToday(List<ChronicleLine> lines, CityHistory history, ChronicleTemplateDef template)
    {
        var last = history.FrameAt(history.Count - 1);
        lines.Add(new ChronicleLine(template.TextKey, last.Population, last.Tick, last.EraIndex));
    }

    /// <summary>
    /// Snímek, mezi kterým a předchozím přibylo nejvíc lidí.
    ///
    /// <para>Přírůstek, ne absolutní číslo: „nejvíc lidí" je vždycky poslední
    /// snímek a o běhu to neřekne nic. Zlom je ta chvíle, kdy se to rozjelo.
    /// </para>
    /// </summary>
    private static void AddBiggestGrowth(List<ChronicleLine> lines, CityHistory history, ChronicleTemplateDef template)
    {
        if (history.Count < 2)
        {
            return;
        }

        long best = 0;
        int bestIndex = -1;
        for (int i = 1; i < history.Count; i++)
        {
            long gained = history.FrameAt(i).Population - history.FrameAt(i - 1).Population;
            if (gained > best)
            {
                best = gained;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0)
        {
            var frame = history.FrameAt(bestIndex);
            lines.Add(new ChronicleLine(template.TextKey, best, frame.Tick, frame.EraIndex));
        }
    }

    private static void AddEraChanges(List<ChronicleLine> lines, CityHistory history, ChronicleTemplateDef template)
    {
        int previous = history.FrameAt(0).EraIndex;
        for (int i = 1; i < history.Count; i++)
        {
            var frame = history.FrameAt(i);
            if (frame.EraIndex == previous || frame.EraIndex < 0)
            {
                continue;
            }

            previous = frame.EraIndex;
            lines.Add(new ChronicleLine(template.TextKey, frame.Population, frame.Tick, frame.EraIndex));
        }
    }

    private static void AddPeak(List<ChronicleLine> lines, CityHistory history, ChronicleTemplateDef template)
    {
        int bestIndex = 0;
        for (int i = 1; i < history.Count; i++)
        {
            if (history.FrameAt(i).Population > history.FrameAt(bestIndex).Population)
            {
                bestIndex = i;
            }
        }

        var frame = history.FrameAt(bestIndex);
        lines.Add(new ChronicleLine(template.TextKey, frame.Population, frame.Tick, frame.EraIndex));
    }

    /// <summary>
    /// První snímek, který splní podmínku. Jen první — kdyby kronika zapsala
    /// každý, byla by z ní tabulka měření, ne příběh.
    /// </summary>
    private static void AddFirstFrameWhere(
        List<ChronicleLine> lines,
        CityHistory history,
        ChronicleTemplateDef template,
        Func<HistoryFrame, double, bool> matches,
        Func<HistoryFrame, long> value)
    {
        for (int i = 0; i < history.Count; i++)
        {
            var frame = history.FrameAt(i);
            if (matches(frame, template.Threshold))
            {
                lines.Add(new ChronicleLine(template.TextKey, value(frame), frame.Tick, frame.EraIndex));
                return;
            }
        }
    }
}
