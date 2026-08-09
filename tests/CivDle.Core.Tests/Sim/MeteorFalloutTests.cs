using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Meteorit a to, co po něm zbude (body 38 a 39).
///
/// <para>Rána z nebe byla v půlce případů „utratil jsi 150 víry a nestalo se
/// nic" a i vyslyšená po sobě nechala jen jiný odstín hlíny. Teď kámen spadne
/// vždycky (nevyslyšená modlitba znamená, že spadne <b>vedle</b>) a nechá po
/// sobě zamořenou půdu, ze které se dá těžit uran.</para>
/// </summary>
public class MeteorFalloutTests
{
    private static Simulation Grass(out GameContent content)
    {
        content = TestData.LoadRealContent();
        return new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
    }

    /// <summary>
    /// Sklady na víru: modlitba za meteorit stojí víc, než je základní strop
    /// víry — bez skladišť by na ni hráč nikdy nenašetřil (a test by ji nikdy
    /// nevyvolal).
    /// </summary>
    private static void StockFaith(Simulation sim, GameContent content)
    {
        int warehouse = content.Buildings.IndexOf("warehouse");
        for (int i = 0; i < 6; i++)
        {
            for (int r = 0; r < content.Resources.Count; r++)
            {
                sim.AddResource(r, sim.GetStorageCap(r));
            }

            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(warehouse, -20 + i * 3, -20));
        }
    }

    private static int Prayer(GameContent content, string id)
    {
        for (int i = 0; i < content.Faith.Prayers.Count; i++)
        {
            if (content.Faith.Prayers[i].Id == id)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"modlitba '{id}' v datech chybí");
    }

    /// <summary>Modlí se, dokud rána nemine — u 45% šance to trvá pár pokusů.</summary>
    private static PrayerOutcome PrayUntil(Simulation sim, GameContent content, PrayerOutcome wanted)
    {
        int meteor = Prayer(content, "meteor");
        int faith = content.Faith.FaithResourceIndex;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            sim.AddResource(faith, sim.GetStorageCap(faith));
            var outcome = sim.TryPray(meteor, 1, 40 + attempt * 30, 40);
            if (outcome == wanted)
            {
                return outcome;
            }

            sim.Tick(); // los závisí i na tiku, jinak by se opakoval pořád stejný
        }

        throw new InvalidOperationException($"výsledek {wanted} se nepodařilo vyvolat");
    }

    [Fact]
    public void RealContentHasFalloutGroundThatOnlyAppearsFromAStrike()
    {
        var content = TestData.LoadRealContent();
        var fallout = content.Biomes[content.Biomes.IndexOf("fallout")];

        Assert.False(fallout.IsNaturallyGenerated, "zamořená půda se nesmí generovat sama");
        Assert.NotNull(fallout.ClickYield);
        Assert.Equal("uranium", content.Resources[fallout.ClickYield!.ResourceIndex].Id);
    }

    [Fact]
    public void AnsweredMeteorLeavesFalloutGround()
    {
        var sim = Grass(out var content);
        StockFaith(sim, content);
        PrayUntil(sim, content, PrayerOutcome.Answered);

        var (x, y) = sim.LastStrikeTile;
        Assert.Equal("fallout", content.Biomes[sim.BiomeAt(x, y)].Id);
    }

    [Fact]
    public void UnansweredStrikeStillFalls_JustElsewhere()
    {
        // Tohle je jádro bodu 38: „ničení budov i při špatném kliknutí".
        var sim = Grass(out var content);
        StockFaith(sim, content);
        var outcome = PrayUntil(sim, content, PrayerOutcome.Strayed);

        Assert.Equal(PrayerOutcome.Strayed, outcome);
        var (x, y) = sim.LastStrikeTile;
        Assert.Equal("fallout", content.Biomes[sim.BiomeAt(x, y)].Id);
    }

    [Fact]
    public void AStrayStrikeDestroysWhateverStandsWhereItLands()
    {
        var sim = Grass(out var content);
        StockFaith(sim, content);
        int house = content.Buildings.IndexOf("house");

        // Domky kolem cíle: minutá rána musí něco najít, ať mine kamkoli.
        for (int y = 30; y <= 50; y += 2)
        {
            for (int x = 30; x <= 50; x += 2)
            {
                for (int i = 0; i < content.Resources.Count; i++)
                {
                    sim.AddResource(i, sim.GetStorageCap(i));
                }

                sim.TryPlaceBuilding(house, x, y);
            }
        }

        int before = sim.Buildings.Length;
        Assert.True(before > 20, "test potřebuje hustou zástavbu");

        int faith = content.Faith.FaithResourceIndex;
        int meteor = Prayer(content, "meteor");
        for (int attempt = 0; attempt < 60; attempt++)
        {
            sim.AddResource(faith, sim.GetStorageCap(faith));
            if (sim.TryPray(meteor, 1, 40, 40) == PrayerOutcome.Strayed)
            {
                Assert.True(sim.Buildings.Length < before,
                    "minutá rána nesmí být bez následků — má spadnout vedle, ne nikam");
                return;
            }

            before = sim.Buildings.Length; // vyslyšená rána taky bourá; porovnávej s aktuálním stavem
            sim.Tick();
        }

        throw new InvalidOperationException("minutá rána se nepodařila vyvolat");
    }

    [Fact]
    public void BlessingsThatMissStillDoNothing()
    {
        // Minout smí jen rána. Déšť, který „spadne vedle", by byl nesmysl —
        // požehnání se odepře, ne přehodí.
        var sim = Grass(out var content);
        StockFaith(sim, content);
        int rain = Prayer(content, "rain");
        int faith = content.Faith.FaithResourceIndex;

        for (int attempt = 0; attempt < 200; attempt++)
        {
            sim.AddResource(faith, sim.GetStorageCap(faith));
            Assert.NotEqual(PrayerOutcome.Strayed, sim.TryPray(rain, 1, 40, 40));
            sim.Tick();
        }
    }

    [Fact]
    public void FalloutGroundHasItsOwnEconomy()
    {
        // Bod 39 je celý řetěz, ne jen jiná barva země: surovina, budovy, výzkum
        // a cesta zpátky k louce.
        var content = TestData.LoadRealContent();

        Assert.True(content.Resources.TryIndexOf("uranium", out _));
        Assert.True(content.Buildings.TryIndexOf("uranium_mine", out int mine));
        Assert.True(content.Buildings.TryIndexOf("nuclear_plant", out _));
        Assert.True(content.Techs.TryIndexOf("radiochemistry", out _));

        // Důl smí stát jen na zamořené půdě — jinak by zamoření byla jen kosmetika.
        var def = content.Buildings[mine];
        Assert.True(def.IsBiomeAllowed(content.Biomes.IndexOf("fallout")));
        Assert.False(def.IsBiomeAllowed(content.Biomes.IndexOf("grassland")));

        Assert.Contains(content.Terraform.All, o => o.Id == "decontaminate");
    }
}
