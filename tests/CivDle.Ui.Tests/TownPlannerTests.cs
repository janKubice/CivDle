using CivDle.Capture;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Půdorys ukázkového městečka do traileru.
///
/// <para>Testuje se to, co rozhoduje, jestli záběr vypadá jako město, nebo jako
/// tabulka: nepravidelné bloky, dvorky uvnitř, zeleň, pestrá zástavba a to, že
/// se domy nepřekrývají s ulicemi. Krása se otestovat nedá — <b>tyhle</b>
/// vlastnosti ano, a bez nich krása nevznikne.</para>
/// </summary>
public class TownPlannerTests
{
    private const int Size = 40;

    [Fact]
    public void TheSameSeedDrawsTheSameTown()
    {
        // Bez tohohle by se natočený záběr nedal zopakovat — a to je u traileru
        // půlka práce (znovu, o vteřinu delší, s jinou hudbou).
        var first = TownPlanner.Plan(1234, Size);
        var second = TownPlanner.Plan(1234, Size);

        Assert.Equal(first.Roads, second.Roads);
        Assert.Equal(first.Lots.Count, second.Lots.Count);
        for (int i = 0; i < first.Lots.Count; i++)
        {
            Assert.Equal(first.Lots[i].X, second.Lots[i].X);
            Assert.Equal(first.Lots[i].Y, second.Lots[i].Y);
            Assert.Equal(first.Lots[i].Candidates[0], second.Lots[i].Candidates[0]);
        }
    }

    [Fact]
    public void DifferentSeedsDrawDifferentTowns()
    {
        // Přehlídka městeček má ukázat, že hra pokaždé vypadá jinak. Kdyby dva
        // seedy daly totéž, byla by to přehlídka jednoho města.
        var a = TownPlanner.Plan(1, Size);
        var b = TownPlanner.Plan(2, Size);

        // Podle počtu dlaždic to poznat nejde — dva různé půdorysy mohou mít
        // shodou okolností stejně silnic. Musí se porovnat tvar.
        Assert.NotEqual(a.Roads.ToHashSet(), b.Roads.ToHashSet());
    }

    [Fact]
    public void NothingIsBuiltOnTheStreet()
    {
        var plan = TownPlanner.Plan(7, Size);
        var roads = new HashSet<(int, int)>(plan.Roads);

        foreach (var lot in plan.Lots)
        {
            for (int y = lot.Y; y < lot.Y + lot.Height; y++)
            {
                for (int x = lot.X; x < lot.X + lot.Width; x++)
                {
                    Assert.DoesNotContain((x, y), roads);
                }
            }
        }
    }

    [Fact]
    public void LotsDoNotOverlap()
    {
        // Překryv by nebyl vidět jako chyba — druhý dům by se prostě nepostavil
        // a v ulici by zůstala díra. Proto to hlídá test, ne oko.
        var plan = TownPlanner.Plan(11, Size);
        var taken = new HashSet<(int, int)>();

        foreach (var lot in plan.Lots)
        {
            for (int y = lot.Y; y < lot.Y + lot.Height; y++)
            {
                for (int x = lot.X; x < lot.X + lot.Width; x++)
                {
                    Assert.True(taken.Add((x, y)), $"dvě parcely na {x},{y}");
                }
            }
        }
    }

    [Fact]
    public void EverythingFitsInsideTheFrame()
    {
        var plan = TownPlanner.Plan(21, Size);

        foreach (var lot in plan.Lots)
        {
            Assert.InRange(lot.X, 0, Size - lot.Width);
            Assert.InRange(lot.Y, 0, Size - lot.Height);
        }

        foreach (var (x, y) in plan.Roads)
        {
            Assert.InRange(x, 0, Size - 1);
            Assert.InRange(y, 0, Size - 1);
        }
    }

    [Fact]
    public void EveryLotOffersACandidate()
    {
        var plan = TownPlanner.Plan(33, Size);

        Assert.NotEmpty(plan.Lots);
        Assert.All(plan.Lots, lot => Assert.NotEmpty(lot.Candidates));
    }

