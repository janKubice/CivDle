using CivDle;
using CivDle.Core.Content;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Rady k pádům při startu. Testuje se to, co by hráč jinak odnesl jako
/// „hra nejde spustit": u známých příčin musí padnout věta, se kterou se dá
/// něco dělat — a u neznámých se nesmí vymýšlet rada, která nikam nevede.
/// </summary>
public sealed class StartupDiagnosisTests
{
    /// <summary>Windows kód „zásada řízení aplikací tento soubor zablokovala".</summary>
    private const int BlockedByAppControl = unchecked((int)0x800711C7);

    [Fact]
    public void OldDriversGetAnAnswerNotAStackTrace()
    {
        // Nejčastější pád na cizím stroji — a ten, u kterého hráč nejspíš hru smaže.
        string? hint = StartupDiagnosis.HintFor(new NoSuitableGraphicsDeviceException("Failed to create graphics device!"));

        Assert.NotNull(hint);
        Assert.Contains("ovladače", hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AFileBlockedByWindowsIsNamedAsSuch()
    {
        // Bez vysvětlení vypadá blokace od Windows jako rozbitá instalace hry.
        var blocked = new FileLoadException("blokováno", "FontStashSharp.MonoGame.dll")
        {
            HResult = BlockedByAppControl,
        };

        string? hint = StartupDiagnosis.HintFor(blocked);

        Assert.NotNull(hint);
        Assert.Contains("FontStashSharp.MonoGame.dll", hint, StringComparison.Ordinal);
        Assert.Contains("Windows", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void AnOrdinaryFileErrorIsNotBlamedOnWindows()
    {
        // Chybějící soubor není blokace — svést to na Smart App Control by
        // hráče poslalo vypínat ochranu, která za to nemůže.
        string? hint = StartupDiagnosis.HintFor(new FileLoadException("chybí", "cokoliv.dll"));

        Assert.Null(hint);
    }

    [Fact]
    public void BrokenDataPointsAtTheDataNotTheCode()
    {
        string? hint = StartupDiagnosis.HintFor(new ContentLoadException("data/buildings.json", "rozbité"));

        Assert.NotNull(hint);
        Assert.Contains("mods", hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnUnknownFailureGetsNoInventedAdvice()
    {
        // Špatná rada je horší než žádná: pošle hráče přenastavovat věci,
        // které s pádem nesouvisí.
        Assert.Null(StartupDiagnosis.HintFor(new InvalidOperationException("něco jiného")));
    }
}
