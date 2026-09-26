using CivDle.Core.Sim;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Agregátní pohled na měřítko města při velkém oddálení („wow" moment
/// z game-feel-wow.md): místo drobných jednotlivých budov hustota zástavby
/// a velké číslo populace. Populace je agregát (viz CLAUDE.md), ne miliony
/// jednotlivců.
///
/// <para>Samotnou hustotu kreslí <see cref="DensityMap"/> — upečená do textur,
/// jeden draw call na kus mapy. Tahle třída drží to ostatní: kdy se přepnout
/// a co k tomu napsat. Dřív dělala obojí a při velkoměstě to znamenalo tisíce
/// obdélníčků a přestavěný slovník každý snímek.</para>
/// </summary>
public sealed class CityScaleRenderer : IDisposable
{
    /// <summary>Pod tímto zoomem se přepne z jednotlivých budov na agregátní hustotu.</summary>
    public const float ThresholdZoom = 0.5f;

    private readonly Texture2D _pixel;
    private readonly SpriteFontBase _font;
    private readonly DensityMap _density;

    public CityScaleRenderer(
        Texture2D whitePixel, SpriteFontBase font, GraphicsDevice device,
        CivDle.Core.Content.GameContent? content = null, Sprites.SpriteLibrary? sprites = null)
    {
        _pixel = whitePixel;
        _font = font;
        _content = content;
        _sprites = sprites;
        _density = new DensityMap(device, content?.Districts);
    }

    private readonly CivDle.Core.Content.GameContent? _content;
    private readonly Sprites.SpriteLibrary? _sprites;

    /// <summary>Upečená mapa hustoty — kvůli diagnostice a testům.</summary>
    public DensityMap Density => _density;

    /// <param name="nightFactor">
    /// Hloubka noci 0–1. V noci se z hustoty stane <b>světelná mapa</b>: město
    /// není teplá skvrna na krajině, ale souhvězdí světel v tmavé zemi.
    /// Je to týž údaj nakreslený jinak — a je to ten obraz, kvůli kterému se
    /// v idle hře oddaluje.
    /// </param>
    public void Draw(
        SpriteBatch spriteBatch, Viewport viewport, Camera2D camera, Simulation simulation, float nightFactor = 0f)
    {
        _density.Draw(spriteBatch, camera, simulation, nightFactor);
        DrawLandmarks(spriteBatch, viewport, camera, simulation);
        DrawPopulation(spriteBatch, viewport, simulation);
    }

    /// <summary>Velikost ikony landmarku na obrazovce (px) — stejná při každém oddálení.</summary>
    private const int LandmarkIconSize = 26;

    /// <summary>Po kolika snímcích se nejdřív smí přepočítat seznam landmarků.</summary>
    private const int LandmarkRefreshFrames = 30;

    private readonly List<(int DefIndex, Vector2 World)> _landmarks = new();
    private long _landmarksRevision = -1;
    private long _frame;
    private long _landmarksFrame = -LandmarkRefreshFrames;

    /// <summary>
    /// Pomníky, divy a megastruktury jako ikony nad mapou z výšky (endgame.md,
    /// B4). Při oddálení se budovy nekreslí vůbec — a právě ty, kterými se
    /// hráč chlubí, by z mapy zmizely první. Ikona má pevnou velikost na
    /// obrazovce, ať je čitelná při každém oddálení.
    /// </summary>
    private void DrawLandmarks(SpriteBatch spriteBatch, Viewport viewport, Camera2D camera, Simulation simulation)
    {
        if (_content is null || _sprites is null)
        {
            return;
        }

        _frame++;
        if (simulation.BuildingRevision != _landmarksRevision && _frame - _landmarksFrame >= LandmarkRefreshFrames)
        {
            RebuildLandmarks(simulation);
        }

        if (_landmarks.Count == 0)
        {
            return;
        }

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        foreach (var (defIndex, world) in _landmarks)
        {
            var screen = camera.WorldToScreen(world);
            if (screen.X < -LandmarkIconSize || screen.Y < -LandmarkIconSize
                || screen.X > viewport.Width + LandmarkIconSize || screen.Y > viewport.Height + LandmarkIconSize)
            {
                continue;
            }

            var sprite = _sprites.Get($"building.{_content.Buildings[defIndex].Id}");
            if (sprite is null)
            {
                continue;
            }

            int height = LandmarkIconSize * sprite.Height / Math.Max(1, sprite.Width);
            var rect = new Rectangle((int)screen.X - LandmarkIconSize / 2, (int)screen.Y - height + LandmarkIconSize / 3, LandmarkIconSize, height);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X + 2, rect.Bottom - 3, rect.Width - 4, 4), Color.Black * 0.35f);
            spriteBatch.Draw(sprite, rect, Color.White);
        }

        spriteBatch.End();
    }

    private void RebuildLandmarks(Simulation simulation)
    {
        _landmarksRevision = simulation.BuildingRevision;
        _landmarksFrame = _frame;
        _landmarks.Clear();
        var buildings = simulation.Buildings;
        const int tile = TerrainRenderer.TileSize;
        for (int i = 0; i < buildings.Length; i++)
        {
            var def = _content!.Buildings[buildings[i].DefIndex];
            if (!buildings[i].IsComplete || def.Category is not ("monument" or "megastructure"))
            {
                continue;
            }

            _landmarks.Add((buildings[i].DefIndex, new Vector2(
                (buildings[i].X + def.FootprintWidth * 0.5f) * tile,
                (buildings[i].Y + def.FootprintHeight * 0.5f) * tile)));
        }
    }

    /// <summary>Velké číslo populace nahoře uprostřed — „koukni, jak je to velké".</summary>
    private void DrawPopulation(SpriteBatch spriteBatch, Viewport viewport, Simulation simulation)
    {
        string text = CivDle.Core.Numbers.Format(simulation.Population);
        var size = _font.MeasureString(text);
        var position = new Vector2((viewport.Width - size.X) * 0.5f, viewport.Height * 0.16f);

        spriteBatch.Begin();
        spriteBatch.Draw(_pixel,
            new Rectangle((int)(position.X - 18), (int)(position.Y - 10), (int)size.X + 36, (int)size.Y + 20),
            new Color(12, 16, 22) * 0.7f);
        spriteBatch.DrawString(_font, text, position + new Vector2(2f, 2f), Color.Black * 0.6f);
        spriteBatch.DrawString(_font, text, position, new Color(255, 224, 168));
        spriteBatch.End();
    }

    public void Dispose() => _density.Dispose();
}