    [Fact]
    public void CandidatesAreAllRealBuildings()
    {
        // Paleta je psaná ručně; překlep v ID by se jinak projevil až tím, že
        // v městečku chybí půlka domů.
        var plan = TownPlanner.Plan(44, Size);
        var known = new HashSet<string>(TownPlanner.AllBuildingIds);

        foreach (var lot in plan.Lots)
        {
            Assert.All(lot.Candidates, id => Assert.Contains(id, known));
        }
    }

    [Fact]
    public void TheStreetsAreIrregular()
    {
        // Stejné rozteče = tabulka. Tohle je ta vlastnost, kvůli které plán
        // vůbec vznikl, takže si zaslouží vlastní test.
        var plan = TownPlanner.Plan(55, Size);

        // Pozor na past: příčné ulice pokrývají všechna x, takže sada „x, kde je
        // nějaká silnice" je souvislá a rozteče z ní nevykoukáš. Svislá ulice je
        // ta, která má silnici po celé své délce.
        var perColumn = plan.Roads.GroupBy(tile => tile.X).ToDictionary(g => g.Key, g => g.Count());
        int height = perColumn.Values.Max();
        var avenues = perColumn.Where(pair => pair.Value == height).Select(pair => pair.Key).Order().ToList();

        var gaps = new HashSet<int>();
        for (int i = 1; i < avenues.Count; i++)
        {
            if (avenues[i] - avenues[i - 1] > 1)
            {
                gaps.Add(avenues[i] - avenues[i - 1]);
            }
        }

        Assert.True(gaps.Count >= 2, $"ulice mají všude stejnou rozteč ({string.Join(", ", gaps)})");
    }

    [Fact]
    public void BlocksKeepACourtyard()
    {
        // Uvnitř bloku má zůstat volno. Kdyby se blok zaplnil celý, splyne
        // zástavba v jednu plochu — přesně to, co ze záběru dělá kaši.
        var plan = TownPlanner.Plan(66, Size);
        var built = new HashSet<(int, int)>();
        foreach (var lot in plan.Lots)
        {
            for (int y = lot.Y; y < lot.Y + lot.Height; y++)
            {
                for (int x = lot.X; x < lot.X + lot.Width; x++)
                {
                    built.Add((x, y));
                }
            }
        }

        var roads = new HashSet<(int, int)>(plan.Roads);
        int free = 0;
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                if (!roads.Contains((x, y)) && !built.Contains((x, y)))
                {
                    free++;
                }
            }
        }

        Assert.True(free > 0, "v městečku nezůstalo volné místo");
    }

    [Fact]
    public void TheTownHasGreenInIt()
    {
        // „Aby vynikly parky" je zadání celé kulisy. Bez zeleně je to jen
        // zástavba.
        var plan = TownPlanner.Plan(77, Size);
        int green = plan.Lots.Count(lot => lot.Candidates[0] is "park" or "city_park" or "botanical_garden");

        Assert.True(green >= 4, $"parků je jen {green}");
    }

    [Fact]
    public void TheTownIsVariedButNotRandom()
    {
        // Dvě strany téže mince: pestrost (ne jedno město z jednoho domu)
        // a charakter (ne náhodná směsice). Proto obě meze najednou.
        var plan = TownPlanner.Plan(88, Size);
        var kinds = new HashSet<string>(plan.Lots.Select(lot => lot.Candidates[0]));

        Assert.True(kinds.Count >= 8, $"v městečku je jen {kinds.Count} druhů budov");

        var housing = plan.Lots.Count(lot => lot.Candidates[0] is "house" or "cottage" or "brick_house");
        Assert.True(housing >= plan.Lots.Count * 0.15, "bydlení tvoří jen zlomek města");
    }

    [Fact]
    public void TheCenterHasASquare()
    {
        // Náměstí je to, kolem čeho se město čte. Poznáme ho podle toho, že
        // uprostřed stojí pomník a kolem něj je volno.
        var plan = TownPlanner.Plan(99, Size);
        var monuments = new[] { "great_statue", "triumphal_arch", "observatory", "obelisk", "clock_tower", "fountain_square" };

        bool nearCenter = plan.Lots.Any(lot =>
            monuments.Contains(lot.Candidates[0])
            && Math.Abs(lot.X - Size / 2) <= 6
            && Math.Abs(lot.Y - Size / 2) <= 6);

        Assert.True(nearCenter, "uprostřed městečka není náměstí");
    }
}
