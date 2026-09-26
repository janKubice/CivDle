using CivDle.Core.Content.Mods;

namespace CivDle.Core.Content;

/// <summary>
/// Obsah všech světů galaxie: Domovina se načte při startu, kolonie až když
/// jsou potřeba (mapa galaxie, přepnutí, načtení savu s kolonií).
///
/// <para><b>Proč líně:</b> každý svět je samostatný <see cref="GameContent"/>
/// se svou validací. Načíst šest světů při každém startu by zdrželo i hráče,
/// který galaxii ještě nemá. Chybu v datech kolonie chytí test, který načte
/// všechny světy (<c>WorldContentTests</c>) — fail-fast při vývoji, ne až
/// u hráče.</para>
///
/// <para>Vrstva: content (OOP), bez stavu hry. Obsah světa je po načtení
/// neměnný, takže se smí sdílet mezi simulacemi.</para>
/// </summary>
public sealed class GalaxyContent
{
    private readonly string _dataDirectory;
    private readonly IReadOnlyList<ModPackage> _mods;
    private readonly Dictionary<string, GameContent> _worlds = new(StringComparer.Ordinal);

    /// <param name="dataDirectory">Složka <c>data/</c>.</param>
    /// <param name="mods">Mody (vrství se na každý svět stejně jako na Domovinu).</param>
    /// <param name="home">Už načtený obsah Domoviny.</param>
    public GalaxyContent(string dataDirectory, IReadOnlyList<ModPackage> mods, GameContent home)
    {
        _dataDirectory = dataDirectory;
        _mods = mods;
        Home = home;
        _worlds[WorldScope.HomeId] = home;
    }

    /// <summary>Galaxie jen s Domovinou (testy, nástroje, hra bez složky dat).</summary>
    public static GalaxyContent HomeOnly(GameContent home) => new(string.Empty, Array.Empty<ModPackage>(), home);

    /// <summary>Obsah Domoviny.</summary>
    public GameContent Home { get; }

    /// <summary>Světy galaxie (z obsahu Domoviny).</summary>
    public WorldCatalog Catalog => Home.Galaxy;

    /// <summary>
    /// Obsah světa; poprvé se načte ze složky dat.
    /// </summary>
    /// <exception cref="ContentLoadException">Data světa jsou chybná.</exception>
    public GameContent For(string worldId)
    {
        if (_worlds.TryGetValue(worldId, out var content))
        {
            return content;
        }

        if (Catalog.Find(worldId) is null)
        {
            throw new ContentLoadException(_dataDirectory, $"Svět '{worldId}' není v galaxii (worlds.json).");
        }

        content = new ContentLoader().LoadFrom(_dataDirectory, _mods, worldId);
        if (Home.IsDemo)
        {
            content.EnableDemoEdition(); // edice platí pro celou aplikaci, ne pro jeden svět
        }

        _worlds[worldId] = content;
        return content;
    }
}
