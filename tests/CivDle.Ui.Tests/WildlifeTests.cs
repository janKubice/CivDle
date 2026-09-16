using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Rendering;
using CivDle.Rendering.Effects;
using Microsoft.Xna.Framework;
using Xunit;
using Xunit.Abstractions;

namespace CivDle.Ui.Tests;

/// <summary>
/// Zvěř mimo město.
///
/// <para>Krajina kolem města byla prázdná: pár teček, které se každá zvlášť
/// toulala náhodným směrem a nereagovaly na nic. Testuje se to, co z nich
/// dělá zvířata — že chodí pohromadě a že před lidmi utíkají.</para>
/// </summary>
public class WildlifeTests
{
    private readonly ITestOutputHelper _out;

    public WildlifeTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void AnimalsArriveAsAHerdNotOneByOne()
    {
        // Srnec sám uprostřed pláně je tečka; stádo je výjev.
        var (fauna, sim, camera) = Wild();

        Run(fauna, sim, camera, seconds: 6);

        _out.WriteLine($"zvířat: {fauna.CountForTests}");
        Assert.True(fauna.CountForTests > 1, "v divočině se objevilo jedno jediné zvíře");
    }

    [Fact]
    public void AHerdStaysTogetherInsteadOfScattering()
    {
        // Kdyby se každé zvíře toulalo po svém, byly by ze stáda do minuty
        // rozprchlé tečky — a přesně tak to vypadalo dřív.
        //
        // Měří se vzdálenost od VLASTNÍHO stáda, ne rozptyl všech zvířat
        // dohromady: v obraze je stád víc naráz a jejich společný rozptyl je
        // velikost výřezu, ne soudržnost skupiny.
        var (fauna, sim, camera) = Wild();
        Run(fauna, sim, camera, seconds: 6);

        Run(fauna, sim, camera, seconds: 45);

        var distances = fauna.HerdsForTests
            .Select(h => Vector2.Distance(h.Position, h.Anchor))
            .OrderBy(d => d)
            .ToList();

        // Posuzuje se tvar skupiny, ne jeden nejhorší kus. Zatoulaný opozdilec
        // ke stádu patří — rozpadlé stádo je něco jiného než stádo s opozdilcem,
        // a test, který hlídá maximum, ten rozdíl nepozná.
        float median = distances[distances.Count / 2];
        float ninety = distances[(int)(distances.Count * 0.9)];

        _out.WriteLine($"od svého stáda: medián {median:0} px, p90 {ninety:0} px ({distances.Count} zvířat)");
        Assert.True(median < TerrainRenderer.TileSize * 4, $"stádo se roztáhlo (medián {median:0} px)");
        Assert.True(ninety < TerrainRenderer.TileSize * 7, $"stádo se rozprchlo (p90 {ninety:0} px)");
    }

    [Fact]
    public void ShyAnimalsRunFromPeople()
    {
        // Reakce je to jediné, co na ambientní fauně opravdu vypadá živě.
        //
        // Počítají se zvířata v dosahu člověka, ne posun průměru přes celý
        // výřez: utíká jen ten, kdo je poblíž, a ve zbytku obrazu se pase dál
        // pár desítek dalších — v průměru by se ten útěk ztratil.
        var (fauna, sim, camera) = Wild();
        Run(fauna, sim, camera, seconds: 6);

        var shyOnes = fauna.CrittersForTests.Where(c => c.Shy).Select(c => c.Position).ToList();
        Assert.NotEmpty(shyOnes);

        var person = shyOnes[0];
        int before = CountWithin(fauna, person, Reach);

        fauna.People = new[] { person };
        Run(fauna, sim, camera, seconds: 3);
        int after = CountWithin(fauna, person, Reach);

        _out.WriteLine($"v dosahu člověka před {before}, po {after}");
        Assert.True(before > 0, "u vybraného místa nebylo na začátku žádné zvíře");
        Assert.True(after < before, $"zvěř zůstala člověku pod nohama ({before} → {after})");
    }

    [Fact]
    public void WithNobodyAroundTheyGrazeInPeace()
    {
        // Bez lidí se nesmí chovat jako vyplašená — pořád je to pastva.
        // Ptá se rovnou na to, co nás zajímá: utíká někdo? Počítat sousedy
        // kolem bodu by byla oklika, protože zvířata se kolem něj beztak
        // procházejí sem a tam.
        var (fauna, sim, camera) = Wild();
        Run(fauna, sim, camera, seconds: 6);

        fauna.People = null;
        Run(fauna, sim, camera, seconds: 5);

        Assert.Equal(0, fauna.FleeingForTests);
    }

    [Fact]
    public void APersonSetsTheHerdRunning()
    {
        var (fauna, sim, camera) = Wild();
        Run(fauna, sim, camera, seconds: 6);

        // Člověk musí stoupnout k PLACHÉMU druhu. Vzít prostě první zvíře v poli
        // znamenalo měřit, kdo se zrovna objevil první — a jakmile v datech
        // přibyla neplachá zvířata (motýl, medvěd), začal test padat.
        var shy = fauna.CrittersForTests.Where(c => c.Shy).Select(c => c.Position).ToList();
        Assert.NotEmpty(shy);

        fauna.People = new[] { shy[0] };
        Run(fauna, sim, camera, seconds: 1);

        _out.WriteLine($"utíká {fauna.FleeingForTests} z {fauna.CountForTests}");
        Assert.True(fauna.FleeingForTests > 0, "člověk prošel stádem a nikdo se nehnul");
    }

    /// <summary>Na jakou vzdálenost plaché zvíře člověka zaregistruje.</summary>
    private static readonly float Reach = TerrainRenderer.TileSize * 5f;

    /// <summary>Kolik zvířat je do dané vzdálenosti od bodu.</summary>
    private static int CountWithin(FaunaSystem fauna, Vector2 place, float radius) =>
        fauna.PositionsForTests.Count(p => Vector2.Distance(p, place) <= radius);

    /// <summary>Průměrná poloha zvířat.</summary>
    private static Vector2 Center(FaunaSystem fauna)
    {
        var positions = fauna.PositionsForTests.ToList();
        Assert.NotEmpty(positions);

        var sum = Vector2.Zero;
        foreach (var position in positions)
        {
            sum += position;
        }

        return sum / positions.Count;
    }

    /// <summary>Jak daleko od sebe zvířata jsou (největší vzdálenost od středu).</summary>
    private static float Spread(FaunaSystem fauna)
    {
        var center = Center(fauna);
        float worst = 0f;
        foreach (var position in fauna.PositionsForTests)
        {
            worst = MathF.Max(worst, Vector2.Distance(position, center));
        }

        return worst;
    }

    private static void Run(FaunaSystem fauna, Simulation sim, Camera2D camera, double seconds)
    {
        const float step = 1f / 30f;
        for (int i = 0; i < seconds / step; i++)
        {
            fauna.Update(step, camera, sim);
        }
    }

    /// <summary>Prázdná pláň bez jediné budovy — tohle je ten svět „mimo město".</summary>
    private static (FaunaSystem Fauna, Simulation Sim, Camera2D Camera) Wild()
    {
        var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();

        var camera = new Camera2D();
        camera.SetViewport(1280, 720);
        camera.CenterOn(new Vector2(40 * TerrainRenderer.TileSize, 40 * TerrainRenderer.TileSize), 2f);

        return (new FaunaSystem(content), sim, camera);
    }
}
