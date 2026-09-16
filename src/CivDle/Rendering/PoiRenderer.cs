using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Anomálie na mapě: značka, ke které stojí za to poslat výpravu.
///
/// <para>Pulzuje schválně. Anomálie je jediná věc na mapě, kterou hráč
/// <b>nepostavil</b> a která na něj čeká — kdyby jen tiše ležela mezi stromy,
/// nikdo by ji nenašel, a mechanika, na kterou se nepřijde, ve hře není.</para>
///
/// <para>Seznam se nedrží: <see cref="PointOfInterestSystem"/> ho dopočítá
/// z hashe pro právě viditelný výřez. Renderer si tedy nemusí nic pamatovat
/// ani nic invalidovat.</para>
///
/// <para>Vrstva: čistý render nad simulací. Nic nemění.</para>
/// </summary>
public sealed class PoiRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    private static readonly Color Idle = new(214, 178, 255);
    private static readonly Color Target = new(255, 226, 150);

    private readonly SpriteLibrary _sprites;
    private readonly Texture2D _pixel;

    /// <summary>Jeden seznam na celý život — žádná alokace za snímek.</summary>
    private readonly List<PointOfInterest> _visible = new();

    /// <summary>
    /// Kresba pro každý druh anomálie, vytažená jednou při startu.
    ///
    /// <para>Skládat <c>"poi." + id</c> při kreslení by znamenalo řetězec na
    /// každou značku v každém snímku. Tabulka se navíc plní podle indexu
    /// druhu, kterým se anomálie identifikuje i v simulaci — nic se nehledá.</para>
    ///
    /// <para>Druh bez vlastní kresby spadne zpátky na obecnou značku. Nová
    /// anomálie v datech tak nezmizí z mapy jen proto, že se na ni zapomnělo
    /// nakreslit obrázek; hlídá to test pokrytí.</para>
    /// </summary>
    private readonly Texture2D?[] _byKind;

    private float _pulse;

    public PoiRenderer(SpriteLibrary sprites, Texture2D whitePixel, GameContent content)
    {
        _sprites = sprites;
        _pixel = whitePixel;

        var kinds = content.PointsOfInterest.Kinds;
        _byKind = new Texture2D?[kinds.Count];
        for (int i = 0; i < kinds.Count; i++)
        {
            _byKind[i] = sprites.Get($"poi.{kinds[i].Id}");
        }
    }

    public void Update(float dt) => _pulse += dt;

    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        var (min, max) = camera.VisibleWorldBounds();
        simulation.PointsOfInterest.InRange(
            (int)Math.Floor(min.X / TileSize),
            (int)Math.Floor(min.Y / TileSize),
            (int)Math.Ceiling(max.X / TileSize),
            (int)Math.Ceiling(max.Y / TileSize),
            _visible);

        if (_visible.Count == 0 && !simulation.ExpeditionRunning)
        {
            return;
        }

        var generic = _sprites.Get("fx.anomaly");
        float glow = 0.55f + (0.45f * MathF.Sin(_pulse * 2.4f));

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);

        for (int i = 0; i < _visible.Count; i++)
        {
            // Zastavěné zvláštní místo se nekreslí. Značka pod budovou vypadala
            // jako kus kulisy, který se zapomněl smazat — hráč přes oázu
            // postavil a ona tam pořád svítila.
            if (simulation.IsOccupied(_visible[i].X, _visible[i].Y)
                || simulation.HasRoadAt(_visible[i].X, _visible[i].Y))
            {
                continue;
            }

            // Značka podle druhu. Jeden fialový kosočtverec pro všechno říkal
            // jen „něco tu je" — jenže hráč se rozhoduje, kam poslat výpravu,
            // a to je rozhodnutí mezi zasypanou ruinou a spadlým meteoritem.
            //
            // Kresba druhu se kreslí v plné barvě; pulzuje kolem ní obecná
            // značka, aby zůstalo poznat, že tohle místo na hráče čeká.
            Mark(spriteBatch, generic, _visible[i].X, _visible[i].Y, Idle * (glow * 0.5f));

            if (SpriteFor(_visible[i].KindIndex) is { } kind)
            {
                Landmark(spriteBatch, kind, _visible[i].X, _visible[i].Y);
            }
        }

        // Cíl běžící výpravy zůstane vidět, i když je „vybraný": jinak by
        // hráč po vypravení ztratil z mapy jediné místo, na kterém se něco děje.
        if (simulation.ExpeditionRunning)
        {
            var target = simulation.ExpeditionTarget;
            Mark(spriteBatch, generic, target.X, target.Y, Target);
        }

        spriteBatch.End();
    }

    /// <summary>Kresba druhu, nebo <c>null</c> u druhu, který svou nemá.</summary>
    private Texture2D? SpriteFor(int kindIndex) =>
        kindIndex >= 0 && kindIndex < _byKind.Length ? _byKind[kindIndex] : null;

    /// <summary>
    /// Kresba druhu — přes dvě dlaždice a patou na tu svou.
    ///
    /// <para>Do jedné dlaždice se nevejde. Kresby jsou na plátně 32×32 a do
    /// šestnácti pixelů by se zmenšily na polovinu, tedy <b>každý druhý pixel
    /// pryč</b> — z ruiny by zbyly tři nesouvislé kameny. Obecný kosočtverec
    /// to snese, protože je to jeden velký tvar; ruina ani vrak ne.</para>
    ///
    /// <para>Anomálie navíc leží jen na volné zemi, takže větší značka nemá
    /// co přerůst.</para>
    /// </summary>
    private static void Landmark(SpriteBatch spriteBatch, Texture2D sprite, int tileX, int tileY)
    {
        int size = TileSize * 2;
        spriteBatch.Draw(
            sprite,
            new Rectangle(
                (tileX * TileSize) + (TileSize / 2) - (size / 2),
                (tileY * TileSize) + TileSize - size,
                size,
                size),
            Color.White);
    }

    private void Mark(SpriteBatch spriteBatch, Texture2D? sprite, int tileX, int tileY, Color color)
    {
        var bounds = new Rectangle(tileX * TileSize, tileY * TileSize, TileSize, TileSize);
        if (sprite is not null)
        {
            spriteBatch.Draw(sprite, bounds, color);
            return;
        }

        spriteBatch.Draw(_pixel, bounds, color);
    }
}
