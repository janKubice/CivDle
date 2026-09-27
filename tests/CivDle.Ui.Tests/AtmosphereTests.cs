using CivDle.Core.Content;
using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Atmosféra světů (svety-design.md 5.1). Nejdůležitější je regrese:
/// Domovina s profilem z dat musí svítit <b>přesně</b> jako dřív s konstantami
/// v kódu — první kapitola se nesmí změnit tím, že se její vzhled přestěhoval
/// do dat.
/// </summary>
public sealed class AtmosphereTests
{
    private static readonly string[] WorldIds = { "home", "dune", "frost", "archipelago", "forge", "gas_giant", "xeno" };

    [Fact]
    public void TheHomeworldLightsExactlyAsBefore()
    {
        var content = LoadContent();
        Assert.Equal("home", content.Atmosphere.Id);
        var seasons = new List<SeasonDef?> { null };
        seasons.AddRange(content.Seasons.Seasons);

        foreach (var season in seasons)
        {
            for (double t = 0; t < 1; t += 0.005)
            {
                Assert.Equal(
                    DayNightCycle.LightColor(t, content.Gameplay.DayNight, season),
                    DayNightCycle.LightColor(t, content.Gameplay.DayNight, season, content.Atmosphere));
            }
        }
    }

    [Fact]
    public void AWorldTintChangesTheLight()
    {
        var content = LoadContent();
        var forge = AtmosphereProfile.Home with { Tint = new RgbColor(255, 160, 120), TintStrength = 0.3 };

        var home = DayNightCycle.LightColor(0.5, content.Gameplay.DayNight, null);
        var tinted = DayNightCycle.LightColor(0.5, content.Gameplay.DayNight, null, forge);

        Assert.True(tinted.B < home.B, "rezavý nádech má ubrat modrou");
        Assert.Equal(home.R, tinted.R);
    }

    [Fact]
    public void EveryParticleKindHasAStyle()
    {
        foreach (string kind in AtmosphereProfile.ParticleKinds)
        {
            var (color, _, _) = AtmosphereLayer.Style(kind);
            Assert.Equal(kind == "none", color == Color.Transparent);
        }
    }

    [Fact]
    public void TheDataHasAnAtmosphereForEveryWorld()
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "atmospheres.json"));
        foreach (string world in WorldIds)
        {
            Assert.Contains($"\"id\": \"{world}\"", json);
        }
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
