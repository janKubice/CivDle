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
        //
        // A měří se přes několik nezávislých světů. Zvěř je kulisa, takže se
        // pohybuje bez semínka — jeden běh je tedy jeden los, a test na jediném
        // losu padal zhruba jednou z dvaceti na hodnotě těsně nad mezí. To není
        // nalezená chyba, to je šum: opakovaný pokus, ze kterého se bere
        // medián, měří tutéž vlastnost a nehlásí poplach kvůli jednomu běhu.
        var medians = new List<float>();
        var nineties = new List<float>();

        for (int world = 0; world < 5; world++)
        {
            var (fauna, sim, camera) = Wild();
            Run(fauna, sim, camera, seconds: 6);
            Run(fauna, sim, camera, seconds: 45);

            var distances = fauna.HerdsForTests
                .Select(h => Vector2.Distance(h.Position, h.Anchor))
                .OrderBy(d => d)
                .ToList();

            // Posuzuje se tvar skupiny, ne jeden nejhorší kus. Zatoulaný
            // opozdilec ke stádu patří — rozpadlé stádo je něco jiného než
            // stádo s opozdilcem, a test, který hlídá maximum, ten rozdíl
            // nepozná.
            medians.Add(distances[distances.Count / 2]);
            nineties.Add(distances[(int)(distances.Count * 0.9)]);
        }

        float median = Middle(medians);
        float ninety = Middle(nineties);

        _out.WriteLine($"od svého stáda: medián {median:0} px, p90 {ninety:0} px (5 světů)");
        Assert.True(median < TerrainRenderer.TileSize * 4, $"stádo se roztáhlo (medián {median:0} px)");
        Assert.True(ninety < TerrainRenderer.TileSize * 7, $"stádo se rozprchlo (p90 {ninety:0} px)");
    }

    /// <summary>Medián naměřených hodnot — prostřední běh, ne ten nejhorší.</summary>
    private static float Middle(List<float> values)
    {
        values.Sort();
        return values[values.Count / 2];
    }

    [Fact]
    public void WildlifeStillArrivesInABuiltUpTown()
    {
        // Tohle je ta chyba, kterou nešlo vidět v prázdné krajině.
        //
        // Zvíře, které šláplo na budovu, se RUŠILO. Ve městě, kde je devět
        // dlaždic z deseti zastavěných, tím zmizelo do vteřiny — takže právě
        // tam, kam se hráč dívá nejčastěji, žádná zvěř nebyla. Naměřeno na
        // hotovém snímku: 1 až 13 kusů proti stropu 40.
        //
        // Přidat pokusy o vypuštění nepomohlo ani o kus: problém nebyl
        // v rození, ale v umírání. Test to ukázal — s opraveným spawnerem
        // a starým rušením pořád vycházelo jedno zvíře ze čtyřiceti.
        //
        // Test staví zastavěné město a čeká, že se zvěř přesto objeví na
        // zbytku krajiny. Měří se PODÍL stropu, ne absolutní číslo: přesný
        // počet závisí na losu, ale „skoro nic" od „slušně obsazeno" to
        // odliší spolehlivě.
        var (fauna, sim, camera) = Town();

        Run(fauna, sim, camera, seconds: 12);

        _out.WriteLine($"v zastavěném městě: {fauna.CountForTests} z {FaunaSystem.MaxActive}");
        Assert.True(
            fauna.CountForTests >= FaunaSystem.MaxActive / 2,
            $"v zastavěném městě se objevilo jen {fauna.CountForTests} zvířat z {FaunaSystem.MaxActive}");
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

    [Fact]
    public void ShyAnimalsRunFromPredatorsToo()
    {
        // V datech byl vlk, medvěd i lev, ale zvěř o nich nevěděla — gazela
        // se pásla lvovi pod nosem. Stádo, které se dá na útěk před šelmou,
        // udělá ze savany ekosystém místo zoo.
        //
        // Dravec se do světa vysadí ručně: čekat, až se lev sám objeví vedle
        // stáda, by byl test o náhodě, ne o útěku.
        // Tundra schválně: polární liška (dravec) i zajíc bělák (plachá kořist)
        // jsou tam činní v kteroukoli denní dobu, takže test nezávisí na tom,
        // kolik je zrovna hodin.
        var (fauna, sim, camera) = Wild("tundra");
        Run(fauna, sim, camera, seconds: 6);

        // Kořist, ne jiná šelma: polární liška je plachá i dravá, a vlastního
        // druhu se nebojí. Když byla náhodou první v poli, test padal.
        var prey = fauna.CrittersForTests.Where(c => c.Shy && !c.Predator).Select(c => c.Position).ToList();
        Assert.NotEmpty(prey);

        Assert.True(fauna.SpawnPredatorForTests(sim, prey[0]), "do světa se nepodařilo vysadit dravce");
        Run(fauna, sim, camera, seconds: 1);

        Assert.True(fauna.FleeingForTests > 0, "vedle stáda stojí šelma a nikdo se nehnul");
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

    /// <summary>
    /// Hustě zastavěné město. Tohle je ten případ, na kterém se spawner dusil.
    ///
    /// <para>Zástavba pokrývá celý výřez kamery a volných dlaždic je desetina —
    /// zhruba jako ve skutečném městě, kde mezi domy zbývají ulice a dvorky.
    /// Vzor je spočítaný, ne losovaný, aby test neměl vlastní náhodu.</para>
    ///
    /// <para>Poloviční zástavba nestačila: při ní uspěl i jediný losovaný bod
    /// dost často na to, aby se strop naplnil, a test chybu nechytil.</para>
    /// </summary>
    private static (FaunaSystem Fauna, Simulation Sim, Camera2D Camera) Town()
    {
        var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();
        sim.DebugFillStorages();

        int house = content.Buildings.IndexOf("house");
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 48; x++)
            {
                // Devět dlaždic z deseti zastavěných; zbytek jsou mezery.
                if ((x * 7 + y * 13) % 10 != 0)
                {
                    sim.TryPlaceBuildingFree(house, x, y);
                }
            }
        }

        var camera = new Camera2D();
        camera.SetViewport(1280, 720);
        camera.CenterOn(new Vector2(24 * TerrainRenderer.TileSize, 16 * TerrainRenderer.TileSize), 2f);

        return (new FaunaSystem(content), sim, camera);
    }

    /// <summary>Prázdná pláň bez jediné budovy — tohle je ten svět „mimo město".</summary>
    private static (FaunaSystem Fauna, Simulation Sim, Camera2D Camera) Wild(string biome = "grassland")
    {
        var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf(biome)));
        sim.SkipTutorial();

        var camera = new Camera2D();
        camera.SetViewport(1280, 720);
        camera.CenterOn(new Vector2(40 * TerrainRenderer.TileSize, 40 * TerrainRenderer.TileSize), 2f);

        return (new FaunaSystem(content), sim, camera);
    }
}
