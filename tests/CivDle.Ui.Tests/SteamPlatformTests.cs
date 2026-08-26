using CivDle.Core.Platform;
using CivDle.Platform;
using Xunit;
using Xunit.Abstractions;

namespace CivDle.Ui.Tests;

/// <summary>
/// Steam jako platforma — a hlavně to, co se stane, když neběží.
///
/// <para>Plán u téhle položky říkal: „Když Steam neběží, hra běží dál. Ověř to
/// testem, ne pohledem." Tohle je ten test. Běží v prostředí bez Steamu, takže
/// ověřuje přesně tu cestu, kterou projde každý hráč, co si hru spustí
/// napřímo — a taky celé CI.</para>
/// </summary>
public class SteamPlatformTests
{
    private readonly ITestOutputHelper _out;

    public SteamPlatformTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void WithoutSteamItJustReturnsNothing()
    {
        var local = Local();

        var steam = SteamPlatformServices.TryCreate(local);

        // Null je správná odpověď: volající zůstane u lokální implementace.
        // Výjimka by znamenala, že hra bez Steamu vůbec nenaběhne.
        _out.WriteLine(steam is null ? "Steam tu není, jak se čekalo" : "Steam běží");
        if (steam is null)
        {
            return;
        }

        // Kdyby se test někdy pustil na stroji se Steamem, ať aspoň ověří,
        // že se obálka chová rozumně.
        Assert.True(steam.IsAvailable);
        Assert.False(steam.HasOnlineLeaderboards);
        steam.Dispose();
    }

    [Fact]
    public void FriendsAreEmptyWithoutSteamInsteadOfThrowing()
    {
        // Karavany s kamarády se musí tiše přeskočit, ne shodit hru.
        var friends = new SteamFriendsSource(Local()).Load();

        Assert.NotNull(friends);
    }

    [Fact]
    public void AnAvatarHandleThatIsNotThereIsNotAnError()
    {
        Assert.False(SteamFriendsSource.TryReadAvatar(0, out _, out _, out _));
        Assert.False(SteamFriendsSource.TryReadAvatar(-1, out _, out _, out _));
    }

    [Fact]
    public void TheLocalPlatformStillWorksOnItsOwn()
    {
        // Kontrolní test: kdyby se Steamem něco bylo, lokální cesta musí držet.
        var local = Local();
        local.UnlockAchievement("TEST_ACHIEVEMENT");

        Assert.True(local.IsAchievementUnlocked("TEST_ACHIEVEMENT"));
        Assert.True(local.IsAvailable);
    }

    private static LocalPlatformServices Local() =>
        new(Path.Combine(Path.GetTempPath(), $"civdle-steam-test-{Guid.NewGuid():N}.json"))
        {
            PlayerName = "test",
        };
}
