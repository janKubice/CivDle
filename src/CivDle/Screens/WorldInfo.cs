namespace CivDle.Screens;

/// <summary>
/// Popis vytvořeného světa pro HUD. Nese ID, ne přeložená jména — překlad
/// se dělá až při zobrazení, aby HUD reagoval na změnu jazyka.
/// </summary>
public sealed record WorldInfo(long Seed, string SizeId, string PresetId);
