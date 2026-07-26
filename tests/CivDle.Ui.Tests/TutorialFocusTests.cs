using System.Text.RegularExpressions;
using CivDle.Core.Content;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Tlačítko „Ukaž mi" u kroku průvodce musí opravdu něco otevřít. Cíl je v datech
/// jako řetězec (<c>focus.target</c>), obsluha je ve <c>GameplayScreen.FocusOn</c> —
/// překlep v JSON by jinak vyrobil tlačítko, po kterém se nic nestane, a to je
/// horší než žádné tlačítko.
///
/// <para>Obsluha se nedá zavolat bez grafického zařízení, takže test kontroluje
/// obsluhované řetězce ve zdrojáku proti obsahu — levné a chytí přesně ten případ.</para>
/// </summary>
public sealed class TutorialFocusTests
{
    [Fact]
    public void EveryFocusTarget_IsHandledByTheScreen()
    {
        string source = File.ReadAllText(RepoFile("src", "CivDle", "Screens", "GameplayScreen.cs"));
        var handled = Regex.Matches(source, "focus\\.Target == \"([a-z_0-9]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var content = LoadContent();
        var unhandled = content.Tutorial
            .Where(step => step.Focus.Kind is FocusKind.Tool or FocusKind.Screen)
            .Select(step => step.Focus.Target)
            .Where(target => !handled.Contains(target))
            .Distinct()
            .ToList();

        Assert.True(unhandled.Count == 0,
            $"Kroky průvodce míří na neobsluhovaný cíl (tlačítko „Ukaž mi\" by nic neudělalo): {string.Join(", ", unhandled)}");
    }

    [Fact]
    public void EveryStep_TellsThePlayerWhatToDo()
    {
        var content = LoadContent();
        var language = content.Languages.All[0];

        var silent = content.Tutorial
            .Where(step => !language.Strings.TryGetValue(step.HintKey, out string? hint) || hint.Trim().Length < 20)
            .Select(step => step.Id)
            .ToList();

        Assert.True(silent.Count == 0,
            $"Kroky průvodce bez použitelné nápovědy „jak na to\": {string.Join(", ", silent)}");
    }

    private static string RepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Nenalezen '{Path.Combine(parts)}' — test běží mimo repozitář?");
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
