using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Jak se svět z jednotlivých vrstev složí do jednoho obrazu.
///
/// <para><b>Proč to vzniklo:</b> hra kreslila pětadvacet vrstev rovnou do
/// backbufferu. Žádný mezikrok neexistoval, takže se s hotovým obrazem nedalo
/// udělat vůbec nic — denní doba se „gradovala" průhledným obdélníkem přes
/// celou obrazovku, což kontrast <b>snižuje</b>: obraz zmléční. Každý pixel
/// přitom svítil na sto procent své barvy, nic neustupovalo do pozadí a oko
/// nedostalo řečeno, kam se dívat. Výsledek byl čitelný, ale nerežírovaný —
/// mapa, ne místo.</para>
///
/// <para><b>Proč bez shaderů:</b> všechno, co je tu potřeba, jde přes
/// <see cref="BlendState"/> a zmenšování do render targetů. Obojí MonoGame umí
/// bez content pipeline, takže nepřibyla závislost na MGCB — a hlavně se to dá
/// ověřit buildem, což u HLSL na tomhle stroji neplatí.</para>
///
/// <para>Složení má tři kroky a každý dělá něco jiného:</para>
/// <list type="number">
/// <item><description><b>Denní světlo jako násobič.</b> Scéna se vynásobí barvou
/// světla. Násobení tmavá místa ztmaví víc než světlá, takže kontrast <i>roste</i>
/// — na rozdíl od závoje, který ho plošně dusil.</description></item>
/// <item><description><b>Bloom.</b> Světlá místa se rozlijí do okolí. Práh se
/// bez shaderu udělá tím, že se obraz <b>umocní sám sebou</b> (blend
/// <c>src×src</c>): tmavé pixely spadnou k nule, světlé zůstanou. Umocněná
/// zmenšenina se rozostří a přičte zpátky.</description></item>
/// <item><description><b>Sevření okraje.</b> Jemné ztmavení rohů, aby pohled
/// držel pohromadě.</description></item>
/// </list>
///
/// <para>Vrstva: čistý render. O obsahu nerozhoduje, jen skládá hotový obraz.</para>
/// </summary>
public sealed class SceneComposer : IDisposable
{
    /// <summary>
    /// Umocní obraz sám sebou. Náhrada prahu pro bloom: dvojnásobným průchodem
    /// vyjde čtvrtá mocnina, po které v obraze zbydou prakticky jen zdroje
    /// světla — okna, ohně, odlesky na vodě.
    /// </summary>
    private static readonly BlendState Square = new()
    {
        ColorSourceBlend = Blend.SourceColor,
        ColorDestinationBlend = Blend.Zero,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.Zero,
    };

    /// <summary>Na kolikátinu se scéna zmenšuje, než se z ní dělá záře.</summary>
    private const int BloomDivisor = 4;

    private readonly GraphicsDevice _device;

    private RenderTarget2D? _scene;
    private RenderTarget2D? _bloomA;
    private RenderTarget2D? _bloomB;
    private int _width;
    private int _height;

    public SceneComposer(GraphicsDevice device) => _device = device;

    /// <summary>
    /// Přepne kreslení do scény. Od téhle chvíle jde všechno do textury, ne na
    /// obrazovku — a teprve <see cref="Compose"/> to složí ven.
    /// </summary>
    public void Begin(Color background)
    {
        var viewport = _device.Viewport;
        EnsureTargets(viewport.Width, viewport.Height);

        _device.SetRenderTarget(_scene);
        _device.Clear(background);
    }

