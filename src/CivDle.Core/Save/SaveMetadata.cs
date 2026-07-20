namespace CivDle.Core.Save;

/// <summary>
/// Metadata uložené hry: čím byl svět vygenerován (pro HUD) a kdy se ukládalo
/// (časové razítko je základ budoucího offline progresu, viz tech-stack.md).
/// </summary>
public sealed record SaveMetadata(long Seed, string SizeId, string PresetId, DateTime SavedAtUtc);
