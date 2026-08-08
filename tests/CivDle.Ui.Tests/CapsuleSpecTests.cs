using CivDle.Capture;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Rozměry podkladů do obchodu.
///
/// <para>Steamworks soubor se špatnou velikostí <b>nepřijme</b> — a pozná se to
/// až při nahrávání, tedy ve chvíli, kdy grafika dávno vznikla. Rozměry byly
/// navíc dlouho poloviční (header 460×215 místo dnešních 920×430), takže se
/// hlídají testem, ne pozorností.</para>
///
/// <para>Podklad musí mít <b>přesně</b> rozměr cílové kapsle: dopočítávat ho
/// zvětšením v kompozitoru znamená rozmazané domy tam, kde je jich nejvíc vidět.</para>
/// </summary>
public sealed class CapsuleSpecTests
{
    /// <summary>Co Steamworks u CivDle vyžaduje (stránka Graphical Assets + knihovna).</summary>
    public static TheoryData<string, int, int> Required => new()
    {
        { "bg-header-920x430", 920, 430 },            // Header Capsule
        { "bg-small-462x174", 462, 174 },             // Small Capsule
        { "bg-main-1232x706", 1232, 706 },            // Main Capsule
        { "bg-vertical-748x896", 748, 896 },          // Vertical Capsule
        { "bg-library-capsule-600x900", 600, 900 },   // Library Capsule
        { "bg-library-hero-3840x1240", 3840, 1240 },  // Library Hero
        { "bg-page-1438x810", 1438, 810 },            // Page Background
    };

    [Theory]
    [MemberData(nameof(Required))]
    public void EveryRequiredBackdropHasTheExactSize(string fileName, int width, int height)
    {
        var spec = Assert.Single(CapsuleSpec.All, s => s.FileName == fileName);

        Assert.Equal(width, spec.Width);
        Assert.Equal(height, spec.Height);
    }

    [Fact]
    public void FileNamesCarryTheirSize()
    {
        // Jméno souboru je jediná věc, kterou má člověk před očima, když ve
        // Steamworks vybírá, co kam nahraje. Když v něm rozměr nesedí, splete se.
        foreach (var spec in CapsuleSpec.All)
        {
            Assert.EndsWith($"{spec.Width}x{spec.Height}", spec.FileName, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NoTwoBackdropsShareAName()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spec in CapsuleSpec.All)
        {
            Assert.True(names.Add(spec.FileName), $"dvakrát stejné jméno podkladu: {spec.FileName}");
        }
    }
}
