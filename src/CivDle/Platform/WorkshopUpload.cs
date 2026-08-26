using Steamworks;

namespace CivDle.Platform;

/// <summary>V jaké fázi je nahrávání do Workshopu.</summary>
public enum UploadStage
{
    /// <summary>Nic neběží.</summary>
    Idle,

    /// <summary>Zakládá se položka ve Workshopu.</summary>
    Creating,

    /// <summary>Nahrávají se soubory.</summary>
    Uploading,

    /// <summary>Hotovo. <see cref="WorkshopUpload.PublishedId"/> nese ID položky.</summary>
    Done,

    /// <summary>Nepovedlo se; důvod je v <see cref="WorkshopUpload.Error"/>.</summary>
    Failed,
}

/// <summary>
/// Nahrání modu do Steam Workshopu.
///
/// <para><b>Proč vlastní třída a ne pár řádků v obrazovce:</b> publikace je
/// <b>asynchronní</b> a má tři kroky (založit položku → naplnit ji → odeslat),
/// z nichž každý může doběhnout o vteřiny později. Držet ten stav v UI by
/// znamenalo, že se rozpadne, jakmile hráč obrazovku zavře.</para>
///
/// <para><b>Zamrznuté okno je horší než pomalé nahrávání.</b> Proto se tu nic
/// nečeká — obrazovka se ptá na <see cref="Stage"/> a <see cref="Progress"/>
/// a mezitím normálně kreslí.</para>
///
/// <para><b>Na co se při první publikaci narazí:</b> Steam vyžaduje
/// odsouhlasení pravidel Workshopu v prohlížeči; dokud to autor neudělá,
/// položka zůstane skrytá. Náhledový obrázek má limit 1 MB. Obojí hlásíme
/// jako srozumitelnou hlášku, ne jako číslo chyby.</para>
///
/// <para>Vrstva: aplikace. Jádro o Workshopu neví.</para>
/// </summary>
public sealed class WorkshopUpload : IDisposable
{
    /// <summary>Strop náhledového obrázku, který Steam přijme.</summary>
    public const long MaxPreviewBytes = 1024 * 1024;

    private readonly uint _appId;
    private CallResult<CreateItemResult_t>? _createResult;
    private CallResult<SubmitItemUpdateResult_t>? _submitResult;

    private string _folder = string.Empty;
    private string _title = string.Empty;
    private string _description = string.Empty;
    private string _previewFile = string.Empty;
    private UGCUpdateHandle_t _update;
    private bool _disposed;

    public WorkshopUpload(uint appId) => _appId = appId;

    /// <summary>V jaké fázi to je.</summary>
    public UploadStage Stage { get; private set; } = UploadStage.Idle;

    /// <summary>Postup nahrávání 0–1 (jen ve fázi <see cref="UploadStage.Uploading"/>).</summary>
    public double Progress { get; private set; }

    /// <summary>ID publikované položky, když se to povedlo.</summary>
    public ulong PublishedId { get; private set; }

    /// <summary>Proč to selhalo (lokalizační klíč), nebo prázdné.</summary>
    public string Error { get; private set; } = string.Empty;

    /// <summary>Musí autor odsouhlasit pravidla Workshopu v prohlížeči?</summary>
    public bool NeedsLegalAgreement { get; private set; }

    /// <summary>Běží to zrovna?</summary>
    public bool IsRunning => Stage is UploadStage.Creating or UploadStage.Uploading;

    /// <summary>
    /// Rozjede publikaci. Vrací false, když se ani nedá začít — to je odpověď
    /// hned, ne za deset vteřin.
    /// </summary>
    /// <param name="folder">Složka modu.</param>
    /// <param name="title">Jméno pro Workshop.</param>
    /// <param name="description">Popis od autora.</param>
    /// <param name="previewFile">Náhledový obrázek, nebo prázdné.</param>
    public bool Begin(string folder, string title, string description, string previewFile)
    {
        if (IsRunning)
        {
            return false;
        }

        Error = string.Empty;
        NeedsLegalAgreement = false;
        PublishedId = 0;
        Progress = 0;

        if (!Directory.Exists(folder))
        {
            Fail("workshop.error.noFolder");
            return false;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            Fail("workshop.error.noTitle");
            return false;
        }

        // Limit náhledu se dá zjistit předem, tak se nebude zjišťovat až
        // po minutě nahrávání.
        if (previewFile.Length > 0 && File.Exists(previewFile)
            && new FileInfo(previewFile).Length > MaxPreviewBytes)
        {
            Fail("workshop.error.previewTooBig");
            return false;
        }

        _folder = folder;
        _title = title.Trim();
        _description = description ?? string.Empty;
        _previewFile = previewFile ?? string.Empty;

        try
        {
            _createResult ??= CallResult<CreateItemResult_t>.Create(OnCreated);
            var call = SteamUGC.CreateItem(new AppId_t(_appId), EWorkshopFileType.k_EWorkshopFileTypeCommunity);
            _createResult.Set(call);
            Stage = UploadStage.Creating;
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or DllNotFoundException
                                          or EntryPointNotFoundException)
        {
            Fail("workshop.error.noSteam");
            return false;
        }
    }

