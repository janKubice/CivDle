using Xunit;
using Xunit.Abstractions;

namespace CivDle.Core.Tests;

/// <summary>
/// Kolik stojí formátování velkých čísel do HUD.
///
/// <para>Plán tuhle metodu jmenoval jako podezřelou: běží v HUDu šedesátkrát
/// za sekundu pro každou surovinu, takže i malá alokace se násobí dvaceti
/// a šedesáti. Test existuje proto, aby se o tom rozhodovalo podle čísla,
/// ne podle dojmu — a aby se poznalo, kdyby to někdo zdražil.</para>
/// </summary>
public class NumbersAllocationTests
{
    /// <summary>Kolik surovin se v HUDu obnovuje. Řádově; jde o poměr, ne o přesnost.</summary>
    private const int ResourcesInHud = 20;

    private readonly ITestOutputHelper _out;

    public NumbersAllocationTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void FormattingTheHudIsNotAMemoryLeakInDisguise()
    {
        double[] values = { 7, 942, 15_400, 3_250_000, 8.4e9, 1.2e15 };

        // Zahřátí (JIT, tabulky přípon).
        foreach (double value in values)
        {
            _ = Numbers.Format(value);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            _ = Numbers.Format(values[i % values.Length]);
        }

        long perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / 1000;
        long perSecond = perCall * ResourcesInHud * 60;
        _out.WriteLine($"{perCall} B na volání, {perSecond / 1024} kB/s při {ResourcesInHud} surovinách a 60 FPS");

        // Řetězec se vrací, takže nula to nikdy nebude. Jde o to, aby to
        // zůstalo v desítkách bajtů — sto kilobajtů za sekundu už je práce
        // pro GC, kterou hráč pozná jako záškub.
        Assert.True(perCall < 200, $"jedno zformátování stojí {perCall} B");
        Assert.True(perSecond < 200 * 1024, $"HUD by za sekundu vyrobil {perSecond / 1024} kB odpadu");
    }
}
