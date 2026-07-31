using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Jak se z prosperity stává barva. Testuje se to, co by v obraze bylo vidět
/// jako chyba: bohatá čtvrť musí být světlejší než chudá, budova nesmí nikdy
/// zčernat ani prosvítat, a prahy pro ozdoby a šmouhy se nesmí překrývat.
/// </summary>
public sealed class ProsperityLookTests
{
    private static float Luminance(Color color) => (color.R + color.G + color.B) / 3f;

    [Fact]
    public void ProsperityMakesTheCityBrighter()
    {
        Assert.True(Luminance(ProsperityLook.Tint(1.0)) > Luminance(ProsperityLook.Tint(0.5)));
        Assert.True(Luminance(ProsperityLook.Tint(0.5)) > Luminance(ProsperityLook.Tint(0.0)));
    }

    [Fact]
    public void MiseryNeverTurnsBuildingsIntoSilhouettes()
    {
        // Hra netrestá tvrdě ani v obraze — z nejhorší čtvrti nesmí být černá díra.
        var worst = ProsperityLook.Tint(0.0);

        Assert.True(Luminance(worst) > 60f, $"Nejchudší nádech je moc tmavý: {worst}.");
    }

    [Fact]
    public void BuildingsNeverBecomeTransparent()
    {
        // Alfa je tu snadná chyba: Color × float v MonoGame stáhne i průhlednost
        // a budova by prosvítala terénem.
        Assert.Equal(255, ProsperityLook.Tint(0.0).A);
        Assert.Equal(255, ProsperityLook.Tint(0.5).A);
        Assert.Equal(255, ProsperityLook.Tint(1.0).A);
    }

    [Fact]
    public void TintIsClampedOutsideTheRange()
    {
        Assert.Equal(ProsperityLook.Tint(0.0), ProsperityLook.Tint(-5.0));
        Assert.Equal(ProsperityLook.Tint(1.0), ProsperityLook.Tint(9.0));
    }

    [Fact]
    public void OrnamentsAndGrimeNeverAppearTogether()
    {
        // Dům s truhlíkem i šmouhou by byl protimluv.
        for (double p = 0; p <= 1.0001; p += 0.05)
        {
            Assert.False(ProsperityLook.HasOrnament(p) && ProsperityLook.HasGrime(p),
                $"Při prosperitě {p:F2} má dům zároveň ozdobu i špínu.");
        }
    }

    [Fact]
    public void OnlyThrivingHousesGetDecorated()
    {
        Assert.True(ProsperityLook.HasOrnament(1.0));
        Assert.False(ProsperityLook.HasOrnament(0.5));
    }

    [Fact]
    public void OnlyNeglectedHousesGetGrimy()
    {
        Assert.True(ProsperityLook.HasGrime(0.1));
        Assert.False(ProsperityLook.HasGrime(0.9));
    }

    [Fact]
    public void OrnamentColoursVaryButAreStable()
    {
        // Odvozené z indexu, ne z náhody — jinak by ozdoby mezi snímky blikaly.
        Assert.Equal(ProsperityLook.OrnamentColor(7), ProsperityLook.OrnamentColor(7));
        Assert.NotEqual(ProsperityLook.OrnamentColor(0), ProsperityLook.OrnamentColor(1));
    }

    [Fact]
    public void ModulateKeepsTheBuildingsOwnColour()
    {
        // Budovy bez spritu se kreslí vlastní barvou — nádech ji smí ztlumit,
        // ne přebarvit.
        var green = new Color(0, 200, 0);

        var lit = ProsperityLook.Modulate(green, ProsperityLook.Tint(1.0));
        var dim = ProsperityLook.Modulate(green, ProsperityLook.Tint(0.0));

        Assert.Equal(0, lit.R);
        Assert.True(lit.G > dim.G);
        Assert.True(dim.G > 0);
    }
}
