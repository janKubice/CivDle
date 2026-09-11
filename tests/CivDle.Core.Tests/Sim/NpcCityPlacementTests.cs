using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Cizí města musí stát na souši.
///
/// <para>Poloha města se odvozovala z hashe a terénu se nikdy nezeptala. Na
/// mapě, kde je půlka plochy voda, tedy pravidelně vznikala města uprostřed
/// moře — a protože do vody nejde postavit dům, byla zároveň prázdná. Obě
/// půlky té chyby mají tutéž příčinu.</para>
/// </summary>
public class NpcCityPlacementTests
{
    [Fact]
    public void NoCityStandsInWater()
    {
        var (map, isLand) = Map();

        int checkedCities = 0;
        for (int cy = -12; cy <= 12; cy++)
        {
            for (int cx = -12; cx <= 12; cx++)
            {
                if (!map.TryCityIn(cx, cy, out var city))
                {
                    continue;
                }

                checkedCities++;
                Assert.True(isLand(city.X, city.Y),
                    $"město '{city.Key}' stojí ve vodě na {city.X},{city.Y}");
            }
        }

        Assert.True(checkedCities > 20, $"testem prošlo jen {checkedCities} měst — málo na důkaz");
    }

    [Fact]
    public void EveryCityHasRoomToBuild()
    {
        // Město na dlaždici trčící z moře by nemělo kam postavit domy a hráč by
        // našel jméno bez města.
        var (map, isLand) = Map();

        for (int cy = -12; cy <= 12; cy++)
        {
            for (int cx = -12; cx <= 12; cx++)
            {
                if (!map.TryCityIn(cx, cy, out var city))
                {
                    continue;
                }

                int land = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (isLand(city.X + dx, city.Y + dy))
                        {
                            land++;
                        }
                    }
                }

                Assert.True(land >= 6, $"město na {city.X},{city.Y} má kolem sebe jen {land}/9 souše");
            }
        }
    }

    [Fact]
    public void PlacementStaysTheSameBetweenLookups()
    {
        // Poloha se nikde neukládá — musí tedy vyjít pokaždé stejně, jinak by
        // se města po znovunačtení hry stěhovala.
        var (first, _) = Map();
        var (second, _) = Map();

        for (int cy = -6; cy <= 6; cy++)
        {
            for (int cx = -6; cx <= 6; cx++)
            {
                bool a = first.TryCityIn(cx, cy, out var cityA);
                bool b = second.TryCityIn(cx, cy, out var cityB);

                Assert.Equal(a, b);
                Assert.Equal(cityA, cityB);
            }
        }
    }

    [Fact]
    public void AnAllWaterWorldHasNoCities()
    {
        // Buňka celá pod vodou nemá mít město. Dřív ho měla vždycky.
        var content = LoadContent();
        var map = new NpcCityMap(1, content.NpcCities.Archetypes.Count, content.NpcCities.Names.Count,
            (_, _) => false);

        for (int cy = -5; cy <= 5; cy++)
        {
            for (int cx = -5; cx <= 5; cx++)
            {
                Assert.False(map.TryCityIn(cx, cy, out _), $"v samé vodě vzniklo město v buňce {cx},{cy}");
            }
        }
    }

    [Fact]
    public void AnAllLandWorldStillHasCities()
    {
        // Pojistka proti opačnému extrému: kdyby se hledání souše pokazilo,
        // zmizela by cizí města ze hry úplně a nikdo by si nevšiml proč.
        var content = LoadContent();
        var map = new NpcCityMap(1, content.NpcCities.Archetypes.Count, content.NpcCities.Names.Count,
            (_, _) => true);

        int found = 0;
        for (int cy = -5; cy <= 5; cy++)
        {
            for (int cx = -5; cx <= 5; cx++)
            {
                if (map.TryCityIn(cx, cy, out _))
                {
                    found++;
                }
            }
        }

        Assert.True(found > 30, $"na samé souši vzniklo jen {found} měst");
    }

    private static (NpcCityMap Map, Func<int, int, bool> IsLand) Map()
    {
        var content = LoadContent();
        var preset = content.WorldGen.Presets[content.WorldGen.DefaultPresetIndex];
        var terrain = new ProceduralTerrain(content.Biomes, preset, 20260728);
        Func<int, int, bool> isLand = (x, y) => !content.Biomes[terrain.BiomeAt(x, y)].IsWater;

        return (new NpcCityMap(
            20260728, content.NpcCities.Archetypes.Count, content.NpcCities.Names.Count, isLand), isLand);
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
