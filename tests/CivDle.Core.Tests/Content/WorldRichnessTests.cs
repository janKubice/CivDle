using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Bohatost světa. Hráčova stížnost byla „mapa je extrémně chudá" — a měl
/// pravdu: patnáct biomů z dvaceti tří nemělo jediný zajímavý bod a v oceánu
/// nebylo vůbec nic.
///
/// <para>Proto se to měří, ne odhaduje. Prázdný biom je díra v zážitku: hráč
/// tam dopluje, nic nenajde a nemá důvod se vrátit.</para>
/// </summary>
public class WorldRichnessTests
{
    [Fact]
    public void EveryBiomeHasSomethingWorthFinding()
    {
        var content = TestData.LoadRealContent();

        for (int i = 0; i < content.Biomes.Count; i++)
        {
            int index = i;
            bool hasLandmark = content.Landmarks.All.Any(l => l.BiomeMask[index]);
            Assert.True(hasLandmark,
                $"Biom '{content.Biomes[index].Id}' nemá jediný zajímavý bod — hráč tam nemá co objevit.");
        }
    }

    [Fact]
    public void EveryBiomeLooksLikeSomething()
    {
        // Biom bez dekorace je jednolitá barevná plocha.
        var content = TestData.LoadRealContent();

        for (int i = 0; i < content.Biomes.Count; i++)
        {
            int index = i;
            bool decorated = content.Decorations.Any(d => d.BiomeMask[index]);
            Assert.True(decorated,
                $"Biom '{content.Biomes[index].Id}' nemá žádnou dekoraci — je to jen barevná plocha.");
        }
    }

    [Fact]
    public void EveryBiomeHasSomethingAlive()
    {
        var content = TestData.LoadRealContent();

        for (int i = 0; i < content.Biomes.Count; i++)
        {
            int index = i;
            bool alive = content.Fauna.Any(f => f.BiomeMask[index]);
            Assert.True(alive, $"V biomu '{content.Biomes[index].Id}' nic nežije.");
        }
    }

    [Fact]
    public void TheWorldHasEnoughVariety()
    {
        // Strop proti opačnému extrému — pár landmarků rozesetých po velké mapě
        // je totéž jako žádné.
        var content = TestData.LoadRealContent();

        Assert.True(content.Landmarks.Count >= 30, $"zajímavých bodů je jen {content.Landmarks.Count}");
        Assert.True(content.Fauna.Count >= 20, $"druhů fauny je jen {content.Fauna.Count}");
        Assert.True(content.Decorations.Count >= 24, $"dekorací je jen {content.Decorations.Count}");
    }
}
