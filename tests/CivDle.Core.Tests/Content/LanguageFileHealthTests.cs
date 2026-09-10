using System.Text.Json;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Jsou jazykové soubory na disku vůbec zdravé?
///
/// <para>Proč to nestačí nechat na loaderu: loader spadne jen na rozbitém
/// <b>JSONu</b>. Jenže tyhle soubory se slučují mezi větvemi po řádcích a git
/// o JSONu nic neví — když se dva bloky slepí, vzniknou dvě věci. Buď chybí
/// čárka (to loader chytí), <b>nebo se klíč objeví dvakrát</b>. A duplicitu
/// <see cref="JsonSerializer"/> mlčky spolkne: platí poslední. Soubor se načte,
/// hra běží, a nikdo se nedozví, že se dvě větve slepily špatně a jedna půlka
/// překladů přepsala druhou.</para>
///
/// <para>Přesně to se stalo: sloučení dvou větví přilepilo na konec všech pěti
/// souborů devatenáct už existujících řádků a k tomu ukouslo čárku. Hra se
/// z hlavní větve nedala spustit vůbec.</para>
/// </summary>
public class LanguageFileHealthTests
{
    [Theory]
    [InlineData("cs")]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("pl")]
    [InlineData("es")]
    public void TheFileIsValidJson(string language)
    {
        // Rozbitý JSON hlásí loader taky, ale až v půlce testů a nesrozumitelně
        // — tady je vidět rovnou který soubor a kde.
        using var stream = File.OpenRead(PathFor(language));

        var exception = Record.Exception(() => JsonDocument.Parse(stream));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("pl")]
    [InlineData("es")]
    public void NoKeyAppearsTwice(string language)
    {
        // Tohle je ta tichá půlka. Duplicitní klíč projde parserem i loaderem
        // a projeví se jen tím, že se překlad „vrátil" na starou verzi.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new List<string>();

        using var document = JsonDocument.Parse(File.ReadAllBytes(PathFor(language)));
        foreach (var property in document.RootElement.GetProperty("strings").EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                duplicates.Add(property.Name);
            }
        }

        Assert.True(
            duplicates.Count == 0,
            $"{language}.json má klíč dvakrát: {string.Join(", ", duplicates.Take(10))}");
    }

    private static string PathFor(string language) =>
        Path.Combine(TestData.RealDataDirectory, "lang", $"{language}.json");
}
