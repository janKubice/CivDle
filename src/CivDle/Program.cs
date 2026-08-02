using CivDle;

// Vstupní bod hry. Chyby (typicky rozbitá herní data — loader je fail-fast)
// se zapíšou do crash.log vedle exe, protože Windows build nemá konzoli.
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

    bool smoke = args.Contains("--smoke");
    bool perf = args.Contains("--perf");

    using var game = new CivDleGame(captureDirectory, capsuleDirectory, smoke, perf);
    game.Run();
    return 0;
}
catch (Exception ex)
{
    // Windows build je WinExe (bez konzole) — bez tohohle by chyba zmizela beze
    // stopy a hra by se z pohledu hráče „prostě nespustila".
    StartupConsole.AttachToParentIfPossible();

    string crashLog = Path.Combine(AppContext.BaseDirectory, "crash.log");
    try
    {
        File.WriteAllText(crashLog, ex.ToString());
    }
    catch (Exception logFailure) when (logFailure is IOException or UnauthorizedAccessException)
    {
        crashLog = string.Empty; // nezapsalo se; níž se na něj neodkazuj
    }

    // Napřed jedna věta, o co jde, teprve pak celý výpis — hráč (ani vývojář
    // ve spěchu) nečte stack trace odshora.
    Console.Error.WriteLine();
    Console.Error.WriteLine($"CivDle se nespustil: {ex.Message}");

    // U známých pádů (ovladače, blokace od Windows, rozbitá data) rovnou i to,
    // co s tím dělat — samotná hláška od systému hráči nepomůže.
    if (StartupDiagnosis.HintFor(ex) is { } hint)
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
    return 1;
}
