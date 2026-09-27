using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Přírodní jevy na mapě (svety-design.md 5.3): pás bouře, který přechází
/// přes město, kopečky na zasypaných budovách (písek, sníh — barva z dat
/// jevu), voda po okna u budov zaplavených přílivem a jinovatka na budovách,
/// kterým chybí teplo (síť se vzhledem výpadku „frost").
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

    private static readonly Color Frost = new(196, 226, 255);
    private static readonly Color Ice = new(236, 248, 255);
    private static readonly Color Sand = new(222, 190, 128);
    private static readonly Color FloodWater = new(84, 168, 206);
    private static readonly Color Foam = new(236, 248, 250);

    /// <summary>Čím jsou zasypané budovy zasypané (z dat jevu: písek, sníh…) a jeho stín a světlo.</summary>
    private readonly Color _mound;
    private readonly Color _moundShade;
    private readonly Color _moundLight;

    private readonly Texture2D _pixel;
    private readonly GameContent _content;

    /// <summary>Které sítě zamrzají (vzhled výpadku „frost"); prázdné = svět bez mrazu.</summary>
    private readonly bool[] _frostNetworks;
    private readonly bool _anyFrost;

    /// <summary>Indexy budov ve výřezu — jeden seznam na celý život.</summary>
    private readonly List<int> _visible = new();

    private float _time;

    public HazardRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        _content = content;

        // Kopeček má barvu jevu, který zasypává (sníh na Mrazu, písek na Duně).
        var mound = content.Hazards.Hazards.FirstOrDefault(h => h.Burial is not null)?.Burial?.MoundColor;
        _mound = mound is { } color ? new Color(color.R, color.G, color.B) : Sand;
        _moundShade = Color.Lerp(_mound, Color.Black, 0.18f);
        _moundLight = Color.Lerp(_mound, Color.White, 0.3f);
        _frostNetworks = new bool[content.Networks.Count];
        for (int n = 0; n < _frostNetworks.Length; n++)
        {
            _frostNetworks[n] = content.Networks[n].ShortageLook == NetworkTypeDef.FrostLook;
            _anyFrost |= _frostNetworks[n];
        }
    }

    /// <summary>Posune animaci proužků (reálný čas — i v pauze vítr fouká).</summary>
    public void Update(float dt) => _time += dt;

    /// <summary>Zasypané budovy a běžící bouře. Svět bez jevů nestojí nic.</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        if (_content.Hazards.Count == 0 && !_anyFrost)
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
            if (i >= buildings.Length)
            {
                continue;
            }

            if (_anyFrost && buildings[i].Stall == BuildingStall.NetworkShortage)
            {
                DrawFrost(spriteBatch, buildings[i]);
                continue;
            }

            if (buildings[i].DisabledTicks > 0 && buildings[i].DisabledCause == DisableCause.Flood)
            {
                DrawFlooded(spriteBatch, buildings[i]);
                continue;
            }

            if (buildings[i].DisabledTicks <= 0 || buildings[i].DisabledCause != DisableCause.Burial)
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
                spriteBatch.Draw(_pixel, rect, layer == 0 ? _moundShade : _mound);
                spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, Math.Max(1, rect.Width / 2), 1), _moundLight);
            }
        }
    }

    /// <summary>
    /// Budova v přílivu: voda po okna přes spodek budovy a vlnky, které se
    /// pomalu posouvají. Nad vodou budova zůstane vidět — příliv ji jen
    /// vyřadil, nic nezbořil.
    /// </summary>
    private void DrawFlooded(SpriteBatch spriteBatch, in BuildingInstance building)
    {
        var def = _content.Buildings[building.DefIndex];
        int px = building.X * TileSize;
        int py = building.Y * TileSize;
        int width = def.FootprintWidth * TileSize;
        int height = def.FootprintHeight * TileSize;
        int water = Math.Max(4, height * 2 / 5);
        spriteBatch.Draw(_pixel, new Rectangle(px - 1, py + height - water, width + 2, water + 1), FloodWater * 0.8f);

        // Vlnky: krátké světlé čárky, které se po hladině posouvají.
        for (int row = 0; row < 2; row++)
        {
            int y = py + height - water + 1 + row * (water / 2);
            float shift = (_time * 6f + row * 5f + Hash(building.X, building.Y) * 20f) % 8f;
            for (float x = shift - 8f; x < width; x += 8f)
            {
                int wx = px + (int)x;
                int w = Math.Min(3, px + width - wx);
                if (wx >= px && w > 0)
                {
                    spriteBatch.Draw(_pixel, new Rectangle(wx, y, w, 1), Foam * 0.7f);
                }
            }
        }
    }

    /// <summary>
    /// Jinovatka na budově, které chybí teplo: bledě modrý závoj, sněhová
    /// čepice nahoře, rampouchy dole a pár třpytek. Kreslí se jen u budov,
    /// jejichž výpadek způsobila mrznoucí síť — jinak by „bez proudu" na
    /// Domovině vypadalo jako mráz.
    /// </summary>
    private void DrawFrost(SpriteBatch spriteBatch, in BuildingInstance building)
    {
        var def = _content.Buildings[building.DefIndex];
        bool frozenByNetwork = false;
        foreach (var use in def.Networks)
        {
            frozenByNetwork |= use.Demand > 0 && use.NetworkIndex < _frostNetworks.Length && _frostNetworks[use.NetworkIndex];
        }

        if (!frozenByNetwork)
        {
            return;
        }

        int px = building.X * TileSize;
        int py = building.Y * TileSize;
        int width = def.FootprintWidth * TileSize;
        int height = def.FootprintHeight * TileSize;
        spriteBatch.Draw(_pixel, new Rectangle(px, py, width, height), Frost * 0.35f);
        spriteBatch.Draw(_pixel, new Rectangle(px, py, width, 2), Ice * 0.9f);

        // Rampouchy: každé tři pixely jeden, délka z hashe.
        for (int x = 1; x < width - 1; x += 3)
        {
            int length = 2 + (int)(Hash(building.X * 131 + x, building.Y) * 4);
            spriteBatch.Draw(_pixel, new Rectangle(px + x, py + height - 1, 1, length), Ice * 0.8f);
        }

        // Třpytky: pár bílých bodů, které pomalu blikají.
        for (int k = 0; k < 3; k++)
        {
            float h = Hash(building.X * 7 + k, building.Y * 13 + k);
            float twinkle = 0.5f + 0.5f * MathF.Sin(_time * 2.2f + h * 12f);
            int sx = px + 2 + (int)(h * (width - 4));
            int sy = py + 2 + (int)(Hash(building.Y + k, building.X) * (height - 4));
            spriteBatch.Draw(_pixel, new Rectangle(sx, sy, 1, 1), Color.White * twinkle);
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
        spriteBatch.Draw(_pixel, center, null, _mound * 0.18f, rotation, new Vector2(0.5f, 0.5f),
            new Vector2(length, width * 1.4f), SpriteEffects.None, 0f);
        spriteBatch.Draw(_pixel, center, null, _mound * 0.28f, rotation, new Vector2(0.5f, 0.5f),
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
            var color = (i % 3 == 0 ? _moundLight : _moundShade) * 0.55f;
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
