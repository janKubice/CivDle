using CivDle;

// Vstupní bod hry. Chyby (typicky rozbitá herní data — loader je fail-fast)
// se zapíšou do crash.log vedle exe, protože Windows build nemá konzoli.
try
{
    using var game = new CivDleGame();
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
