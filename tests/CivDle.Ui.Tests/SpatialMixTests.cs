using CivDle.Audio;
using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Míchání prostorového zvuku.
///
/// <para>Zvuková karta tu není, a nemusí být: co je slyšet a odkud je
/// rozhodnutí o hře, ne o hardwaru. Hlídá se hlavně to, že <b>sto pil nezní
/// jako sto pil</b> — hlasitost má strop, jinak by z průmyslového města byla
/// kaše, ze které si hráč vypne zvuk.</para>
/// </summary>
public class SpatialMixTests
{
    private static readonly int KindCount = Enum.GetValues<SoundLoop>().Length;

    [Fact]
    public void RealContentHasSomethingToListenTo()
    {
        var content = LoadContent();

        Assert.Contains(content.Buildings.All, b => b.Sound is not null);
        foreach (var def in content.Buildings.All)
        {
            if (def.Sound is { } sound)
            {
                Assert.True(sound.RadiusTiles > 0, $"'{def.Id}' se tváří, že zní, a mlčí");
                Assert.InRange(sound.Volume, 0.01, 1.0);
            }
        }
    }

    [Fact]
    public void AnEmptyCityIsSilent()
    {
        var (sim, content) = Scene();
        var mix = Mix(content, sim, listenerX: 0, listenerY: 0);

        Assert.All(mix, m => Assert.False(m.IsAudible));
    }

    [Fact]
    public void ABuildingIsLoudestUpCloseAndInaudibleFarAway()
    {
        var (sim, content) = Scene();
        int sawmill = Sounding(content, SoundLoop.Mill);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(sawmill, 0, 0));

        var near = Mix(content, sim, 0, 0)[(int)SoundLoop.Mill];
        var far = Mix(content, sim, 200, 200)[(int)SoundLoop.Mill];

        Assert.True(near.IsAudible);
        Assert.False(far.IsAudible);
    }

    [Fact]
    public void ABuildingToTheRightSoundsFromTheRight()
    {
        var (sim, content) = Scene();
        int sawmill = Sounding(content, SoundLoop.Mill);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(sawmill, 6, 0));

        var mix = Mix(content, sim, 0, 0)[(int)SoundLoop.Mill];

        Assert.True(mix.Pan > 0, $"pila vpravo zní z panorama {mix.Pan}");
    }

    [Fact]
    public void AHundredSawmillsDoNotDrownEverythingElse()
    {
        // Tohle je ten důvod, proč se míchá po druzích a se stropem.
        var (sim, content) = Scene();
        int sawmill = Sounding(content, SoundLoop.Mill);

        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                sim.TryPlaceBuildingFree(sawmill, x * 2, y * 2);
            }
        }

        var mix = Mix(content, sim, 0, 0)[(int)SoundLoop.Mill];

        Assert.True(mix.Volume <= 1f, $"hlasitost přetekla na {mix.Volume}");
        Assert.InRange(mix.Pan, -1f, 1f);
    }

    [Fact]
    public void SoundsOfDifferentKindsDoNotMixTogether()
    {
        var (sim, content) = Scene();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(Sounding(content, SoundLoop.Mill), 0, 0));

        var mix = Mix(content, sim, 0, 0);

        Assert.True(mix[(int)SoundLoop.Mill].IsAudible);
        Assert.False(mix[(int)SoundLoop.Forge].IsAudible);
    }

    // ----- pomocné -----

    private static SoundMix[] Mix(GameContent content, Simulation sim, double listenerX, double listenerY)
    {
        var indices = new List<int>();
        for (int i = 0; i < sim.Buildings.Length; i++)
        {
            indices.Add(i);
        }

        var results = new SoundMix[KindCount];
        SpatialMix.Compute(content, sim.Buildings, indices, listenerX, listenerY, results);
        return results;
    }

    /// <summary>Index libovolné budovy, která vydává daný zvuk.</summary>
    private static int Sounding(GameContent content, SoundLoop loop)
    {
        for (int i = 0; i < content.Buildings.Count; i++)
        {
            if (content.Buildings[i].Sound?.Loop == loop && content.Buildings[i].Buildable)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"v datech není budova se zvukem {loop}");
    }

    private static (Simulation Sim, GameContent Content) Scene()
    {
        var content = LoadContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();
        return (sim, content);
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
