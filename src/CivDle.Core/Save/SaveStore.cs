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

    /// <summary>
    /// Druhý slot vedle tohohle (stejná složka, jiný soubor). Výzvy mají vlastní
    /// slot: rozehraná výzva nesmí přepsat hlavní město — dřív to udělala.
    /// </summary>
    public SaveStore Sibling(string fileName) =>
        new(Path.Combine(Path.GetDirectoryName(_filePath) ?? ".", fileName));

    /// <summary>Složka archivu měst (vedle savu).</summary>
    public string ArchiveDirectory => Path.Combine(Path.GetDirectoryName(_filePath) ?? ".", "archiv");

    /// <summary>
    /// Zkopíruje rozehranou hru do archivu. Původní save zůstane — archiv je
    /// pojistka, ne přesun: Nová hra+ pak hlavní slot přepíše, ale město první
    /// kapitoly se neztratí (endgame.md, C3).
    /// </summary>
    /// <param name="label">Čitelná část jména souboru (jméno města); datum se přidá.</param>
    /// <returns>Cesta k archivu, nebo <c>null</c>, když není co archivovat nebo zápis selhal.</returns>
    public string? TryArchive(string label, DateTime nowUtc)
    {
        if (!HasSave)
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(ArchiveDirectory);
            string safe = new string(label.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
            string name = $"{(safe.Length > 0 ? safe : "mesto")}-{nowUtc:yyyyMMdd-HHmmss}.civdle";
            string path = Path.Combine(ArchiveDirectory, name);
            File.Copy(_filePath, path, overwrite: true);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Archivovaná města, nejnovější první.</summary>
    public IReadOnlyList<string> ArchivedFiles()
    {
        if (!Directory.Exists(ArchiveDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(ArchiveDirectory, "*.civdle")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
    }

    /// <summary>
    /// Vrátí archivované město do hlavního slotu. Rozehraná hra se napřed sama
    /// archivuje — výměna nikdy nic nesmaže.
    /// </summary>
    public bool TryRestoreFromArchive(string archivePath, DateTime nowUtc)
    {
        if (!File.Exists(archivePath))
        {
            return false;
        }

        try
        {
            TryArchive("predchozi", nowUtc);
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(archivePath, _filePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

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
