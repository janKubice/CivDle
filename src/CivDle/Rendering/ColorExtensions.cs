using CivDle.Core.Content;
using Microsoft.Xna.Framework;

namespace CivDle.Rendering;

/// <summary>Převod barev z Core (bez MonoGame) na XNA <see cref="Color"/>.</summary>
public static class ColorExtensions
{
    public static Color ToXna(this RgbColor color) => new(color.R, color.G, color.B);
}
