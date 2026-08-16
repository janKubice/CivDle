namespace CivDle.Capture;

/// <summary>
/// Jeden záběr do traileru.
///
/// <para>Renderuje se <b>po snímcích a mimo reálný čas</b>: záběr dostane pořadové
/// číslo snímku, ne uplynulý čas. Kdyby se řídil hodinami, běželo by výsledné
/// video podle toho, jak zrovna stíhal počítač — a při plném detailu bez LOD
/// trvá jeden snímek klidně půl vteřiny.</para>
///
/// <para>Kam se kreslí, záběr neřeší; render target i ukládání má na starosti
/// <see cref="TrailerDirector"/>.</para>
/// </summary>
internal interface ITrailerShot : IDisposable
{
    /// <summary>Název složky se sekvencí — je vidět v editoru videa, ať je poznat.</summary>
    string Name { get; }

    /// <summary>Kolik snímků záběr má.</summary>
    int FrameCount { get; }

    /// <summary>Vykreslí snímek do právě nastaveného render targetu.</summary>
    void DrawFrame(int frameIndex);
}
