using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Přírodní jevy na mapě (svety-design.md 5.3): pás písečné bouře, který
/// přechází přes město, a kopečky písku na zasypaných budovách.
///
/// <para>„Mechanika je vidět": hráč má z mapy poznat, odkud bouře jde a co
/// zasypala, dřív než si přečte hlášku. Zasypaná budova má kopeček písku
/// (návrh: „má kopeček písku a nekouří") — ne červené šrafování útoku.</para>
///
/// <para>Vrstva: čistý render, ze simulace jen čte (<see cref="Simulation.CurrentHazard"/>
/// a stav budov). Bez alokací za snímek: pás je jeden otočený obdélník
/// a pár set proužků, jejichž polohy jsou hash indexu a času.</para>
/// </summary>
public sealed class HazardRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    /// <summary>Kolik proužků písku nese pás.</summary>
    private const int Streaks = 260;

    private static readonly Color Sand = new(222, 190, 128);
    private static readonly Color SandShade = new(186, 150, 96);
    private static readonly Color SandLight = new(240, 216, 164);

    private readonly Texture2D _pixel;
    private readonly GameContent _content;

    /// <summary>Indexy budov ve výřezu — jeden seznam na celý život.</summary>
    private readonly List<int> _visible = new();

    private float _time;

    public HazardRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        _content = content;
    }

    /// <summary>Posune animaci proužků (reálný čas — i v pauze vítr fouká).</summary>
    public void Update(float dt) => _time += dt;

    /// <summary>Zasypané budovy a běžící bouře. Svět bez jevů nestojí nic.</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        if (_content.Hazards.Count == 0)
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        DrawBuried(spriteBatch, simulation, min, max);

        var view = simulation.CurrentHazard;
        if (view.Phase == HazardPhase.Active && view.Radius > 0)
        {
            DrawBand(spriteBatch, view);
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Kopeček písku přes spodek budovy: tři vrstvy, každá užší, se světlou
    /// hranou ze strany slunce. Přes index budov — hledat zasypané průchodem
    /// celého města by stálo tolik co vykreslit ho znovu.
    /// </summary>
    private void DrawBuried(SpriteBatch spriteBatch, Simulation simulation, Vector2 min, Vector2 max)
    {
        var buildings = simulation.Buildings;
        simulation.BuildingsIn(
            (int)Math.Floor(min.X / TileSize) - 1,
            (int)Math.Floor(min.Y / TileSize) - 1,
            (int)Math.Ceiling(max.X / TileSize) + 1,
            (int)Math.Ceiling(max.Y / TileSize) + 1,
            _visible);

        for (int slot = 0; slot < _visible.Count; slot++)
        {
            int i = _visible[slot];
            if (i >= buildings.Length || buildings[i].DisabledTicks <= 0 || buildings[i].DisabledCause != DisableCause.Burial)
            {
                continue;
            }

            var def = _content.Buildings[buildings[i].DefIndex];
            int px = buildings[i].X * TileSize;
            int py = buildings[i].Y * TileSize;
            int width = def.FootprintWidth * TileSize;
            int height = def.FootprintHeight * TileSize;
            int mound = Math.Max(6, height * 3 / 5);
            int layers = 3;
            for (int layer = 0; layer < layers; layer++)
            {
                int inset = width * layer / 7;
                int layerHeight = mound * (layers - layer) / layers;
                var rect = new Rectangle(px + inset, py + height - layerHeight, width - 2 * inset, layerHeight);
                spriteBatch.Draw(_pixel, rect, layer == 0 ? SandShade : Sand);
                spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, Math.Max(1, rect.Width / 2), 1), SandLight);
            }
        }
    }

    /// <summary>
    /// Pás bouře: průsvitný pruh kolmo na směr pohybu a v něm proužky písku
    /// nesené větrem. Poloha pásu je z jádra (stejná, jaká zasypává).
    /// </summary>
    private void DrawBand(SpriteBatch spriteBatch, in HazardView view)
    {
        var (moveX, moveY) = view.Movement;
        var move = new Vector2((float)moveX, (float)moveY);
        var across = new Vector2(-move.Y, move.X);
        var center = new Vector2(view.CenterX + 0.5f, view.CenterY + 0.5f) * TileSize
            + move * (float)(view.BandOffset * TileSize);
        float length = (float)(view.Radius * 2.2 * TileSize);
        float width = view.BandTiles * TileSize;
        float rotation = MathF.Atan2(across.Y, across.X);

        // Tělo pásu: dvě vrstvy, ať má měkký okraj.
        spriteBatch.Draw(_pixel, center, null, Sand * 0.18f, rotation, new Vector2(0.5f, 0.5f),
            new Vector2(length, width * 1.4f), SpriteEffects.None, 0f);
        spriteBatch.Draw(_pixel, center, null, Sand * 0.28f, rotation, new Vector2(0.5f, 0.5f),
            new Vector2(length, width), SpriteEffects.None, 0f);

        // Proužky: poloha je hash indexu, posun po větru je čas — žádný stav.
        float moveRotation = MathF.Atan2(move.Y, move.X);
        for (int i = 0; i < Streaks; i++)
        {
            float along = Hash(i, 1) - 0.5f;
            float depth = Hash(i, 2) - 0.5f;
            float speed = 0.6f + Hash(i, 3);
            float drift = (_time * speed * 0.35f + Hash(i, 4)) % 1f - 0.5f;
            var position = center + across * (along * length) + move * ((depth + drift) * width);
            int streak = 3 + (int)(Hash(i, 5) * 9);
            var color = (i % 3 == 0 ? SandLight : SandShade) * 0.55f;
            spriteBatch.Draw(_pixel, position, null, color, moveRotation, Vector2.Zero,
                new Vector2(streak, 1), SpriteEffects.None, 0f);
        }
    }

    /// <summary>Deterministické číslo 0–1 z indexu proužku.</summary>
    private static float Hash(int index, int salt)
    {
        uint h = (uint)(index * 374761393 + salt * 668265263);
        h = (h ^ (h >> 13)) * 1274126177;
        return (h ^ (h >> 16)) / (float)uint.MaxValue;
    }
}
