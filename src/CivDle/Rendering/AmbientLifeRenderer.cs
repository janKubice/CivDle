using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Co se ve městě děje, i když hráč nic nedělá: kouř z komínů, chvění vzduchu
/// nad hutěmi a ptáci nad krajinou.
///
/// <para>Proč to vzniklo: hra má chodce, auta i letouny, ale všechny se
/// přestanou kreslit hned, jak hráč odjede kamerou o kousek dál
/// (<see cref="DetailLevel.Creatures"/>). Na běžném pohledu na město tak
/// zůstane úplně nehybný obraz — a nehybný obraz přestane mozek číst jako
/// místo. Tyhle tři vrstvy jsou schválně dělané tak, aby <b>přežily i střední
/// oddálení</b>: jsou to velké měkké tvary, které jsou čitelné i když mají pár
/// pixelů, na rozdíl od jednopixelového chodce.</para>
///
/// <para>Všechno je odvozené z času a polohy — žádný stav, žádné částice,
/// žádné alokace. Kouř nad stovkou budov je stovka sinusovek, ne stovka
/// objektů.</para>
///
/// <para>Vrstva: čistý render, čte jen simulaci.</para>
/// </summary>
public sealed class AmbientLifeRenderer
{
    /// <summary>
    /// Pod tímhle přiblížením přestane stoupat kouř.
    ///
    /// <para>Je to <b>nejnižší možná</b> hodnota — přesně tam, kde se pohled
    /// překlápí do agregátního (<see cref="CityScaleRenderer.ThresholdZoom"/>).
    /// Tím je celý smysl téhle vrstvy: chodci mizí už na 0,9 a mezi 0,5 a 0,9
    /// zůstávala scéna úplně nehybná. Kouř je velký měkký tvar, který je
    /// čitelný i o pár pixelech, tak ať vydrží až dolů.</para>
    /// </summary>
    public const float SmokeZoom = CityScaleRenderer.ThresholdZoom;

    /// <summary>Pod tímhle přiblížením zmizí chvění vzduchu (je to jemný efekt).</summary>
    public const float HeatZoom = 0.75f;

    /// <summary>Pod tímhle přiblížením zmizí ptáci — vydrží stejně daleko jako kouř.</summary>
    public const float BirdZoom = CityScaleRenderer.ThresholdZoom;

    /// <summary>Kolik obláčků má jeden komín v jednom okamžiku.</summary>
    private const int PuffsPerStack = 4;

    /// <summary>Jak vysoko kouř doletí, než se rozplyne (v pixelech).</summary>
    private const float SmokeRise = 26f;

    /// <summary>Velikost oka mřížky, ve které se rozhoduje o hejnech (v dlaždicích).</summary>
    private const int FlockCell = 24;

    /// <summary>Jak často má oko hejno. Vzácnost je smysl věci — ptáci mají být událost.</summary>
    private const int FlockChanceOutOf = 7;

    private static readonly Color Smoke = new(214, 210, 205);
    private static readonly Color Steam = new(226, 232, 238);
    private static readonly Color Bird = new(46, 44, 52);

    private readonly Texture2D _pixel;
    private readonly GameContent _content;

    /// <summary>Čas scény. Jediný stav rendereru.</summary>
    private float _time;

