using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Tilt-shift (efekt „město jako model na stole") — bez jediného shaderu.
///
/// <para><b>Proč bez shaderu:</b> v projektu není content pipeline ani jeden
/// <c>.fx</c>. Zavést je znamená přibalit MGCB do buildu a odladit ho zvlášť pro
/// DirectX a zvlášť pro OpenGL. To je proti pravidlu „no balast" a je to
/// mnohem víc práce než samotný efekt.</para>
///
/// <para><b>Jak se rozostřuje bez shaderu:</b> obrázek se zmenší na čtvrtinu a
/// osminu s lineárním filtrem a natáhne zpátky. Zvětšení lineárně
/// interpolovaného zmenšeniny <i>je</i> rozostření — jen se za něj neplatí
/// desítkami vzorků na pixel. Zmenšuje se po polovinách, ne jedním skokem:
/// grafika při zmenšení 4× v jednom kroku sáhne jen na 2×2 texely a zbytek
/// zahodí, takže by z toho bylo aliasování, ne rozostření.</para>
///
/// <para><b>Jak vzniká plynulý přechod:</b> obraz se skládá po vodorovných
/// pruzích a každý pruh dostane jinou průhlednost podle
/// <see cref="TiltShiftOptions.BlurAt"/>. Pruhů je dost na to, aby jejich
/// hranice nebyly vidět, a lineární filtr zbytek zahladí.</para>
///
/// <para>Vrstva: čistý render. Nic nečte ze simulace a nic do ní nezapisuje —
/// dostane hotovou texturu scény a vrátí hotovou texturu scény.</para>
/// </summary>
public sealed class TiltShift : IDisposable
{
    /// <summary>
    /// Na kolik vodorovných pruhů se obraz skládá. Šedesát čtyři je z měření:
    /// při třiceti dvou je na plochém nebi vidět schodovitý přechod, při
    /// stodvaceti osmi už není rozdíl poznat a jen to stojí draw cally.
    /// </summary>
    public const int StripCount = 64;

    /// <summary>
    /// Kontrast a sytost jedním kreslením: cílový pixel se vynásobí zdrojovým,
    /// ale jen z části podle alfy. Vyjde z toho <c>cíl × lerp(1, zdroj, síla)</c>,
    /// tedy plynulá cesta mezi „nech být" a „umocni na druhou". Umocnění barvy
    /// prohloubí stíny a zesytí barvy — přesně to, čím se fotky modelů liší od
    /// fotek měst.
    /// </summary>
    private static readonly BlendState PartialMultiply = new()
    {
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.InverseSourceAlpha,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
    };

    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _batch;

    private RenderTarget2D? _half;
    private RenderTarget2D? _quarter;
    private RenderTarget2D? _eighth;
    private int _sourceWidth;
    private int _sourceHeight;
    private bool _disposed;

    public TiltShift(GraphicsDevice device, SpriteBatch batch)
    {
        _device = device;
        _batch = batch;
    }

    /// <summary>
    /// Svislé hranice pruhu — veřejné kvůli testu, že pruhy pokryjí plochu beze
    /// zbytku. Kdyby se hranice počítaly pro každý pruh zvlášť ze zaokrouhlené
    /// výšky, zůstaly by mezi nimi řádky obrazu nedotčené a na fotce by byly
    /// vidět jako ostré linky uprostřed rozmazané plochy.
    /// </summary>
    public static (int Top, int Bottom) StripBounds(int index, int count, int top, int height)
    {
        int y0 = top + (int)Math.Round(height * (double)index / count);
        int y1 = top + (int)Math.Round(height * (double)(index + 1) / count);
        return (y0, y1);
    }

    /// <summary>
    /// Výřez zmenšeniny, který odpovídá pruhu na výsledném obrázku. Vždy aspoň
    /// jeden řádek vysoký — u osminové zmenšeniny by jinak úzký pruh vyšel
    /// nulový a nenakreslilo by se nic.
    /// </summary>
    public static Rectangle SourceStrip(int stripTop, int stripBottom, int destinationTop, int destinationHeight, int sourceWidth, int sourceHeight)
    {
        double scale = sourceHeight / (double)Math.Max(1, destinationHeight);
        int y0 = (int)Math.Floor((stripTop - destinationTop) * scale);
        int y1 = (int)Math.Ceiling((stripBottom - destinationTop) * scale);
        y0 = Math.Clamp(y0, 0, Math.Max(0, sourceHeight - 1));
        y1 = Math.Clamp(y1, y0 + 1, sourceHeight);
        return new Rectangle(0, y0, sourceWidth, y1 - y0);
    }

    /// <summary>
    /// Vykreslí scénu s efektem do <paramref name="output"/>.
    ///
    /// <para><b>Po návratu zůstává <paramref name="output"/> nastavený jako cíl
    /// kreslení</b> — volající na něj pak může dokreslit proužek s čísly.
    /// Kdyby si ho nastavoval znovu sám, MonoGame by při přepnutí obsah zahodil
    /// a efekt by zmizel.</para>
    /// </summary>
    /// <param name="scene">Hotová scéna. Nesmí to být <paramref name="output"/>.</param>
    /// <param name="output">Kam se skládá výsledek.</param>
    /// <param name="destination">Kam v cíli scéna patří (bez proužku).</param>
    /// <param name="background">
    /// Čím se cíl vyplní pod scénou. Přepnutí cíle jeho obsah zahodí, takže se
    /// vyplnit musí — a barvu volí volající, protože pod proužkem s čísly musí
    /// zůstat totéž pozadí jako bez efektu, jinak by byl proužek průsvitný.
    /// </param>
    public void Render(
        RenderTarget2D scene, RenderTarget2D output, Rectangle destination,
        Color background, TiltShiftOptions options)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(output);
        if (ReferenceEquals(scene, output))
        {
            throw new ArgumentException("Scéna a cíl nesmí být tatáž textura — grafika by četla to, do čeho zrovna píše.", nameof(output));
        }

        if (options.Strength > 0.001f)
        {
            BuildBlurChain(scene);
        }

        _device.SetRenderTarget(output);
        _device.Clear(background);

        _batch.Begin(samplerState: SamplerState.LinearClamp);
        _batch.Draw(scene, destination, Color.White);
        _batch.End();

        if (options.Punch > 0.001f)
        {
            _batch.Begin(blendState: PartialMultiply, samplerState: SamplerState.LinearClamp);
            _batch.Draw(scene, destination, Color.White * options.Punch);
            _batch.End();
        }

        if (options.Strength > 0.001f)
        {
            DrawBlurStrips(destination, options);
        }
    }

    /// <summary>
    /// Postupné zmenšování na polovinu. Každý krok je průměr 2×2 texelů, takže
    /// osminová zmenšenina nese informaci z celého okolí, ne z každého osmého
    /// pixelu.
    /// </summary>
    private void BuildBlurChain(RenderTarget2D scene)
    {
        EnsureTargets(scene.Width, scene.Height);

        Downscale(scene, _half!);
        Downscale(_half!, _quarter!);
        Downscale(_quarter!, _eighth!);
    }

    private void Downscale(Texture2D source, RenderTarget2D target)
    {
        _device.SetRenderTarget(target);
        _device.Clear(Color.Transparent);
        _batch.Begin(samplerState: SamplerState.LinearClamp);
        _batch.Draw(source, new Rectangle(0, 0, target.Width, target.Height), Color.White);
        _batch.End();
    }

    /// <summary>
    /// Složí rozostření po pruzích. První polovina náběhu míchá ostrý obraz se
    /// čtvrtinovou zmenšeninou, druhá přidá osminovou — tedy dva stupně
    /// rozostření místo jednoho. S jedním stupněm vypadá poloviční průhlednost
    /// jako duch, ne jako neostrost: je vidět ostrá hrana i její rozmazaná kopie.
    /// </summary>
    private void DrawBlurStrips(Rectangle destination, TiltShiftOptions options)
    {
        _batch.Begin(samplerState: SamplerState.LinearClamp);

        for (int i = 0; i < StripCount; i++)
        {
            var (top, bottom) = StripBounds(i, StripCount, destination.Y, destination.Height);
            if (bottom <= top)
            {
                continue;
            }

            float center = (i + 0.5f) / StripCount;
            float blur = options.BlurAt(center);
            if (blur <= 0.002f)
            {
                continue;
            }

            DrawStrip(_quarter!, destination, top, bottom, Math.Min(1f, blur * 2f));

            if (blur > 0.5f)
            {
                DrawStrip(_eighth!, destination, top, bottom, (blur - 0.5f) * 2f);
            }
        }

        _batch.End();
    }

    private void DrawStrip(RenderTarget2D source, Rectangle destination, int top, int bottom, float alpha)
    {
        var slice = SourceStrip(top, bottom, destination.Y, destination.Height, source.Width, source.Height);
        var target = new Rectangle(destination.X, top, destination.Width, bottom - top);
        _batch.Draw(source, target, slice, Color.White * Math.Clamp(alpha, 0f, 1f));
    }

    /// <summary>
    /// Zmenšeniny se drží mezi snímky a přesypou se, až když se změní rozměr —
    /// při natáčení videa jde o tisíce snímků a nová textura na každý z nich by
    /// se v ovladači nasčítala do gigabajtů (což už tenhle projekt jednou
    /// zabilo, viz <c>FrameSequence</c>).
    /// </summary>
    private void EnsureTargets(int width, int height)
    {
        if (_half is not null && _sourceWidth == width && _sourceHeight == height)
        {
            return;
        }

        DisposeTargets();
        _sourceWidth = width;
        _sourceHeight = height;
        _half = NewTarget(width, height, 2);
        _quarter = NewTarget(width, height, 4);
        _eighth = NewTarget(width, height, 8);
    }

    private RenderTarget2D NewTarget(int width, int height, int divisor) =>
        new(_device, Math.Max(1, width / divisor), Math.Max(1, height / divisor));

    private void DisposeTargets()
    {
        _half?.Dispose();
        _quarter?.Dispose();
        _eighth?.Dispose();
        _half = _quarter = _eighth = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeTargets();
    }
}