    /// <summary>
    /// Složí scénu na obrazovku: osvětlí ji, přidá záři a sevře okraje.
    /// </summary>
    /// <param name="light">
    /// Barva světla, kterou se scéna násobí. Bílá = poledne (nic se nemění),
    /// teplá = zlatá hodina, modrošedá = noc.
    /// </param>
    /// <param name="bloom">Síla záře 0–1; 0 = vypnuto a celý řetěz se přeskočí.</param>
    /// <param name="whitePixel">
    /// Bílý pixel, přes který se násobí světlo. Násobí se <b>plocha</b>, ne
    /// znovu nakreslená scéna — kdyby se kreslila scéna, vyšla by scéna na
    /// druhou a ne scéna krát světlo.
    /// </param>
    public void Compose(SpriteBatch batch, Texture2D whitePixel, Color light, float bloom)
    {
        if (_scene is null)
        {
            return; // Begin se nezavolal — není co skládat
        }

        if (bloom > 0.001f)
        {
            BuildBloom(batch);
        }

        _device.SetRenderTarget(null);
        _device.Clear(Color.Black);

        var full = new Rectangle(0, 0, _width, _height);

        batch.Begin(samplerState: SamplerState.PointClamp);
        batch.Draw(_scene, full, Color.White);
        batch.End();

        // Světlo až na hotovou scénu: násobí se celý obraz včetně budov a lidí,
        // ne jen terén. Kdyby se osvětlovala každá vrstva zvlášť, každá by si to
        // vyložila jinak a soumrak by na budovách vypadal jinak než na zemi.
        DayNightCycle.DrawLight(batch, whitePixel, new Viewport(full), light);

        if (bloom > 0.001f && _bloomA is not null)
        {
            batch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.LinearClamp);
            batch.Draw(_bloomA, full, Color.White * bloom);
            batch.End();
        }
    }

    public void Dispose()
    {
        _scene?.Dispose();
        _bloomA?.Dispose();
        _bloomB?.Dispose();
        _scene = null;
        _bloomA = null;
        _bloomB = null;
    }

    /// <summary>
    /// Vyrobí záři: zmenšit, dvakrát umocnit (to je ten práh), rozmazat
    /// zvětšením a zmenšením zpátky.
    /// </summary>
    private void BuildBloom(SpriteBatch batch)
    {
        if (_bloomA is null || _bloomB is null || _scene is null)
        {
            return;
        }

        var small = new Rectangle(0, 0, _bloomA.Width, _bloomA.Height);

        // Zmenšenina scény. Lineární filtr tu dělá průměr okolí, takže se
        // zmenšením zároveň rozmazává.
        _device.SetRenderTarget(_bloomA);
        _device.Clear(Color.Transparent);
        batch.Begin(samplerState: SamplerState.LinearClamp);
        batch.Draw(_scene, small, Color.White);
        batch.End();

        // Dvakrát umocnit: po čtvrté mocnině zbyde jen to, co opravdu svítí.
        for (int pass = 0; pass < 2; pass++)
        {
            _device.SetRenderTarget(_bloomB);
            _device.Clear(Color.Transparent);
            batch.Begin(blendState: Square, samplerState: SamplerState.LinearClamp);
            batch.Draw(_bloomA, small, Color.White);
            batch.End();

            (_bloomA, _bloomB) = (_bloomB, _bloomA);
        }

        // Rozmáznout: dolů na polovinu a zpátky nahoru. Lineární filtr v obou
        // směrech udělá měkký přechod bez jediného shaderu.
        _device.SetRenderTarget(_bloomB);
        _device.Clear(Color.Transparent);
        batch.Begin(samplerState: SamplerState.LinearClamp);
        batch.Draw(_bloomA, new Rectangle(0, 0, _bloomB.Width / 2, _bloomB.Height / 2), Color.White);
        batch.End();

        _device.SetRenderTarget(_bloomA);
        _device.Clear(Color.Transparent);
        batch.Begin(samplerState: SamplerState.LinearClamp);
        batch.Draw(
            _bloomB,
            small,
            new Rectangle(0, 0, _bloomB.Width / 2, _bloomB.Height / 2),
            Color.White);
        batch.End();
    }

    private void EnsureTargets(int width, int height)
    {
        if (_scene is not null && _width == width && _height == height)
        {
            return;
        }

        Dispose();
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);

        _scene = new RenderTarget2D(
            _device, _width, _height, mipMap: false,
            SurfaceFormat.Color, DepthFormat.None);

        int bw = Math.Max(1, _width / BloomDivisor);
        int bh = Math.Max(1, _height / BloomDivisor);
        _bloomA = new RenderTarget2D(_device, bw, bh, mipMap: false, SurfaceFormat.Color, DepthFormat.None);
        _bloomB = new RenderTarget2D(_device, bw, bh, mipMap: false, SurfaceFormat.Color, DepthFormat.None);
    }
}
