using CivDle.Core.Content;
using CivDle.Rendering;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Pohyb, který ve scéně zbývá, když hráč nic nedělá.
///
/// <para>Idle hra se na sebe většinu času jen dívá. Chodci a auta se ale
/// přestanou kreslit hned, jak kamera trochu odjede, takže mezi běžným
/// pohledem na město a agregátním pohledem zůstával úplně nehybný obraz —
/// a ten mozek přestane číst jako místo. Testuje se proto to, co z toho dělá
/// scénu: že se všechno hýbe podle <b>jednoho</b> větru, že je ten pohyb
/// nepravidelný, a že sousedi nekmitají v zákrytu.</para>
/// </summary>
public sealed class AmbientLifeTests
{
    private static BuildingDef Def(string id, string category, Recipe? recipe = null) =>
        new(id, category, new RgbColor(200, 150, 100), 1, 1,
            WorkerSlots: 0, HousingCapacity: 0,
            BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: recipe,
            AllowedBiomes: new[] { true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true,
            UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0);

    private static readonly Recipe AnyRecipe =
        new(Array.Empty<ResourceAmount>(), Array.Empty<ResourceAmount>(), 10);

    [Fact]
    public void SwayIsGentle()
    {
        // Přes pár setin radiánu už to není dech, ale bouře.
        for (float t = 0; t < 40f; t += 0.29f)
        {
            Assert.InRange(AmbientWind.Sway(3, 7, t), -AmbientWind.MaxSway, AmbientWind.MaxSway);
        }
    }

    [Fact]
    public void TheWindNeverStopsAndNeverPegs()
    {
        for (float t = 0; t < 120f; t += 0.41f)
        {
            Assert.InRange(AmbientWind.Gust(t), 0f, 1f);
        }
    }

    [Fact]
    public void TheWindIsNotAMetronome()
    {
        // Jedna sinusovka by dýchala pravidelně a oko na to přijde do pár vteřin.
        // Dvě nesoudělné periody se nesmí po jedné otočce potkat na stejné hodnotě.
        float first = AmbientWind.Gust(0f);
        float afterOnePeriod = AmbientWind.Gust(MathF.Tau / 0.21f);

        Assert.NotEqual(first, afterOnePeriod, 2);
    }

    [Fact]
    public void NeighbouringTreesDoNotSwayInLockstep()
    {
        // Les kolébající se jako jeden kus vypadá jako chyba animace.
        Assert.NotEqual(AmbientWind.Sway(10, 10, 2f), AmbientWind.Sway(11, 10, 2f), 4);
        Assert.NotEqual(AmbientWind.Sway(10, 10, 2f), AmbientWind.Sway(10, 11, 2f), 4);
    }

    [Fact]
    public void TheSameTreeAtTheSameMomentLeansTheSameWay()
    {
        Assert.Equal(AmbientWind.Sway(-4, 9, 3.5f), AmbientWind.Sway(-4, 9, 3.5f));
    }

    [Fact]
    public void SmokeBlowsTheSameWayTheTreesLean()
    {
        // Kdyby se kouř snášel proti větru, foukalo by v jedné scéně dvěma směry.
        for (float t = 0; t < 30f; t += 0.5f)
        {
            Assert.True(AmbientWind.Drift(t, 1f, phase: 0f) > 0f,
                $"Kouř se v čase {t:F1} snáší proti větru.");
        }
    }

    [Fact]
    public void SmokeDriftsMoreTheHigherItGets()
    {
        // U komína je kouř ještě rovně, nahoře už ho vítr odnáší.
        float low = AmbientWind.Drift(5f, 0.1f, 0f);
        float high = AmbientWind.Drift(5f, 1f, 0f);

        Assert.True(high > low, $"Kouř se nahoře nesnáší víc ({high} vs {low}).");
    }

    [Fact]
    public void AtTheChimneyThereIsNoDriftYet()
    {
        Assert.Equal(0f, AmbientWind.Drift(7f, 0f, 1.3f), 5);
    }

    [Fact]
    public void WorkshopsAndPowerPlantsSmoke()
    {
        Assert.Equal(
            AmbientLifeRenderer.Stack.Works,
            AmbientLifeRenderer.StackKind(Def("smelter", "production", AnyRecipe), 0, 0));

        Assert.Equal(
            AmbientLifeRenderer.Stack.Works,
            AmbientLifeRenderer.StackKind(Def("coal_plant", "power"), 0, 0));
    }

    [Fact]
    public void WarehousesAndMonumentsDoNot()
    {
        // Kouřící sklad by byl nesmysl, který hráč hlásí jako chybu.
        Assert.Equal(
            AmbientLifeRenderer.Stack.None,
            AmbientLifeRenderer.StackKind(Def("warehouse", "storage"), 0, 0));

        Assert.Equal(
            AmbientLifeRenderer.Stack.None,
            AmbientLifeRenderer.StackKind(Def("obelisk", "monument"), 0, 0));
    }

    [Fact]
    public void AFarmWithoutARecipeDoesNotSmoke()
    {
        // Kategorie sama nestačí: provoz bez receptu nic nezpracovává.
        Assert.Equal(
            AmbientLifeRenderer.Stack.None,
            AmbientLifeRenderer.StackKind(Def("pasture", "production"), 0, 0));
    }

    [Fact]
    public void OnlyHousesWithAChimneySmoke()
    {
        // Komín na střeše je varianta instance — kouř má vycházet jen tam,
        // kde ten komín opravdu je.
        var house = Def("hut", "housing");
        int smoking = 0;
        const int total = 300;
        for (int i = 0; i < total; i++)
        {
            bool hasChimney = BuildingVariation.For(i, i * 5, 0).Extra == BuildingExtra.Chimney;
            var kind = AmbientLifeRenderer.StackKind(house, i, i * 5);

            Assert.Equal(hasChimney ? AmbientLifeRenderer.Stack.Home : AmbientLifeRenderer.Stack.None, kind);
            if (hasChimney)
            {
                smoking++;
            }
        }

        Assert.InRange(smoking / (double)total, 0.01, 0.2);
    }

    [Fact]
    public void AmbientMotionOutlastsThePedestrians()
    {
        // Tohle je celý důvod, proč ta vrstva vznikla: chodci mizí dřív než ona.
        Assert.True(AmbientLifeRenderer.SmokeZoom < DetailLevel.BaseCreatures);
        Assert.True(AmbientLifeRenderer.BirdZoom < DetailLevel.BaseCreatures);
    }
}
