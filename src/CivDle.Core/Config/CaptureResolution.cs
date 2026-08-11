namespace CivDle.Core.Config;

/// <summary>
/// Rozlišení, ve kterém se fotí a natáčí — nezávisle na velikosti okna.
///
/// <para>Proč to je volba a ne konstanta: render jednoho snímku bez LOD ve 4K
/// trvá řádově dýl než v 1080p. Na screenshot do obchodu se to vyplatí, na
/// rychlou fotku na Discord ne. A hlavně: na slabším stroji nemusí render
/// target pro 4K vůbec projít, takže musí existovat cesta dolů.</para>
///
/// <para>Sedí v konfiguraci (ne v renderu), protože je to nastavení hráče,
/// které se ukládá do profilu — stejně jako stupeň detailu.</para>
/// </summary>
public enum CaptureResolution
{
    /// <summary>1920×1080 — rychlé, stačí na Steam screenshoty.</summary>
    Hd1080,

    /// <summary>2560×1440 — výchozí kompromis mezi kvalitou a časem renderu.</summary>
    Qhd1440,

    /// <summary>3840×2160 — na trailer a na obrázky, ze kterých se dá vyřezávat.</summary>
    Uhd4K,
}
