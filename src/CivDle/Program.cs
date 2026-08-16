using CivDle;
using CivDle.Core.Content;
using CivDle.Core.Content.Mods;

// Vstupní bod hry. Když start selže, musí se to hráč dozvědět — viz katalog
// příčin v StartupDiagnosis a tři cesty ven: okno (Steam), konzole (terminál)
// a crash.log (když si o něj někdo řekne).

// Automatické režimy nesmí NIKDY vyskočit modální okno: v CI by na něj nikdo
// neklikl a běh by visel až do timeoutu. Zjišťuje se to před try, aby platilo
// i v případě, že spadne rovnou konstruktor hry.
bool automated = args.Contains("--smoke")
    || args.Contains("--perf")
    || args.Contains("--capture")
    || args.Contains("--capsules")
    || args.Contains("--trailer");

try
{
    // --capture <složka>: nafotí sadu snímků do obchodu a skončí. Běžný start
    // hry se tím nemění — bez přepínače se rovnou otevře menu.
    string? captureDirectory = null;
    int captureFlag = Array.IndexOf(args, "--capture");
    if (captureFlag >= 0 && captureFlag + 1 < args.Length)
    {
        captureDirectory = args[captureFlag + 1];
    }

    string? capsuleDirectory = null;
    int capsuleFlag = Array.IndexOf(args, "--capsules");
    if (capsuleFlag >= 0 && capsuleFlag + 1 < args.Length)
    {
        capsuleDirectory = args[capsuleFlag + 1];
    }

    // --trailer <složka> [--nahled]: natočí záběry do traileru a skončí.
    string? trailerDirectory = null;
    int trailerFlag = Array.IndexOf(args, "--trailer");
    if (trailerFlag >= 0 && trailerFlag + 1 < args.Length)
    {
        trailerDirectory = args[trailerFlag + 1];
    }

    bool trailerPreview = args.Contains("--nahled");
    bool smoke = args.Contains("--smoke");
    bool perf = args.Contains("--perf");

    // Obsah se načítá TADY, ne až v LoadContent hry. Rozdíl je vidět jen když
    // jsou data rozbitá: dřív se chyba objevila ve chvíli, kdy okno už
    // existovalo, chybový dialog mu vzal fokus a MonoGame spadl podruhé
    // v OnDeactivate — hráč pak místo české hlášky viděl NullReferenceException.
    // Bez okna se má co vypsat a nemá co spadnout.
    //
    // Data leží vedle binárky — funguje pro `dotnet run` i pro publish jedním exe.
    // Mody bydlí ve vlastní složce vedle nich: kdyby přepisovaly data/, každá
    // aktualizace hry by je smazala a dva mody by se nedaly použít naráz.
    // Demo mody nenačítá vůbec. Není to jen skrytá obrazovka: ukázka má
    // ukazovat hru tak, jak ji autor postavil, a mod by do ní mohl přinést
    // cokoli — včetně obsahu, který má být až v plné verzi.
    var mods = Edition.IsDemo
        ? Array.Empty<ModPackage>()
        : ModCatalog.Discover(Path.Combine(AppContext.BaseDirectory, "mods"));

    var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"), mods);
    if (Edition.IsDemo)
    {
        content.EnableDemoEdition();
        Console.WriteLine("edice: DEMO");
    }

    foreach (var mod in mods)
    {
        Console.WriteLine($"mod: {mod.Name} {mod.Version} ({mod.Id})");
    }

    using var game = new CivDleGame(
        content, captureDirectory, capsuleDirectory, trailerDirectory, trailerPreview, smoke, perf);
    game.Run();
    return 0;
}
catch (Exception ex)
{
    // Windows build je WinExe (bez konzole) — bez tohohle by chyba zmizela beze
    // stopy a hra by se z pohledu hráče „prostě nespustila".
    StartupConsole.AttachToParentIfPossible();

    string summary = $"CivDle se nespustil: {ex.Message}";
    string? hint = StartupDiagnosis.HintFor(ex);
    string crashLog = WriteCrashLog(ex);

    // Napřed jedna věta, o co jde, teprve pak celý výpis — hráč (ani vývojář
    // ve spěchu) nečte stack trace odshora.
    Console.Error.WriteLine();
    Console.Error.WriteLine(summary);

    // U známých pádů (ovladače, blokace od Windows, rozbitá data) rovnou i to,
    // co s tím dělat — samotná hláška od systému hráči nepomůže.
    if (hint is not null)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine(hint);
    }

    if (crashLog.Length > 0)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Podrobnosti: {crashLog}");
    }

    Console.Error.WriteLine();
    Console.Error.WriteLine(ex);

    // Hráč ze Steamu konzoli nevidí. Bez tohohle okna mu hra jen tiše nenaběhne
    // — a místo aktualizace ovladačů následuje vrácení peněz.
    if (!automated)
    {
        StartupAlert.Show(summary, hint, crashLog);
    }

    return 1;
}

// Výpis do profilu uživatele, ne vedle exe: složka hry ve Steamu nemusí být
// zapisovatelná a hráč do ní stejně nehledá. Vedle exe zůstává jako záloha.
static string WriteCrashLog(Exception ex)
{
    foreach (string directory in new[] { CivDleGame.GetProfileDirectory(), AppContext.BaseDirectory })
    {
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "crash.log");
            File.WriteAllText(path, ex.ToString());
            return path;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // Zkus další místo; nemožnost zapsat log nesmí přebít hlášení chyby.
        }
    }

    return string.Empty;
}
