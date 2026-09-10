using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Dá se na každou vrstvu hry vůbec dostat?
///
/// <para>Proč zrovna tyhle testy: celá mechanika se dá vypnout <b>jedním
/// chybějícím blokem v datech</b>, a nic to nenahlásí — tlačítko se prostě
/// neukáže a hráč se o funkci nedozví. Přesně tak vypadalo plavení dřeva,
/// kde česle nemohly kládu chytit: systém fungoval, testy prošly, a ve hře
/// to bylo mrtvé.</para>
///
/// <para>Tyhle testy netestují chování, testují <b>dosažitelnost</b>. Selžou
/// ve chvíli, kdy někdo z dat vyndá blok, na kterém stojí kus rozhraní.</para>
/// </summary>
public class FeatureReachabilityTests
{
    [Fact]
    public void EveryOptionalLayerIsSwitchedOnInTheRealData()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Gameplay.History.IsEnabled, "časosběr — bez něj zmizí statistiky i kronika");
        Assert.True(content.Gameplay.Power.IsEnabled, "prostorová energetika — bez ní zmizí pohled na pokrytí");
        Assert.True(content.Gameplay.Subsea.IsEnabled, "podmoří — bez něj se nedá stavět na dně");
        Assert.True(content.Chronicle.IsEnabled, "kronika — bez šablon je to prázdná stránka");
        Assert.True(content.Scenarios.IsEnabled, "scénáře — bez nich zmizí položka z hlavního menu");
        Assert.True(content.Doctrines.IsEnabled, "doktríny — bez nich zmizí tlačítko z lišty");
        Assert.True(content.PointsOfInterest.IsEnabled, "anomálie — bez nich je mapa prázdná");
        Assert.True(content.Orbit.IsEnabled, "orbita — bez ní není co vypustit");
        Assert.True(content.Frontier.IsAvailable, "obrana — bez ní se režim nedá ani zapnout");
        Assert.True(content.Carillon.IsEnabled, "zvonohra — bez ní se nedá postavit");
        Assert.True(content.Figures.IsEnabled, "osobnosti — bez nich se nikdo nenarodí");
        Assert.True(content.HasRafting, "plavení dřeva — bez něj nemá splav co dělat");
        Assert.True(content.NpcCities.IsEnabled, "cizí města — bez nich není s kým obchodovat");
    }

    [Fact]
    public void EverythingTheCarillonNeedsIsInTheData()
    {
        // Zvonohra odkazuje budovu jménem. Kdyby ta budova z dat zmizela,
        // loader by to chytil — ale kdyby zmizel jen její sprite nebo jméno,
        // stála by na mapě jako bezejmenný čtvereček.
        var content = TestData.LoadRealContent();

        var building = content.Buildings[content.Carillon.BuildingIndex];

        Assert.True(building.Buildable, $"'{building.Id}' se nedá postavit — zvonohra by nikdy nezazvonila");
        Assert.NotEmpty(content.Carillon.DefaultTune);
    }

    [Fact]
    public void RaftingHasBothHalvesAndTheyCanMeet()
    {
        // Tohle je ta chyba, kvůli které tyhle testy vznikly. Splav bere dřevo
        // ze skladu; když ho nemá kdo vytáhnout, plavení surovinu NIČÍ.
        var content = TestData.LoadRealContent();

        var flumes = content.Buildings.All.Where(b => b.DropsLogs).ToList();
        var booms = content.Buildings.All.Where(b => b.CatchesLogs).ToList();

        Assert.NotEmpty(flumes);
        Assert.NotEmpty(booms);

        // Obě půlky musí jít postavit u téže řeky — tedy na souši.
        foreach (var def in flumes.Concat(booms))
        {
            Assert.False(
                def.IsSubsea,
                $"'{def.Id}' je podmořská budova — u řeky by ji nešlo postavit vůbec.");
        }
    }

    [Fact]
    public void TheDefenceModeHasSomethingToDefendWith()
    {
        // Režim, ve kterém přijdou vlny a hráč nemá čím střílet, není režim,
        // ale trest.
        var content = TestData.LoadRealContent();

        Assert.Contains(content.Buildings.All, b => b.IsArmed);
        Assert.NotEmpty(content.Frontier.Attackers);
        Assert.NotEmpty(content.Frontier.Waves);
    }

    [Fact]
    public void ArmedBuildingsAreOnlyOfferedInTheDefenceMode()
    {
        // Věž ve hře bez útoků je past: hráč za ni zaplatí dělníky a nikdy se
        // nedozví, proč nic nedělá.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));

        for (int i = 0; i < content.Buildings.Count; i++)
        {
            if (content.Buildings[i].IsArmed)
            {
                Assert.False(sim.IsBuildingBuildable(i), $"'{content.Buildings[i].Id}' jde postavit i bez obrany");
            }
        }
    }

    [Fact]
    public void EveryAnomalyKindCanActuallyAppear()
    {
        // Anomálie na biomu, který se negeneruje, by nikdy nevznikla.
        var content = TestData.LoadRealContent();

        foreach (var kind in content.PointsOfInterest.Kinds)
        {
            bool reachable = false;
            for (int i = 0; i < content.Biomes.Count; i++)
            {
                if (kind.IsBiomeAllowed(i) && content.Biomes[i].IsNaturallyGenerated)
                {
                    reachable = true;
                    break;
                }
            }

            Assert.True(reachable, $"anomálie '{kind.Id}' smí ležet jen na biomech, které se negenerují");
        }
    }

    [Fact]
    public void EveryMegastructureIsSomewhereInTheResearchTree()
    {
        // Megastruktury dlouho neodemykal žádný výzkum — stačila hodnost sídla.
        // Ve stromu se tedy neobjevily a hráč se o nich nemohl dozvědět jinak
        // než tím, že mu jednou samy naskočí v katalogu. Nejhůř na tom byl
        // kosmodrom: celá vesmírná část hry za ním visela a nic k ní
        // neukazovalo.
        var content = TestData.LoadRealContent();

        var unlocked = content.Techs.All
            .SelectMany(tech => tech.UnlockedBuildingIndices)
            .ToHashSet();

        foreach (var def in content.Buildings.All)
        {
            if (def.Category != "megastructure")
            {
                continue;
            }

            Assert.True(
                unlocked.Contains(content.Buildings.IndexOf(def.Id)),
                $"megastrukturu '{def.Id}' neodemyká žádná technologie — ve stromu výzkumu není vidět");
        }
    }

    [Fact]
    public void TheWayToOrbitLeadsThroughTheTree()
    {
        // Konkrétně tahle cesta musí existovat celá: bez kosmodromu se nedá
        // vypustit nic a bez výzkumu se nedá postavit kosmodrom.
        var content = TestData.LoadRealContent();

        int spaceport = content.Orbit.LaunchBuildingIndex;
        Assert.True(spaceport >= 0, "orbita nemá odkud startovat");

        Assert.Contains(
            content.Techs.All,
            tech => tech.UnlockedBuildingIndices.Contains(spaceport));
    }
}
