using CivDle.Core.Content;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Ambientní kulisa je data, ne nahrávky — a jako u každého obsahu platí, že
/// nesmí být „mrtvá". Testuje se, že každý biom a každé počasí něco slyší
/// a že jevy počasí přebijí biom (v lese, kde se žene bouřka, je slyšet bouřka).
/// </summary>
public sealed class AmbienceTests
{
    [Fact]
    public void EveryBiome_HasSomeAmbience()
    {
        var content = TestData.LoadRealContent();
        Assert.NotEmpty(content.Ambience);

        for (int biome = 0; biome < content.Biomes.Count; biome++)
        {
            Assert.True(
                content.Ambience.Any(a => !a.IsWeatherBound && a.Matches(biome, -1)),
                $"Biom '{content.Biomes[biome].Id}' nemá žádnou kulisu — bylo by tam ticho.");
        }
    }

    [Fact]
    public void WeatherBoundAmbience_MatchesRegardlessOfBiome()
    {
        var content = TestData.LoadRealContent();
        var weatherBound = content.Ambience.Where(a => a.IsWeatherBound).ToList();
        Assert.NotEmpty(weatherBound);

        foreach (var def in weatherBound)
        {
            // Kulisa počasí nesmí být zároveň vázaná na biom, jinak by při stejném
            // jevu jinde na mapě zmizela.
            Assert.Empty(def.BiomeIndices);
            Assert.True(def.Matches(0, def.WeatherIndices[0]));
            Assert.False(def.Matches(0, -1), "Bez daného počasí kulisa hrát nemá.");
        }
    }

    [Fact]
    public void EveryWeather_IsAudible()
    {
        var content = TestData.LoadRealContent();
        for (int weather = 0; weather < content.Weather.Count; weather++)
        {
            bool covered = content.Ambience.Any(a => a.Matches(biomeIndex: 0, weatherIndex: weather));
            Assert.True(covered, $"Počasí '{content.Weather[weather].Id}' nemá co znít.");
        }
    }

    [Fact]
    public void MissingFile_IsNotAnError()
    {
        // Kulisa je volitelný obsah — bez souboru hraje jen hudba a hra běží dál.
        var content = TestContent.Build();
        Assert.Empty(content.Ambience);
    }
}
