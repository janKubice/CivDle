using CivDle.Core.Sim;
using Xunit;
using Xunit.Abstractions;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Šablona jako text ke sdílení.
///
/// <para>Dvě věci, na kterých to stojí: kolečko <b>šablona → text → šablona</b>
/// musí dát tutéž šablonu, a <b>poškozený kód nesmí shodit hru</b>. Do schránky
/// se dostane leccos a hráč tam vloží, co má zrovna zkopírované.</para>
/// </summary>
public class TemplateCodeTests
{
    private readonly ITestOutputHelper _out;

    public TemplateCodeTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void ATemplateSurvivesTheRoundTrip()
    {
        var original = new BuildTemplate(
            "Můj blok",
            new[]
            {
                new TemplatePart("house", 0, 0),
                new TemplatePart("granary", 2, 1),
                new TemplatePart("house", 4, 0),
            },
            new[] { (1, 0), (1, 1), (1, 2) });

        string code = TemplateCode.Write(original);
        _out.WriteLine($"{code.Length} znaků: {code[..Math.Min(60, code.Length)]}…");

        Assert.True(TemplateCode.TryRead(code, out var back));
        Assert.Equal(original.Name, back.Name);
        Assert.Equal(original.Buildings, back.Buildings);
        Assert.Equal(original.Roads, back.Roads);
    }

    [Fact]
    public void AnEmptyTemplateWorksToo()
    {
        string code = TemplateCode.Write(BuildTemplate.Empty);

        Assert.True(TemplateCode.TryRead(code, out var back));
        Assert.True(back.IsEmpty);
    }

    [Fact]
    public void DiacriticsInTheNameSurvive()
    {
        var original = new BuildTemplate(
            "Čtvrť u řeky — příliš žluťoučký kůň",
            new[] { new TemplatePart("house", 0, 0) },
            Array.Empty<(int, int)>());

        Assert.True(TemplateCode.TryRead(TemplateCode.Write(original), out var back));
        Assert.Equal(original.Name, back.Name);
    }

    [Fact]
    public void TheCodeIsShorterThanTheRawData()
    {
        // Padesát budov je bez komprese zeď textu, kterou chat zalomí.
        var parts = Enumerable.Range(0, 50)
            .Select(i => new TemplatePart("house", i % 10, i / 10))
            .ToList();
        var template = new BuildTemplate("velký blok", parts, Array.Empty<(int, int)>());

        string code = TemplateCode.Write(template);
        int rawGuess = parts.Count * "house".Length * 2;

        _out.WriteLine($"{parts.Count} budov → {code.Length} znaků");
        Assert.True(code.Length < rawGuess, $"kód má {code.Length} znaků, syrově by to bylo kolem {rawGuess}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("tohle není kód")]
    [InlineData("CIVD1:")]
    [InlineData("CIVD1:tohle-není-base64!!!")]
    [InlineData("CIVD1:aGVsbG8gd29ybGQ=")]     // platný base64, ale není to gzip
    [InlineData("CIVD9:aGVsbG8=")]             // budoucí verze formátu
    public void GarbageIsRefusedInsteadOfCrashing(string? code)
    {
        Assert.False(TemplateCode.TryRead(code, out var template));
        Assert.True(template.IsEmpty);
    }

    [Fact]
    public void ATruncatedCodeIsRefused()
    {
        string code = TemplateCode.Write(new BuildTemplate(
            "blok", new[] { new TemplatePart("house", 0, 0) }, Array.Empty<(int, int)>()));

        Assert.False(TemplateCode.TryRead(code[..(code.Length / 2)], out _));
    }

    [Fact]
    public void AnAbsurdlyLongCodeIsRefusedWithoutReadingIt()
    {
        // Ochrana proti tomu, že hráč vloží do pole celý soubor.
        string code = TemplateCode.Prefix + new string('A', TemplateCode.MaxCodeLength + 1);

        Assert.False(TemplateCode.TryRead(code, out _));
    }

    [Fact]
    public void WhitespaceAroundTheCodeIsForgiven()
    {
        // Kopírování z chatu skoro vždycky přinese mezeru nebo nový řádek.
        string code = TemplateCode.Write(new BuildTemplate(
            "blok", new[] { new TemplatePart("house", 1, 2) }, Array.Empty<(int, int)>()));

        Assert.True(TemplateCode.TryRead($"\n  {code}  \n", out var back));
        Assert.Single(back.Buildings);
    }
}
