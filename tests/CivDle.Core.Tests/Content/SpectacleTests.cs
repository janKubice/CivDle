using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Podívané megastruktur: div světa se stavěl desítky minut a pak jen stál.
///
/// <para>Testuje se to, co odděluje odměnu od nedodělku: megastruktury musí
/// nějakou podívanou mít, intervaly musí být v rozumném rozsahu (jinak by z toho
/// bylo blikání), a běžné domy nesmí blikat vůbec.</para>
/// </summary>
public class SpectacleTests
{
    [Fact]
    public void MegastructuresPutOnAShow()
    {
        // Aspoň polovina megastruktur má mít co ukázat — jinak je to ozdoba
        // dvou budov, ne vlastnost kategorie.
        var content = TestData.LoadRealContent();
        var mega = content.Buildings.All.Where(b => b.Category == "megastructure").ToList();

        Assert.True(mega.Count >= 5, "Herní data mají mít aspoň pět megastruktur.");
        Assert.True(
            mega.Count(b => b.HasSpectacle) * 2 >= mega.Count,
            $"Podívanou má jen {mega.Count(b => b.HasSpectacle)} z {mega.Count} megastruktur.");
    }

    [Fact]
    public void TheSpaceportLaunchesRockets()
    {
        var content = TestData.LoadRealContent();
        var spaceport = content.Buildings[content.Buildings.IndexOf("spaceport")];

        Assert.Equal(SpectacleEffect.RocketLaunch, spaceport.Spectacle?.Effect);
    }

    [Fact]
    public void ThereIsAParticleAccelerator()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Buildings.TryIndexOf("particle_accelerator", out int index));
        Assert.Equal(SpectacleEffect.ParticleBeam, content.Buildings[index].Spectacle?.Effect);
    }

    [Fact]
    public void OrdinaryBuildingsStayQuiet()
    {
        // Kdyby blikal každý dům, přestala by být podívaná zvláštní.
        var content = TestData.LoadRealContent();

        Assert.All(
            content.Buildings.All.Where(b => b.Category is "housing" or "production"),
            b => Assert.False(b.HasSpectacle, $"Budova '{b.Id}' by neměla nic předvádět."));
    }

    [Fact]
    public void IntervalsAreNeitherFrantikNorForgotten()
    {
        // Pod pár vteřin je to blikání, nad pár minut si toho nikdo nevšimne.
        var content = TestData.LoadRealContent();

        foreach (var building in content.Buildings.All.Where(b => b.HasSpectacle))
        {
            Assert.InRange(building.Spectacle!.IntervalSeconds, 3.0, 300.0);
        }
    }

    [Fact]
    public void NewMegastructuresAreLockedBehindScale()
    {
        // Urychlovač ani maják nesmí být k mání od začátku — je to odměna
        // za dotažené měřítko.
        //
        // Testuje se chování, ne příznak v datech. Dřív tu stálo
        // `Assert.False(def.Buildable)` a znamenalo to něco jiného, než se
        // myslelo: `buildable: false` je „jen jako cíl vylepšení", tedy
        // NIKDY — a protože na megastruktury nic nevylepšuje, nešly postavit
        // ani po dosažení stupně. Zamykání dělá stupeň měřítka, ne ten příznak.
        var content = TestData.LoadRealContent();
        var fresh = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));

        foreach (string id in new[] { "particle_accelerator", "fusion_beacon" })
        {
            int index = content.Buildings.IndexOf(id);

            Assert.False(fresh.IsBuildingBuildable(index), $"'{id}' se nemá dát postavit hned na začátku.");
            Assert.Contains(
                content.AscensionTiers.All,
                tier => tier.UnlockedBuildingIndices.Contains(index));
        }
    }

    [Fact]
    public void MegastructuresBecomeBuildableWhenTheScaleIsReached()
    {
        // Druhá půlka téhož: odemčení musí být k něčemu. Přesně tohle v datech
        // dlouho neplatilo — stupeň budovu „odemkl" a postavit se pořád nedala.
        var content = TestData.LoadRealContent();

        foreach (var tier in content.AscensionTiers.All)
        {
            foreach (int index in tier.UnlockedBuildingIndices)
            {
                var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
                sim.DebugGrantAscensionLevels(tier.Order);

                Assert.True(
                    sim.IsBuildingBuildable(index),
                    $"'{content.Buildings[index].Id}' odemyká stupeň '{tier.Id}', ale postavit se nedá.");
            }
        }
    }
}
