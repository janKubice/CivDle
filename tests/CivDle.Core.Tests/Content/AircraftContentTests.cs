using CivDle.Core.Content;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Létající kulisa (bod 42). Nad mapou se mají objevit balony a letadla —
/// stejně jako po vodě plují rybářské lodičky.
/// </summary>
public sealed class AircraftContentTests
{
    [Fact]
    public void RealContentHasAircraft()
    {
        var content = TestData.LoadRealContent();

        Assert.NotEmpty(content.Aircraft);
        Assert.Contains(content.Aircraft, a => a.Id == "balloon");
        Assert.Contains(content.Aircraft, a => a.Id == "airliner");
    }

    [Fact]
    public void EveryAircraftLaunchesFromSomewhere()
    {
        // Letoun bez domovské budovy by byl jen tapeta na obloze; s ní je to
        // odměna za postavené letiště.
        var content = TestData.LoadRealContent();

        foreach (var craft in content.Aircraft)
        {
            Assert.True(craft.NeedsHomeBuilding, $"letoun '{craft.Id}' nemá odkud vzlétnout");
            Assert.InRange(craft.HomeBuildingIndex, 0, content.Buildings.Count - 1);
        }
    }

    [Fact]
    public void AircraftAppearOnlyInLaterEras()
    {
        // Balon nad osadou z doby kamenné by byl vtip, ne kulisa.
        var content = TestData.LoadRealContent();

        foreach (var craft in content.Aircraft)
        {
            Assert.True(craft.MinEraOrder >= 3, $"letoun '{craft.Id}' létá už v éře {craft.MinEraOrder}");
            Assert.False(craft.FitsEra(0));
        }
    }

    [Fact]
    public void LaterAircraftFlyFasterAndHigher()
    {
        var content = TestData.LoadRealContent();
        var balloon = content.Aircraft.First(a => a.Id == "balloon");
        var airliner = content.Aircraft.First(a => a.Id == "airliner");

        Assert.True(airliner.Speed > balloon.Speed);
        Assert.True(airliner.Altitude > balloon.Altitude);
    }

    [Fact]
    public void UnknownHomeBuildingIsALoadError()
    {
        // Překlep v odkazu = letoun, který nikdy nevzlétne, a nikdo nepozná proč
        // (CLAUDE.md: fail-fast při načtení, ne tichá chyba za hodinu hraní).
        string directory = Path.Combine(AppContext.BaseDirectory, "tmp-aircraft", Guid.NewGuid().ToString("N"));
        CopyDirectory(TestData.RealDataDirectory, directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "vehicles.json"), """
                {
                  "schemaVersion": 1,
                  "vehicles": [],
                  "aircraft": [
                    { "id": "ghost", "color": "#FFFFFF", "speed": 40, "altitude": 20,
                      "minEra": 4, "home": "neexistujici_budova" }
                  ]
                }
                """);

            var error = Assert.Throws<ContentLoadException>(() => new ContentLoader().LoadFrom(directory));
            Assert.Contains("neexistujici_budova", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (string directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }
}
