using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Obyvatelé se jménem: občas se ozve někdo konkrétní, že si chce otevřít
/// živnost.
///
/// <para>Testuje se to, co z toho dělá moment, a ne další seznam úkolů: prosba
/// je vždycky jen jedna, pomoc opravdu postaví budovu, ta budova nese jméno
/// napořád — a když hráč nepomůže, nic se nestane.</para>
/// </summary>
public class CitizenTests
{
    private const int Wood = 0;

    private static CitizenCatalog Catalog(double gapSeconds = 10, double duration = 30)
    {
        var requests = new[]
        {
            new CitizenRequestDef("mill", 0, new[] { new ResourceAmount(Wood, 20) }, duration),
        };

        return new CitizenCatalog(
            new[] { "Marek", "Anna" },
            new[] { "Kovář", "Dvořák" },
            new DefRegistry<CitizenRequestDef>(requests, r => r.Id, "prosba obyvatele"),
            gapSeconds);
    }

    private static Simulation NewSim(CitizenCatalog? catalog = null, double startingWood = 500)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("plain") };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(1, 1, 1), startingWood, BaseStorage: 1_000_000),
        };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
        };

        // Vzestup hned od začátku — jeden z testů ověřuje, že nový svět zakladatele
        // předchozího světa nedědí.
        var prestige = new PrestigeConfig(
            new GoalCondition(MetricKind.Population, -1, 1), MetricKind.Population, -1, 5);

        var content = TestContent.Build(
            biomes, 1, resources, gameplay: gameplay, prestige: prestige,
            citizens: catalog ?? Catalog());
        return new Simulation(content, new UniformTerrain(1));
    }

    private static void Tick(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void SomeoneEventuallyAsks()
    {
        var sim = NewSim();
        Assert.False(sim.PendingCitizenRequest.IsActive);

        Tick(sim, 20);

        Assert.True(sim.PendingCitizenRequest.IsActive);
        Assert.NotEmpty(sim.PendingCitizenName);
    }

    [Fact]
    public void OnlyOnePersonAsksAtATime()
    {
        // Prosba je moment, ne nástěnka. Tři naráz by z toho udělaly seznam úkolů.
        var sim = NewSim();
        Tick(sim, 200);

        Assert.True(sim.PendingCitizenRequest.IsActive);
        // Stav je jediná struktura — víc než jedna prosba prostě neexistuje.
        Assert.NotNull(sim.PendingCitizenDef);
    }

    [Fact]
    public void HelpingFoundsTheirTradeAndCharges()
    {
        var sim = NewSim();
        Tick(sim, 20);
        Assert.True(sim.CanHelpCitizen());

        double before = sim.GetResource(Wood);
        int buildingsBefore = sim.Buildings.Length;

        Assert.True(sim.TryHelpCitizen());

        Assert.Equal(before - 20, sim.GetResource(Wood), 3);
        Assert.Equal(buildingsBefore + 1, sim.Buildings.Length);
        Assert.Equal(1, sim.FoundedByCitizens);
        Assert.False(sim.PendingCitizenRequest.IsActive);
    }

    [Fact]
    public void TheBuildingKeepsTheirName()
    {
        // Tenhle jeden řádek je celý smysl mechaniky — bez něj je to jen budova.
        var sim = NewSim();
        Tick(sim, 20);
        string name = sim.PendingCitizenName;

        Assert.True(sim.TryHelpCitizen());

        var founded = sim.Buildings[^1];
        Assert.Equal(name, sim.FounderOf(founded.X, founded.Y));
    }

    [Fact]
    public void WithoutTheMaterialsNothingHappens()
    {
        var sim = NewSim(startingWood: 0);
        Tick(sim, 20);

        Assert.True(sim.PendingCitizenRequest.IsActive);
        Assert.False(sim.CanHelpCitizen());
        Assert.False(sim.TryHelpCitizen());
        Assert.True(sim.PendingCitizenRequest.IsActive); // prosba nikam nezmizela
    }

    [Fact]
    public void GivingUpCostsTheCityNothing()
    {
        // Nesplněná prosba je smutná, ne trestná — hra netrestá nikde jinde taky.
        var sim = NewSim(Catalog(duration: 2));
        Tick(sim, 20);
        double before = sim.GetResource(Wood);

        Tick(sim, 40);

        Assert.Equal(before, sim.GetResource(Wood), 3);
        Assert.Equal(0, sim.FoundedByCitizens);
    }

    [Fact]
    public void AfterHelpingThereIsABreathBeforeTheNextOne()
    {
        var sim = NewSim(Catalog(gapSeconds: 60));
        Tick(sim, 20);
        Assert.True(sim.TryHelpCitizen());

        Tick(sim, 100); // míň než rozestup

        Assert.False(sim.PendingCitizenRequest.IsActive);
    }

    [Fact]
    public void AscendingClearsTheFoundersToo()
    {
        var sim = NewSim();
        Tick(sim, 20);
        Assert.True(sim.TryHelpCitizen());
        var founded = sim.Buildings[^1];
        Assert.NotEmpty(sim.FounderOf(founded.X, founded.Y));

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.Equal(string.Empty, sim.FounderOf(founded.X, founded.Y));
        Assert.False(sim.PendingCitizenRequest.IsActive);
    }

    [Fact]
    public void AnEmptyCatalogLeavesTheGameAsItWas()
    {
        var sim = NewSim(CitizenCatalog.Empty);

        Tick(sim, 300);

        Assert.False(sim.CitizensEnabled);
        Assert.False(sim.PendingCitizenRequest.IsActive);
    }
}
