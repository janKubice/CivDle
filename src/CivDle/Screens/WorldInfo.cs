namespace CivDle.Screens;

/// <summary>Popis vytvořeného světa pro HUD (co hráč zadal v menu nové hry).</summary>
public sealed record WorldInfo(long Seed, string SizeName, string PresetName);