    /// <summary>
    /// Obnoví postup. Volá se jednou za snímek, dokud <see cref="IsRunning"/>.
    ///
    /// <para>Callbacky samotné pumpuje hra (<c>SteamAPI.RunCallbacks</c>);
    /// tady se jen dočte, jak daleko nahrávání je.</para>
    /// </summary>
    public void Update()
    {
        if (Stage != UploadStage.Uploading)
        {
            return;
        }

        try
        {
            var status = SteamUGC.GetItemUpdateProgress(_update, out ulong done, out ulong total);
            Progress = total > 0 ? Math.Clamp(done / (double)total, 0, 1) : 0;

            if (status == EItemUpdateStatus.k_EItemUpdateStatusInvalid && Progress <= 0)
            {
                Progress = 0; // ještě se nerozjelo
            }
        }
        catch (Exception error) when (error is InvalidOperationException or DllNotFoundException
                                          or EntryPointNotFoundException)
        {
            Fail("workshop.error.noSteam");
        }
    }

    private void OnCreated(CreateItemResult_t result, bool ioFailure)
    {
        if (ioFailure || result.m_eResult != EResult.k_EResultOK)
        {
            Fail(ResultKey(ioFailure ? EResult.k_EResultIOFailure : result.m_eResult));
            return;
        }

        PublishedId = result.m_nPublishedFileId.m_PublishedFileId;

        // Steam tohle vrací u PRVNÍ publikace: dokud autor neodsouhlasí
        // pravidla v prohlížeči, položka zůstane skrytá. Není to chyba, ale
        // musí se to říct — jinak bude hledat mod, který nikde není.
        NeedsLegalAgreement = result.m_bUserNeedsToAcceptWorkshopLegalAgreement;

        try
        {
            _update = SteamUGC.StartItemUpdate(new AppId_t(_appId), result.m_nPublishedFileId);
            SteamUGC.SetItemTitle(_update, _title);
            SteamUGC.SetItemDescription(_update, _description);
            SteamUGC.SetItemContent(_update, _folder);
            SteamUGC.SetItemVisibility(_update, ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic);
            SteamUGC.SetItemTags(_update, new[] { "mod" }, false);

            if (_previewFile.Length > 0 && File.Exists(_previewFile))
            {
                SteamUGC.SetItemPreview(_update, _previewFile);
            }

            _submitResult ??= CallResult<SubmitItemUpdateResult_t>.Create(OnSubmitted);
            _submitResult.Set(SteamUGC.SubmitItemUpdate(_update, "CivDle"));
            Stage = UploadStage.Uploading;
        }
        catch (Exception error) when (error is InvalidOperationException or DllNotFoundException
                                          or EntryPointNotFoundException)
        {
            Fail("workshop.error.noSteam");
        }
    }

    private void OnSubmitted(SubmitItemUpdateResult_t result, bool ioFailure)
    {
        if (ioFailure || result.m_eResult != EResult.k_EResultOK)
        {
            Fail(ResultKey(ioFailure ? EResult.k_EResultIOFailure : result.m_eResult));
            return;
        }

        NeedsLegalAgreement |= result.m_bUserNeedsToAcceptWorkshopLegalAgreement;
        Progress = 1;
        Stage = UploadStage.Done;
    }

    /// <summary>
    /// Přeloží steamovský výsledek na lokalizační klíč.
    ///
    /// <para>Vyjmenované jsou jen ty, se kterými autor může něco udělat.
    /// Zbytek dostane obecnou hlášku — číslo chyby by mu neřeklo nic.</para>
    /// </summary>
    public static string ResultKey(EResult result) => result switch
    {
        EResult.k_EResultInsufficientPrivilege => "workshop.error.banned",
        EResult.k_EResultTimeout => "workshop.error.timeout",
        EResult.k_EResultNotLoggedOn => "workshop.error.noSteam",
        EResult.k_EResultLimitExceeded => "workshop.error.quota",
        EResult.k_EResultFileNotFound => "workshop.error.noFolder",
        EResult.k_EResultAccessDenied => "workshop.error.accessDenied",
        _ => "workshop.error.generic",
    };

    private void Fail(string key)
    {
        Error = key;
        Stage = UploadStage.Failed;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _createResult?.Dispose();
        _submitResult?.Dispose();
    }
}
