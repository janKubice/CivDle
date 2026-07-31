using CivDle.Core.Content;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Vozidla pro dopravu po silnicích: obsah v datech, ne v renderu (CLAUDE.md).
///
/// <para>Testuje se hlavně to, na čem stojí použitelnost: éry na sebe musí
/// navazovat, jinak by v některé době po silnicích nejezdilo nic a město by
/// najednou vypadalo opuštěně.</para>
/// </summary>
public class VehicleContentTests
{
    [Fact]
    public void RealDataHasVehicles()
    {
        var content = TestData.LoadRealContent();

        Assert.NotEmpty(content.Vehicles);
    }

    [Fact]
    public void EveryEraHasSomethingOnTheRoads()
    {
        // Díra v posloupnosti by znamenala éru s prázdnými silnicemi — přesně
        // ten okamžik, kdy hráč nabude dojmu, že se něco rozbilo.
        var content = TestData.LoadRealContent();

        foreach (var era in content.Eras.All)
        {
            Assert.True(
                content.Vehicles.Any(v => v.FitsEra(era.Order)),
                $"V éře '{era.Id}' (order {era.Order}) by po silnicích nejezdilo nic.");
        }
    }

    [Fact]
    public void VehiclesGetFasterOverTime()
    {
        // Postup v čase má být vidět i na provozu, ne jen na barvě korby.
        var content = TestData.LoadRealContent();

        float earliest = content.Vehicles.Where(v => v.FitsEra(0)).Max(v => v.Speed);
        float latest = content.Vehicles.Where(v => v.FitsEra(6)).Max(v => v.Speed);

        Assert.True(latest > earliest, $"Nejrychlejší vozidlo budoucnosti ({latest}) není rychlejší než na začátku ({earliest}).");
    }

    [Fact]
    public void FitsEra_RespectsBothEnds()
    {
        var limited = new VehicleDef("cart", new RgbColor(1, 1, 1), 3, 5, 20f, MinEraOrder: 2, MaxEraOrder: 4, Glow: false);

        Assert.False(limited.FitsEra(1));
        Assert.True(limited.FitsEra(2));
        Assert.True(limited.FitsEra(4));
        Assert.False(limited.FitsEra(5));
    }

    [Fact]
    public void FitsEra_WithoutAnUpperBound_RunsForever()
    {
        var forever = new VehicleDef("pod", new RgbColor(1, 1, 1), 3, 5, 20f, MinEraOrder: 6, MaxEraOrder: -1, Glow: false);

        Assert.False(forever.FitsEra(5));
        Assert.True(forever.FitsEra(6));
        Assert.True(forever.FitsEra(99));
    }
}
