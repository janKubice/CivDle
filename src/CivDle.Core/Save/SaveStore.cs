using CivDle.Core.Content;
using CivDle.Core.Sim;

namespace CivDle.Core.Save;

/// <summary>Načtená uložená hra: simulace připravená k hraní + metadata pro HUD.</summary>
public sealed record LoadedGame(Simulation Simulation, SaveMetadata Metadata);

/// <summary>
/// Souborové úložiště uložené hry (MVP: jeden slot). Zapisuje atomicky
/// (tmp + přesun), aby pád při ukládání nezničil předchozí save. Chyby
/// nepropouští jako pád hry — vrací false/null a detail dá volajícímu.
/// </summary>
public sealed class SaveStore
{
    private readonly string _filePath;
    private readonly SaveGameSerializer _serializer = new();

    /// <param name="filePath">Plná cesta k souboru savu (typicky v profilu uživatele).</param>
    public SaveStore(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>Existuje uložená hra? (Řídí tlačítko „Pokračovat" v menu.)</summary>
    public bool HasSave => File.Exists(_filePath);

    /// <summary>
    /// Kam ukládat sdílitelné obrázky — vedle savu, ve složce profilu.
    ///
    /// <para>Bydlí to tady, protože je to jediné místo, které ví, kde má hra
    /// právo zapisovat; vedle exe ho mít nemusí.</para>
    /// </summary>
    public string ShareDirectory =>
        Path.Combine(Path.GetDirectoryName(_filePath) ?? ".", "obrazky");

    /// <summary>Kam se ukládají časosběry — vedle savu, ve složce profilu.</summary>
    public TimelapseStore Timelapses =>
        _timelapses ??= new TimelapseStore(Path.Combine(Path.GetDirectoryName(_filePath) ?? ".", "casosbery"));

    private TimelapseStore? _timelapses;

    /// <summary>Uloží hru; false = zápis selhal (plný disk, práva…).</summary>
    public bool TrySave(Simulation simulation, SaveMetadata metadata)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _filePath + ".tmp";
            using (var stream = File.Create(tempPath))
            {
                _serializer.Write(stream, simulation, metadata);
            }

            File.Move(tempPath, _filePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Načte uloženou hru; <c>null</c> = save chybí nebo nejde přečíst
    /// (<paramref name="error"/> nese detail pro log/diagnostiku).
    /// </summary>
    public LoadedGame? TryLoad(GameContent content, out string? error)
    {
        error = null;
        if (!HasSave)
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(_filePath);
            var (simulation, metadata) = _serializer.Read(stream, content);
            return new LoadedGame(simulation, metadata);
        }
        catch (Exception ex) when (ex is SaveLoadException or IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return null;
        }
    }
}
