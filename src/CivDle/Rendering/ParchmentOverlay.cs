using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Z dálky vypadá svět jako nakreslená mapa, ne jako vyblitá satelitní fotka.
///
/// <para><b>Proč zrovna při oddálení:</b> zblízka hráč vidí domy, stromy
/// a lidi — obraz nese detail. Jak se oddálí, detail zmizí a zbydou barevné
/// plochy; a barevné plochy bez detailu jsou přesně to, co vypadá jako
/// tabulka. Papír tu díru zaplní: zrno, vlákna a teplý tón dají velké ploše
/// texturu a velkému oddálení vlastní <b>záměr</b> místo dojmu, že se hra
/// prostě vzdala.</para>
///
/// <para>Kreslí se <b>násobením</b> přes terén, ne průhledným překryvem:
/// papír je povrch, na kterém mapa leží, takže tmavá vlákna mají ztmavit
/// i mapu. Překryv by položil přes obraz šedou fólii.</para>
///
/// <para>Nastupuje plynule. Skokové přepnutí vzhledu při jednom kroku zoomu
/// vypadá jako chyba, i když je to záměr.</para>
///
/// <para>Vrstva: čistý render, nic nečte ze simulace.</para>
/// </summary>
public sealed class ParchmentOverlay : IDisposable
{
    /// <summary>Hrana textury papíru. Dlaždicově se opakuje, takže stačí malá.</summary>
    private const int TextureSize = 128;

    /// <summary>Od tohohle přiblížení papír začíná prosvítat.</summary>
    public const float FadeInZoom = CityScaleRenderer.ThresholdZoom;

    /// <summary>Pod tímhle přiblížením je papír naplno.</summary>
    public const float FullZoom = FadeInZoom * 0.45f;

    /// <summary>Nejsilnější, co papír udělá. Přes třetinu už je z mapy hnědý hadr.</summary>
    private const float MaxStrength = 0.34f;

    /// <summary>Teplý tón papíru. Násobí se, takže tmavší než bílá znamená „ušpinit".</summary>
    private static readonly Color Paper = new(238, 226, 202);

    private readonly Texture2D _grain;

    public ParchmentOverlay(GraphicsDevice device) => _grain = BuildGrain(device);

    /// <summary>
    /// Jak silný je papírový vzhled při daném přiblížení (0 = vypnuto).
    ///
    /// <para>Veřejné, protože je to jediné netriviální rozhodnutí celého
    /// efektu a dá se ověřit bez grafického zařízení.</para>
    /// </summary>
    public static float StrengthAt(float zoom)
    {
        if (zoom >= FadeInZoom)
        {
            return 0f;
        }

        if (zoom <= FullZoom)
        {
            return 1f;
        }

        float t = (FadeInZoom - zoom) / (FadeInZoom - FullZoom);
        return t * t * (3f - 2f * t); // hladký nástup, ne rovná rampa
    }

    /// <summary>Přetáhne přes mapu papír.</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Viewport viewport)
    {
        float strength = StrengthAt(camera.Zoom);
        if (strength <= 0.01f)
        {
            return;
        }

        // Zrno sedí na OBRAZOVCE, ne ve světě: papír je podklad, na kterém mapa
        // leží, takže se s posunem mapy nemá hýbat. Ve světových souřadnicích
        // by vlákna putovala pod rukou a vypadala jako šum, ne jako papír.
        var source = new Rectangle(0, 0, viewport.Width / 2, viewport.Height / 2);

        float amount = MaxStrength * strength;
        var tint = Color.Lerp(Color.White, Paper, amount);

        spriteBatch.Begin(
            blendState: DayNightCycle.MultiplyBlend,
            samplerState: SamplerState.LinearWrap);
        spriteBatch.Draw(_grain, new Rectangle(0, 0, viewport.Width, viewport.Height), source, tint);
        spriteBatch.End();
    }

    public void Dispose() => _grain.Dispose();

    /// <summary>
    /// Zrno papíru: jemný šum a přes něj delší vodorovná vlákna.
    ///
    /// <para>Vlákna jsou to, co odliší papír od náhodného šumu — ruční papír
    /// má směr. Bílá znamená „nech být", takže se textura dá kreslit
    /// násobením a světlá místa mapu nezmění.</para>
    /// </summary>
    private static Texture2D BuildGrain(GraphicsDevice device)
    {
        var pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                // Zrno: drobné, na každém pixelu jiné.
                float speck = Hash(x, y, 1) * 0.10f;

                // Vlákna: táhnou se vodorovně, takže se vzorkují hrubě v X
                // a jemně v Y. Ta nesouměrnost dělá směr.
                float fibre = Hash(x / 7, y, 2) * 0.14f;

                float darken = speck + fibre;
                pixels[y * TextureSize + x] = new Color(1f - darken, 1f - darken * 0.94f, 1f - darken * 0.82f);
            }
        }

        var texture = new Texture2D(device, TextureSize, TextureSize);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>Deterministický hash 0–1 — papír musí vypadat vždy stejně.</summary>
    private static float Hash(int x, int y, int salt)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + salt * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }
}
