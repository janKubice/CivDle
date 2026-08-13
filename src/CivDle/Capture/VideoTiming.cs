using CivDle.Core.Sim;

namespace CivDle.Capture;

/// <summary>
/// Kolik snímků má video a kdy se v něm má odtikat simulace.
///
/// <para>Proč vlastní typ: renderování videa běží mimo reálný čas — jeden
/// snímek ve 4K bez LOD trvá klidně půl vteřiny. Nesmí se tedy odvozovat
/// z toho, jak dlouho snímek doopravdy trval, ale z <b>pevného kroku</b>.
/// Jinak by se výsledné video zrychlovalo a zpomalovalo podle toho, jak
/// zrovna stíhal počítač.</para>
///
/// <para>Tiky se rozpočítávají celočíselně z pořadí snímku, ne přičítáním
/// zlomků. Přičítání by po pár tisících snímcích ujelo o celý tik a video by
/// se rozešlo se zvukem i s hudbou.</para>
/// </summary>
public static class VideoTiming
{
    /// <summary>Snímková frekvence výsledného videa.</summary>
    public const int Fps = 60;

    /// <summary>Kolik snímků má záběr dané délky. Vždy aspoň jeden.</summary>
    public static int FrameCount(double durationSeconds) =>
        Math.Max(1, (int)Math.Round(Math.Max(0, durationSeconds) * Fps));

    /// <summary>Čas snímku v sekundách od začátku záběru.</summary>
    public static double TimeOfFrame(int frameIndex) => frameIndex / (double)Fps;

    /// <summary>
    /// Kolik tiků simulace se má odtikat <b>před</b> daným snímkem.
    ///
    /// <para>Počítá se jako rozdíl dvou celočíselných součtů, takže se chyba
    /// nikdy nekumuluje: po n snímcích je odtikáno přesně tolik tiků, kolik
    /// odpovídá n/60 sekundy.</para>
    /// </summary>
    public static int TicksBeforeFrame(int frameIndex)
    {
        if (frameIndex <= 0)
        {
            return 0;
        }

        return TotalTicksBy(frameIndex) - TotalTicksBy(frameIndex - 1);
    }

    /// <summary>Kolik tiků má být odtikáno, když je hotovo <paramref name="frames"/> snímků.</summary>
    public static int TotalTicksBy(int frames) =>
        (int)(Math.Max(0, frames) * Simulation.TicksPerSecond / Fps);

    /// <summary>
    /// Příkaz, kterým se ze sekvence udělá video. Hra ho jen vypíše — kódování
    /// videa by znamenalo přibalit další knihovnu a tím porušit „no balast".
    /// Takhle si navíc autor sám zvolí kodek a kvalitu.
    /// </summary>
    public static string FfmpegCommand(string directory, string fileName = "civdle.mp4") =>
        $"ffmpeg -framerate {Fps} -i \"{Path.Combine(directory, "frame-%06d.png")}\" "
        + $"-c:v libx264 -preset slow -crf 16 -pix_fmt yuv420p \"{Path.Combine(directory, fileName)}\"";
}
