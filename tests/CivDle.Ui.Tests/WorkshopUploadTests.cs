using CivDle.Platform;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Publikace do Workshopu — testuje se to, co jde bez Steamu: kontroly, které
/// odpoví hned, a překlad steamovských výsledků na hlášky.
///
/// <para>Samotné nahrávání se ověřit nedá — chce živý klient a vydané App ID.
/// Zato se dá ověřit, že se ani nezačne, když je něco špatně, a že hráč
/// dostane hlášku, se kterou může něco udělat — místo čísla chyby.</para>
/// </summary>
public class WorkshopUploadTests
{
    [Fact]
    public void ANewUploadIsIdle()
    {
        using var upload = new WorkshopUpload(SteamAppId.CivDle);

        Assert.Equal(UploadStage.Idle, upload.Stage);
        Assert.False(upload.IsRunning);
        Assert.Empty(upload.Error);
    }

    [Fact]
    public void AMissingFolderIsRefusedRightAway()
    {
        using var upload = new WorkshopUpload(SteamAppId.CivDle);

        bool started = upload.Begin("/tohle/tu/opravdu/neni", "Můj mod", "popis", string.Empty);

        Assert.False(started);
        Assert.Equal(UploadStage.Failed, upload.Stage);
        Assert.Equal("workshop.error.noFolder", upload.Error);
    }

    [Fact]
    public void AModWithoutANameIsRefusedRightAway()
    {
        using var upload = new WorkshopUpload(SteamAppId.CivDle);
        string folder = TempFolder();

        bool started = upload.Begin(folder, "   ", "popis", string.Empty);

        Assert.False(started);
        Assert.Equal("workshop.error.noTitle", upload.Error);
    }

    [Fact]
    public void AnOversizedPreviewIsCaughtBeforeUploading()
    {
        // Limit se dá zjistit předem. Zjišťovat ho až po minutě nahrávání
        // by bylo zbytečně kruté.
        using var upload = new WorkshopUpload(SteamAppId.CivDle);
        string folder = TempFolder();
        string preview = Path.Combine(folder, "preview.png");
        File.WriteAllBytes(preview, new byte[WorkshopUpload.MaxPreviewBytes + 1]);

        bool started = upload.Begin(folder, "Můj mod", "popis", preview);

        Assert.False(started);
        Assert.Equal("workshop.error.previewTooBig", upload.Error);
    }

    [Fact]
    public void WithoutSteamItFailsInsteadOfThrowing()
    {
        // Tohle je ta cesta, kterou projde každý, kdo si hru spustí napřímo.
        using var upload = new WorkshopUpload(SteamAppId.CivDle);
        string folder = TempFolder();

        upload.Begin(folder, "Můj mod", "popis", string.Empty);

        Assert.NotEqual(UploadStage.Done, upload.Stage);
        Assert.False(upload.IsRunning && upload.Stage == UploadStage.Idle);
    }

    [Theory]
    [InlineData(Steamworks.EResult.k_EResultTimeout, "workshop.error.timeout")]
    [InlineData(Steamworks.EResult.k_EResultNotLoggedOn, "workshop.error.noSteam")]
    [InlineData(Steamworks.EResult.k_EResultLimitExceeded, "workshop.error.quota")]
    [InlineData(Steamworks.EResult.k_EResultInsufficientPrivilege, "workshop.error.banned")]
    [InlineData(Steamworks.EResult.k_EResultBadResponse, "workshop.error.generic")]
    public void SteamResultsBecomeSomethingTheAuthorCanActOn(Steamworks.EResult result, string expected)
    {
        Assert.Equal(expected, WorkshopUpload.ResultKey(result));
    }

    [Fact]
    public void EveryErrorKeyIsTranslated()
    {
        // Hláška, která se ukáže jako holý klíč, je horší než žádná.
        var content = new CivDle.Core.Content.ContentLoader()
            .LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var loc = new CivDle.Core.Content.Localization(content.Languages, "cs");

        foreach (Steamworks.EResult result in Enum.GetValues<Steamworks.EResult>().Distinct())
        {
            string key = WorkshopUpload.ResultKey(result);
            Assert.False(loc[key] == key, $"chybí překlad pro {key}");
        }
    }

    private static string TempFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"civdle-mod-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        return folder;
    }
}
