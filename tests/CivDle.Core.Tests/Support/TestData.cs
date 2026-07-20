using CivDle.Core.Content;

namespace CivDle.Core.Tests.Support;

/// <summary>Přístup ke skutečným herním datům zkopírovaným do výstupu testů (viz csproj).</summary>
internal static class TestData
{
    /// <summary>Složka se skutečnými herními daty.</summary>
    public static string RealDataDirectory => Path.Combine(AppContext.BaseDirectory, "data");

    /// <summary>Načte skutečný herní obsah (sdílená pomůcka integračních testů).</summary>
    public static GameContent LoadRealContent() => new ContentLoader().LoadFrom(RealDataDirectory);
}
