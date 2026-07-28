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

    using var game = new CivDleGame(captureDirectory, capsuleDirectory);
    game.Run();
    return 0;
}
catch (Exception ex)
{
    try
    {
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), ex.ToString());
    }
    catch (IOException)
    {
        // Nezapisovatelný disk — aspoň stderr níže.
    }

    Console.Error.WriteLine(ex);
    return 1;
}
