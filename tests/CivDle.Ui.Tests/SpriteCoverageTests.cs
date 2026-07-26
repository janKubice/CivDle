using System.Text.RegularExpressions;
using CivDle.Core.Content;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Každá budova, surovina a typ zóny musí mít svůj sprite. Bez toho se na mapě
/// kreslí jen barevný čtvereček — hráč pak nepozná huť od bytovky.
///
/// <para>Sprity se generují kódem, takže se nedají načíst bez grafického zařízení;
/// test proto kontroluje REGISTRACI v <c>SpriteLibrary</c> proti obsahu. Je to
/// levné a chytí to přesně ten případ, který nastával: nová budova v JSON, na
/// kterou se zapomnělo nakreslit model.</para>
/// </summary>
public sealed class SpriteCoverageTests
{
    [Fact]
    public void EveryBuilding_HasItsOwnSprite()
    {
        var registered = RegisteredIds("building");
        var content = LoadContent();

        var missing = content.Buildings.All
            .Where(b => !registered.Contains(b.Id))
            .Select(b => b.Id)
            .ToList();

        Assert.True(missing.Count == 0,
            $"Budovy bez modelu (kreslí se jen čtvereček): {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryResource_HasAnIcon()
    {
        var registered = RegisteredIds("icon");
        var content = LoadContent();

        var missing = content.Resources.All
            .Where(r => !registered.Contains(r.Id))
            .Select(r => r.Id)
            .ToList();

        Assert.True(missing.Count == 0,
            $"Suroviny bez ikony (v HUD zůstane barevný čtvereček): {string.Join(", ", missing)}");
    }

    /// <summary>ID spritů zaregistrovaných v knihovně pro daný prefix.</summary>
    private static HashSet<string> RegisteredIds(string prefix)
    {
        string source = File.ReadAllText(SpriteLibrarySource());
        return Regex.Matches(source, $"\"{prefix}\\.([a-z_0-9]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Najde zdroják knihovny — test běží z bin/, repozitář je nad ním.</summary>
    private static string SpriteLibrarySource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "src", "CivDle", "Rendering", "Sprites", "SpriteLibrary.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Nenalezen SpriteLibrary.cs — test běží mimo repozitář?");
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
