using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Láva na mapě (Výheň, svety-design.md 4.4 a 5.3): žhnoucí průduchy,
/// tekoucí láva s jasnou čelní vlnou, chladnoucí kůra, která pomalu tmavne,
/// a — když ve městě stojí seismická stanice — čárkovaná dráha příští lávy.
/// Hráč tak vidí, kudy láva poteče, dřív než vybuchne.
///
/// <para>Kreslí se pod budovami (láva teče mezi nimi; zalitou budovu má
/// zvlášť <see cref="HazardRenderer"/>). Bez alokací za snímek: dráhy drží
/// jádro, jiskření je hash dlaždice a času.</para>
///
/// <para>Vrstva: render. Ze simulace jen čte (dráhy lávy, biom průduchu).</para>
/// </summary>
public sealed class LavaRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    /// <summary>Jak dlouho ztuhlá kůra ještě žhne (herní sekundy).</summary>
    private const double CoolSeconds = 90;

    /// <summary>Nad tolika dlaždicemi ve výřezu se průduchy nehledají (velké oddálení).</summary>
    private const int VentScanBudget = 40_000;

    private static readonly Color Lava = new(255, 104, 26);
    private static readonly Color LavaHot = new(255, 214, 96);
    private static readonly Color Ember = new(255, 150, 60);
    private static readonly Color Forecast = new(255, 150, 60);

    private readonly Texture2D _pixel;
    private readonly int _vent;
    private float _time;

    public LavaRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        int eruption = content.Hazards.EruptionIndex;
        _vent = eruption >= 0 ? content.Hazards.Hazards[eruption].Eruption!.VentBiomeIndex : -1;
    }

    /// <summary>Posune žhnutí a pochod čárek (reálný čas).</summary>
    public void Update(float dt) => _time += dt;

    /// <summary>Nakreslí lávu ve výřezu; svět bez erupcí nestojí nic.</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        if (_vent < 0)
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        DrawVents(spriteBatch, simulation, min, max);
        DrawCooling(spriteBatch, simulation);

        var (path, front) = simulation.ActiveLava;
        if (front >= 0)
        {
            DrawFlow(spriteBatch, path, front);
        }
        else if (simulation.ForecastsHazards)
        {
            DrawForecast(spriteBatch, simulation.PredictedLavaPath);
        }

        spriteBatch.End();
    }

    /// <summary>Průduchy: tmavý kráter s dýchající žhnoucí tečkou.</summary>
    private void DrawVents(SpriteBatch spriteBatch, Simulation simulation, Vector2 min, Vector2 max)
    {
        int fromX = (int)Math.Floor(min.X / TileSize);
        int fromY = (int)Math.Floor(min.Y / TileSize);
        int toX = (int)Math.Ceiling(max.X / TileSize);
        int toY = (int)Math.Ceiling(max.Y / TileSize);
        if ((long)(toX - fromX + 1) * (toY - fromY + 1) > VentScanBudget)
        {
            return;
        }

        for (int y = fromY; y <= toY; y++)
        {
            for (int x = fromX; x <= toX; x++)
            {
                if (simulation.BiomeAt(x, y) != _vent)
                {
                    continue;
                }

                float pulse = 0.6f + 0.4f * MathF.Sin(_time * 2.1f + (x * 7 + y * 13) * 0.3f);
                int inset = TileSize / 4;
                spriteBatch.Draw(_pixel, new Rectangle(x * TileSize + inset, y * TileSize + inset, TileSize - 2 * inset, TileSize - 2 * inset),
                    Lava * pulse);
                spriteBatch.Draw(_pixel, new Rectangle(x * TileSize + TileSize / 2 - 1, y * TileSize + TileSize / 2 - 1, 2, 2), LavaHot * pulse);
            }
        }
    }

    /// <summary>Tekoucí láva: tělo dráhy až k čelu, čelo jasnější, v těle jiskry.</summary>
    private void DrawFlow(SpriteBatch spriteBatch, IReadOnlyList<long> path, int front)
    {
        for (int i = 1; i <= front && i < path.Count; i++)
        {
            int x = TileKey.X(path[i]);
            int y = TileKey.Y(path[i]);
            var rect = new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize);
            spriteBatch.Draw(_pixel, rect, i >= front - 1 ? LavaHot : Lava);
            DrawSparks(spriteBatch, x, y, 1f);
        }
    }

    /// <summary>Ztuhlá kůra dohasíná: oranžová, která během minuty a půl vybledne do černé.</summary>
    private void DrawCooling(SpriteBatch spriteBatch, Simulation simulation)
    {
        var (path, cooledAt) = simulation.CoolingLava;
        double age = simulation.TickCount / Simulation.TicksPerSecond - cooledAt;
        if (path.Count == 0 || age < 0 || age > CoolSeconds)
        {
            return;
        }

        float heat = 1f - (float)(age / CoolSeconds);
        for (int i = 1; i < path.Count; i++)
        {
            int x = TileKey.X(path[i]);
            int y = TileKey.Y(path[i]);
            spriteBatch.Draw(_pixel, new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize), Lava * (0.45f * heat));
            DrawSparks(spriteBatch, x, y, heat);
        }
    }

    /// <summary>Předpověď: čárky po středu dráhy, které pomalu pochodují od průduchu.</summary>
    private void DrawForecast(SpriteBatch spriteBatch, IReadOnlyList<long> path)
    {
        int march = (int)(_time * 3f);
        for (int i = 1; i < path.Count; i++)
        {
            if ((i + march) % 3 == 0)
            {
                continue; // mezera mezi čárkami
            }

            int x = TileKey.X(path[i]);
            int y = TileKey.Y(path[i]);
            int size = TileSize / 3;
            spriteBatch.Draw(_pixel, new Rectangle(x * TileSize + (TileSize - size) / 2, y * TileSize + (TileSize - size) / 2, size, size),
                Forecast * 0.75f);
        }
    }

    /// <summary>Pár jisker v dlaždici; poloha je hash dlaždice, blikání čas.</summary>
    private void DrawSparks(SpriteBatch spriteBatch, int x, int y, float strength)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177;
        for (int k = 0; k < 2; k++)
        {
            float blink = 0.5f + 0.5f * MathF.Sin(_time * 4f + ((h >> (k * 8)) & 0xFF) * 0.1f);
            int sx = x * TileSize + 1 + (int)((h >> (k * 5)) % (uint)Math.Max(1, TileSize - 2));
            int sy = y * TileSize + 1 + (int)((h >> (k * 7 + 3)) % (uint)Math.Max(1, TileSize - 2));
            spriteBatch.Draw(_pixel, new Rectangle(sx, sy, 1, 1), Ember * (blink * strength));
        }
    }
}