    public AmbientLifeRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        _content = content;
    }

    /// <summary>Posune čas ambientního pohybu (v pauze se nevolá — město stojí).</summary>
    public void Update(float dt) => _time += dt;

    /// <summary>Nakreslí kouř, chvění nad hutěmi a ptáky.</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        bool smoke = camera.Zoom >= DetailLevel.Scale(SmokeZoom);
        bool birds = camera.Zoom >= DetailLevel.Scale(BirdZoom);
        if (!smoke && !birds)
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);

        if (smoke)
        {
            DrawChimneys(spriteBatch, simulation, min, max, camera.Zoom >= DetailLevel.Scale(HeatZoom));
        }

        if (birds)
        {
            DrawBirds(spriteBatch, min, max);
        }

        spriteBatch.End();
    }

    /// <summary>Kouř nad pracujícími provozy a chvění vzduchu nad elektrárnami.</summary>
    private void DrawChimneys(
        SpriteBatch spriteBatch, Simulation simulation, Vector2 min, Vector2 max, bool withHeat)
    {
        const int tileSize = TerrainRenderer.TileSize;
        var buildings = simulation.Buildings;

        for (int i = 0; i < buildings.Length; i++)
        {
            ref readonly var building = ref buildings[i];
            var def = _content.Buildings[building.DefIndex];
            int x = building.X * tileSize;
            int y = building.Y * tileSize;
            int width = def.FootprintWidth * tileSize;
            int height = def.FootprintHeight * tileSize;
            if (x + width < min.X - 32 || x > max.X + 32 || y + height < min.Y - 48 || y > max.Y + 32)
            {
                continue;
            }

            // Stojící provoz nekouří. Je to drobnost, ale právě z ní hráč pozná
            // živou čtvrť od mrtvé dřív, než se podívá na odznaky.
            if (!building.IsComplete || building.Stall != BuildingStall.None)
            {
                continue;
            }

            switch (StackKind(def, building.X, building.Y))
            {
                case Stack.Works:
                    DrawSmokeColumn(spriteBatch, x + width / 2, y, Smoke, 1f);
                    if (withHeat)
                    {
                        DrawHeatHaze(spriteBatch, x, y, width);
                    }

                    break;

                case Stack.Home:
                    // Domácí komín kouří slabě — je to obytná čtvrť, ne huť.
                    DrawSmokeColumn(spriteBatch, x + width / 4, y, Smoke, 0.45f);
                    break;
            }
        }
    }

    /// <summary>
    /// Kdo kouří a jak. Rozhoduje kategorie z dat, ne seznam ID v kódu —
    /// nová budova se tím zařadí sama.
    /// </summary>
    public static Stack StackKind(BuildingDef def, int tileX, int tileY) => def.Category switch
    {
        "production" when def.Recipe is not null => Stack.Works,
        "power" => Stack.Works,
        "housing" when BuildingVariation.For(tileX, tileY, 0).Extra == BuildingExtra.Chimney => Stack.Home,
        _ => Stack.None,
    };

    /// <summary>Co budově stoupá nad střechu.</summary>
    public enum Stack
    {
        /// <summary>Nic — sklad, monument, pole.</summary>
        None,

        /// <summary>Provoz nebo elektrárna — pořádný sloup kouře.</summary>
        Works,

        /// <summary>Chalupa s komínem — obláček.</summary>
        Home,
    }

    /// <summary>
    /// Sloup kouře nad komínem. Obláčky jsou rozprostřené po dráze, takže
    /// v každém okamžiku jeden vystupuje a jiný se nahoře rozplývá — z čtyř
    /// obdélníčků je tím spojitý pohyb.
    /// </summary>
    private void DrawSmokeColumn(SpriteBatch spriteBatch, int stackX, int topY, Color color, float strength)
    {
        float phase = (stackX * 0.37f + topY * 0.11f) % MathF.Tau;

        for (int p = 0; p < PuffsPerStack; p++)
        {
            // Každý obláček je na dráze jinde; frac() z něj udělá smyčku.
            float rise = (_time * 0.42f + p / (float)PuffsPerStack + phase * 0.05f) % 1f;
            float drift = AmbientWind.Drift(_time, rise, phase + p);

            // Nahoře je kouř větší a průhlednější — takhle se rozplývá.
            int size = 2 + (int)(rise * 4f);
            float alpha = strength * 0.42f * (1f - rise) * MathF.Min(1f, rise * 6f);
            if (alpha <= 0.01f)
            {
                continue;
            }

            int px = stackX + (int)drift - size / 2;
            int py = topY - 2 - (int)(rise * SmokeRise);
            spriteBatch.Draw(_pixel, new Rectangle(px, py, size, size), color * alpha);
        }
    }

    /// <summary>
    /// Chvění horkého vzduchu nad hutí: pár svislých proužků, které se vlní.
    /// Je to jemné schválně — má to jít poznat koutkem oka, ne přečíst.
    /// </summary>
    private void DrawHeatHaze(SpriteBatch spriteBatch, int x, int topY, int width)
    {
        for (int i = 0; i < 3; i++)
        {
            float phase = i * 2.1f + x * 0.05f;
            int wobble = (int)MathF.Round(MathF.Sin(_time * 2.6f + phase) * 2f);
            int stripeX = x + 3 + i * Math.Max(3, (width - 6) / 3);
            spriteBatch.Draw(
                _pixel,
                new Rectangle(stripeX + wobble, topY - 8, 1, 8),
                Steam * 0.16f);
        }
    }

    /// <summary>
    /// Ptáci. Nejsou to entity: mřížka nad světem rozhodne, ve kterých okách
    /// hejno je, a hejno pak krouží po deterministické dráze. Nic se
    /// nesimuluje, nic se nealokuje a nikam to neuletí.
    /// </summary>
    private void DrawBirds(SpriteBatch spriteBatch, Vector2 min, Vector2 max)
    {
        const int tileSize = TerrainRenderer.TileSize;
        int cellSize = FlockCell * tileSize;
        int startX = (int)MathF.Floor(min.X / cellSize);
        int startY = (int)MathF.Floor(min.Y / cellSize);
        int endX = (int)MathF.Ceiling(max.X / cellSize);
        int endY = (int)MathF.Ceiling(max.Y / cellSize);

        for (int cy = startY; cy <= endY; cy++)
        {
            for (int cx = startX; cx <= endX; cx++)
            {
                ulong h = Hash(cx, cy);
                if (h % FlockChanceOutOf != 0)
                {
                    continue;
                }

                // Hejno krouží kolem středu oka. Poloměr a rychlost se liší,
                // ať dvě hejna na obrazovce nevypadají jako kopie.
                float radius = cellSize * (0.18f + (h >> 8 & 0xFF) / 255f * 0.2f);
                float speed = 0.25f + (h >> 16 & 0xFF) / 255f * 0.3f;
                float angle = _time * speed + (h >> 24 & 0xFF) / 255f * MathF.Tau;
                float centerX = cx * cellSize + cellSize * 0.5f;
                float centerY = cy * cellSize + cellSize * 0.5f;

                for (int b = 0; b < 3; b++)
                {
                    float a = angle + b * 0.22f;
                    int bx = (int)(centerX + MathF.Cos(a) * radius);
                    int by = (int)(centerY + MathF.Sin(a) * radius * 0.55f);

                    // Dvě čárky do „V". Křídla se mávají podle fáze letu.
                    int flap = MathF.Sin(_time * 6f + b) > 0f ? 1 : 0;
                    spriteBatch.Draw(_pixel, new Rectangle(bx - 2, by - flap, 2, 1), Bird * 0.65f);
                    spriteBatch.Draw(_pixel, new Rectangle(bx, by - flap, 2, 1), Bird * 0.65f);
                }
            }
        }
    }

    private static ulong Hash(int x, int y)
    {
        ulong h = (ulong)(uint)x * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)y * 0xBF58476D1CE4E5B9UL;
        h = (h ^ (h >> 30)) * 0xBF58476D1CE4E5B9UL;
        h = (h ^ (h >> 27)) * 0x94D049BB133111EBUL;
        return h ^ (h >> 31);
    }
}
