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
    public void EverySatellite_HasASprite()
    {
        // Družice bez spritu by na orbitální obrazovce prostě nebyla vidět —
        // a hráč by za ni přitom zaplatil pozdní ekonomiku.
        var registered = RegisteredIds("orbit");
        var content = LoadContent();

        var missing = content.Orbit.Satellites
            .Where(s => !registered.Contains(s.Id))
            .Select(s => s.Id)
            .ToList();

        Assert.True(missing.Count == 0,
            $"Družice bez spritu (na dráze by nebyly vidět): {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryBuildStage_HasASprite()
    {
        // Fáze odkazuje sprite jménem. Překlep by znamenal div, který se
        // desítky minut kreslí jako prázdný obrys — a to nikdo nespojí s daty.
        var content = LoadContent();
        var registered = AllRegisteredIds();

        var missing = content.Buildings.All
            .SelectMany(b => b.Stages.Select(stage => (Building: b.Id, stage.Sprite)))
            .Where(pair => !registered.Contains(pair.Sprite))
            .Select(pair => $"{pair.Building} → {pair.Sprite}")
            .ToList();

        Assert.True(missing.Count == 0,
            $"Fáze stavby odkazují neexistující sprity: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryAttacker_HasASprite()
    {
        // Neviditelný útočník je nefér: hráč by viděl, jak mu ubývají budovy,
        // a neměl by na co střílet.
        var registered = RegisteredIds("attacker");
        var content = LoadContent();

        var missing = content.Frontier.Attackers
            .Where(a => !registered.Contains(a.Id))
            .Select(a => a.Id)
            .ToList();

        Assert.True(missing.Count == 0,
            $"Útočníci bez spritu (nebylo by je vidět): {string.Join(", ", missing)}");
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

    [Fact]
    public void EveryDecorationSpriteExists()
    {
        // Dekorace se spritem, který v knihovně není, tiše spadne zpátky na
        // barevný čtvereček. To je ta nejhorší varianta chyby: nic nespadne,
        // jen je v lese místo stromu zelená tečka — a nikdo to nespojí
        // s překlepem v datech.
        var registered = AllRegisteredIds();
        var content = LoadContent();

        var missing = content.Decorations
            .Where(d => d.HasSprite && !registered.Contains(d.Sprite!))
            .Select(d => $"{d.Id} → {d.Sprite}")
            .ToList();

        Assert.True(missing.Count == 0,
            $"Dekorace odkazují neexistující sprity: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryDistrictPropSpriteExists()
    {
        // Čtvrť s překlepem ve spritu by po zemi tiše nerozsypala nic a její
        // ulice by vypadala jako každá jiná — tedy přesně ten stav, kvůli
        // kterému drobnosti vznikly.
        var registered = AllRegisteredIds();
        var content = LoadContent();

        var missing = content.Districts.Types.All
            .Where(d => d.HasProps && !registered.Contains(d.Prop!))
            .Select(d => $"{d.Id} → {d.Prop}")
            .ToList();

        Assert.True(missing.Count == 0,
            $"Čtvrti odkazují neexistující sprity: {string.Join(", ", missing)}");
    }

    /// <summary>ID spritů zaregistrovaných v knihovně pro daný prefix.</summary>
    /// <summary>Všechna registrovaná ID i s předponou — fáze se odkazují celým jménem.</summary>
    private static HashSet<string> AllRegisteredIds()
    {
        string source = File.ReadAllText(SpriteLibrarySource());
        return Regex.Matches(source, "\"([a-z]+\\.[a-z_0-9]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

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
