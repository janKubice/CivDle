using CivDle.Core.Content;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Dá se na každou budovu v datech vůbec někdy dosáhnout?
///
/// <para>Proč zrovna tenhle test: <c>buildable: false</c> znamená „jen jako cíl
/// vylepšení". Když ale na budovu nic nevylepšuje ani neslučuje, je z ní obsah,
/// který ve hře nikdy nikdo neuvidí — a nic to nenahlásí. Přesně tak se osm
/// budov (přístav, rybářství a šest megastruktur) dostalo do dat, měly svoje
/// jméno, cenu, ikonu i achievement, a postavit se nedaly.</para>
///
/// <para>„Postavit" tu neznamená jen „hráč si to koupí": pomník po osobnosti
/// staví simulace, a přesto je to budova, kterou hráč ve hře uvidí.</para>
/// </summary>
public class BuildingReachabilityTests
{
    [Fact]
    public void EveryBuildingCanBeReachedSomehow()
    {
        var content = TestData.LoadRealContent();
        var reachable = new HashSet<string>();

        foreach (var def in content.Buildings.All)
        {
            if (def.UpgradesToIndex >= 0)
            {
                reachable.Add(content.Buildings[def.UpgradesToIndex].Id);
            }

            if (def.MergesToIndex >= 0)
            {
                reachable.Add(content.Buildings[def.MergesToIndex].Id);
            }
        }

        // Pomník po významné osobnosti postaví simulace sama — hráč ho postavit
        // nemá, a přesto ve hře skutečně vznikne.
        foreach (var figure in content.Figures.Figures)
        {
            if (figure.LeavesStatue)
            {
                reachable.Add(content.Buildings[figure.StatueBuildingIndex].Id);
            }
        }

        var orphans = new List<string>();
        foreach (var def in content.Buildings.All)
        {
            if (!def.Buildable && !reachable.Contains(def.Id))
            {
                orphans.Add(def.Id);
            }
        }

        Assert.True(
            orphans.Count == 0,
            "Tyhle budovy nejdou postavit ani na ně nic nevylepšuje: " + string.Join(", ", orphans));
    }

    [Fact]
    public void ABuildingOnGroundThatDoesNotExistNaturallyMustBeReachableSomehow()
    {
        // Uranový důl stojí jedině na zamoření po meteoritu. To je legitimní
        // návrh — ale jen dokud si to zamoření hráč umí sám udělat. Kdyby
        // takový terén neuměl vyrobit nic, byla by to budova, kterou nikdo
        // nikdy nepostaví, a nic by to nenahlásilo.
        var content = TestData.LoadRealContent();

        // Terén, který si hráč umí vyrobit: cíl nějaké modlitby nebo
        // terraformace. „Zamoření" dělá meteorit.
        var makeable = new HashSet<string>(StringComparer.Ordinal) { "fallout", "badlands", "shallow_water" };

        foreach (var def in content.Buildings.All)
        {
            bool anyNatural = false;
            var needed = new List<string>();
            for (int i = 0; i < content.Biomes.Count; i++)
            {
                if (!def.IsBiomeAllowed(i))
                {
                    continue;
                }

                if (content.Biomes[i].IsNaturallyGenerated)
                {
                    anyNatural = true;
                    break;
                }

                needed.Add(content.Biomes[i].Id);
            }

            if (anyNatural || needed.Count == 0)
            {
                continue;
            }

            Assert.True(
                needed.Any(makeable.Contains),
                $"'{def.Id}' smí stát jen na {string.Join(", ", needed)} — a ten terén nikdo neumí vyrobit.");
        }
    }

    [Fact]
    public void TierGatedBuildingsAreBuildableOnceTheTierIsReached()
    {
        // Stupeň měřítka budovu jen odemyká; když má zároveň 'buildable: false',
        // zůstane zamčená napořád a odměna za měřítko je prázdný slib.
        var content = TestData.LoadRealContent();

        foreach (var tier in content.AscensionTiers.All)
        {
            foreach (int index in tier.UnlockedBuildingIndices)
            {
                var def = content.Buildings[index];
                Assert.True(
                    def.Buildable || def.MergesToIndex >= 0,
                    $"'{def.Id}' odemyká stupeň '{tier.Id}', ale 'buildable' je false — nepostaví se nikdy.");
            }
        }
    }

    [Fact]
    public void TechUnlockedBuildingsAreBuildableOrAnUpgradeTarget()
    {
        var content = TestData.LoadRealContent();
        var upgradeTargets = new HashSet<int>();

        foreach (var def in content.Buildings.All)
        {
            if (def.UpgradesToIndex >= 0)
            {
                upgradeTargets.Add(def.UpgradesToIndex);
            }

            if (def.MergesToIndex >= 0)
            {
                upgradeTargets.Add(def.MergesToIndex);
            }
        }

        foreach (var tech in content.Techs.All)
        {
            foreach (int index in tech.UnlockedBuildingIndices)
            {
                var def = content.Buildings[index];
                Assert.True(
                    def.Buildable || upgradeTargets.Contains(index),
                    $"'{def.Id}' odemyká technologie '{tech.Id}', ale postavit se nedá.");
            }
        }
    }
}
