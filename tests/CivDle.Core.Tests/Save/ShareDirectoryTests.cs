using CivDle.Core.Save;
using Xunit;

namespace CivDle.Core.Tests.Save;

/// <summary>
/// Kam hra ukládá sdílitelné obrázky. Testuje se to, co by jinak vyšlo najevo
/// až na cizím počítači: obrázky musí jít vedle savu (kde má hra právo zapisovat),
/// ne vedle exe, a nesmí se míchat se souborem savu samotným.
/// </summary>
public class ShareDirectoryTests
{
    [Fact]
    public void ImagesGoNextToTheSave()
    {
        var store = new SaveStore(Path.Combine("profil", "save.civ"));

        Assert.Equal(Path.Combine("profil", "obrazky"), store.ShareDirectory);
    }

    [Fact]
    public void TheImageFolderIsNotTheSaveFile()
    {
        string path = Path.Combine("profil", "save.civ");
        var store = new SaveStore(path);

        Assert.NotEqual(path, store.ShareDirectory);
    }

    [Fact]
    public void ASaveWithoutAFolderStillHasSomewhereToWrite()
    {
        // Relativní cesta bez adresáře nesmí skončit prázdným řetězcem —
        // Directory.CreateDirectory("") hází výjimku.
        var store = new SaveStore("save.civ");

        Assert.False(string.IsNullOrWhiteSpace(store.ShareDirectory));
    }
}
