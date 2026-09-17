using CivDle.Core.Content;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Kresby zvěře.
///
/// <para>Zvíře se dřív kreslilo jako barevný čtvereček o dvou až pěti
/// pixelech. Dokud byly druhy tři, dalo se to omluvit; s osmačtyřiceti z toho
/// byla jen mapa různobarevných teček. Testuje se proto to, co z tečky dělá
/// zvíře: že kresbu má <b>každý</b> druh z dat, že něco opravdu nakreslí
/// a že má tvar — tedy siluetu, ne vyplněný obdélník.</para>
///
/// <para>Kreslí se do <see cref="PixelCanvas"/>, což je jen pole barev.
/// Grafická karta k tomu není potřeba a testy běží headless.</para>
/// </summary>
public class FaunaSpriteTests
{
    [Fact]
    public void EverySpeciesHasASprite()
    {
        // Tohle je ta chyba, která se nedá odhalit pohledem: nový druh v JSON,
        // na který se zapomnělo nakreslit tělo. Nic nespadne — jen je v trávě
        // místo lišky oranžová tečka a nikdo to nespojí s chybějící kresbou.
        var missing = LoadContent().Fauna
            .Where(f => !FaunaSprites.Knows(f.Id))
            .Select(f => f.Id)
            .ToList();

        Assert.True(missing.Count == 0,
            $"Druhy bez kresby (zůstane po nich čtvereček): {string.Join(", ", missing)}");
    }

    [Fact]
    public void NoSpriteIsDrawnForASpeciesThatDoesNotExist()
    {
        // Opačný směr. Kresba bez druhu je mrtvý kód, který si nikdo nevšimne,
        // protože se prostě nikdy nepoužije.
        var species = LoadContent().Fauna.Select(f => f.Id).ToHashSet(StringComparer.Ordinal);

        var orphans = Sprites
            .Select(s => s.Id["fauna.".Length..])
            .Where(id => !species.Contains(id))
            .ToList();

        Assert.True(orphans.Count == 0,
            $"Kresby bez druhu v datech: {string.Join(", ", orphans)}");
    }

    [Fact]
    public void EverySpriteActuallyDrawsSomething()
    {
        foreach (var sprite in Sprites)
        {
            var canvas = Paint(sprite);
            Assert.True(Solid(canvas) >= 4, $"{sprite.Id} je skoro prázdný");
        }
    }

    [Fact]
    public void NoSpriteIsJustAFilledBox()
    {
        // Silueta je na deseti pixelech celá informace. Kresba, která vyplní
        // plátno od kraje ke kraji, je zpátky ten čtvereček — jen větší.
        foreach (var sprite in Sprites)
        {
            var canvas = Paint(sprite);
            float covered = Solid(canvas) / (float)(sprite.Width * sprite.Height);

            Assert.True(covered < 0.82f, $"{sprite.Id} vyplňuje {covered:P0} plátna — to není silueta");
        }
    }

    [Fact]
    public void NothingIsDrawnOutsideTheCanvas()
    {
        // PixelCanvas kresbu za okrajem tiše zahodí. Zvíře, kterému uteče
        // půlka rohů mimo plátno, tedy nespadne — jen bude bezrohé, a to je
        // přesně ta chyba, kterou test musí chytit za mě.
        foreach (var sprite in Sprites)
        {
            var canvas = Paint(sprite);

            Assert.True(TouchesRow(canvas, 0) || TouchesRow(canvas, 1),
                $"{sprite.Id} má nahoře prázdno — plátno je zbytečně vysoké, nebo kresba přetekla");
        }
    }

    [Fact]
    public void AnimalsThatStandHaveTheirFeetAtTheBottom()
    {
        // Kotva „na zem" znamená, že spodní řádek plátna je země. Kdyby tvor
        // končil o tři pixely výš, vznášel by se nad trávou.
        foreach (var sprite in Sprites.Where(s => s.Anchor == FaunaAnchor.Ground))
        {
            var canvas = Paint(sprite);

            Assert.True(
                TouchesRow(canvas, sprite.Height - 1) || TouchesRow(canvas, sprite.Height - 2),
                $"{sprite.Id} stojí na zemi, ale dole na plátně nic nemá");
        }
    }

    [Fact]
    public void EverySpeciesKeepsItsOwnLook()
    {
        // Dva druhy se stejnou kresbou by byla chyba přehlédnutelná i při
        // prohlížení: v trávě jsou to stejně jen siluety. Porovnávají se
        // proto pixel po pixelu.
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var sprite in Sprites)
        {
            string signature = Signature(Paint(sprite));
            Assert.False(seen.TryGetValue(signature, out string? twin),
                $"{sprite.Id} a {twin} vypadají úplně stejně");
            seen[signature] = sprite.Id;
        }
    }

    [Fact]
    public void SpritesAreBigEnoughToRecogniseAndSmallEnoughToFitTheWorld()
    {
        // Dlaždice má šestnáct pixelů a chodec dvanáct. Zvíře menší než pět
        // pixelů je zpátky tečka; zvíře přes dvacet přeroste dlaždici,
        // na které stojí, a rozbije měřítko krajiny.
        foreach (var sprite in Sprites)
        {
            Assert.InRange(sprite.Width, 5, 20);
            Assert.InRange(sprite.Height, 4, 16);
        }
    }

    [Fact]
    public void TheAnchorSaysWhereTheDrawingIsBuiltFor()
    {
        // Pták v letu na ničem nestojí. Postavit ho „nohama na zem" by ho
        // posunulo o půl těla dolů a letěl by pod sebou.
        Assert.Equal(FaunaAnchor.Free, FaunaSprites.AnchorFor("eagle"));
        Assert.Equal(FaunaAnchor.Free, FaunaSprites.AnchorFor("whale"));
        Assert.Equal(FaunaAnchor.Ground, FaunaSprites.AnchorFor("bear"));
        Assert.Equal(FaunaAnchor.Ground, FaunaSprites.AnchorFor("penguin"));

        // Neznámý druh se chová jako tvor na zemi — to je většina a je to
        // ta méně nápadná chyba z těch dvou.
        Assert.Equal(FaunaAnchor.Ground, FaunaSprites.AnchorFor("neexistuje"));
    }

    [Fact]
    public void TheColourComesFromTheDataNotFromTheCode()
    {
        // Tohle byla tichá chyba: barva druhu stála dvakrát — v fauna.json
        // a natvrdo v kresbě. Změna v datech pak přebarvila zvíře na
        // minimapě, ale sprite ne, takže si dva kusy hry myslely něco
        // jiného a nic nespadlo.
        //
        // Test obarví druh nesmyslně a čeká, že se to na kresbě projeví.
        var content = LoadContent();
        var deer = content.Fauna.First(f => f.Id == "deer");

        var repainted = deer with { Color = new RgbColor(20, 200, 40) };
        var sprite = FaunaSprites.For(new[] { repainted }).Single();

        var canvas = new PixelCanvas(sprite.Width, sprite.Height);
        sprite.Draw(canvas);

        Assert.True(HasGreenish(canvas), "změna barvy v datech se do kresby nepromítla");
    }

    [Fact]
    public void TheScaleComesFromTheDataToo()
    {
        // Měřítko musí data opravdu řídit. Dřív tu bylo pole `size`, které
        // po příchodu kreseb neřídilo nic — a pole, které tiše nic nedělá,
        // je horší než žádné.
        var content = LoadContent();
        var bear = content.Fauna.First(f => f.Id == "bear");

        var small = FaunaSprites.For(new[] { bear with { Scale = 0.6 } }).Single();
        var large = FaunaSprites.For(new[] { bear with { Scale = 1.6 } }).Single();

        Assert.True(large.Width > small.Width, "měřítko z dat nemění šířku kresby");
        Assert.True(large.Height > small.Height, "měřítko z dat nemění výšku kresby");
    }

    [Fact]
    public void AnAbsurdScaleStillFitsTheWorld()
    {
        // Dlaždice má šestnáct pixelů. Zvíře se musí do krajiny vejít i tehdy,
        // když někdo v datech přepíše měřítko na krajní hodnotu.
        var content = LoadContent();
        var bear = content.Fauna.First(f => f.Id == "bear");

        foreach (double scale in new[] { 0.4, 3.0 })
        {
            var sprite = FaunaSprites.For(new[] { bear with { Scale = scale } }).Single();

            Assert.InRange(sprite.Width, 4, 20);
            Assert.InRange(sprite.Height, 4, 20);
        }
    }

    private static bool HasGreenish(PixelCanvas canvas)
    {
        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                var pixel = canvas.At(x, y);
                if (pixel.A > 128 && pixel.G > pixel.R + 20 && pixel.G > pixel.B + 20)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Kresby postavené z <b>ostrých dat</b>. Barva i měřítko jdou z
    /// <c>fauna.json</c>, takže test kontroluje to, co hráč opravdu uvidí —
    /// ne jen tvary, které leží v kódu.
    /// </summary>
    private static IReadOnlyList<FaunaSprite> Sprites { get; } =
        FaunaSprites.For(LoadContent().Fauna);

    private static PixelCanvas Paint(FaunaSprite sprite)
    {
        var canvas = new PixelCanvas(sprite.Width, sprite.Height);
        sprite.Draw(canvas);
        return canvas;
    }

    /// <summary>Kolik pixelů je tělo kresby, ne stín ani závoj.</summary>
    private static int Solid(PixelCanvas canvas)
    {
        int count = 0;
        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                if (canvas.At(x, y).A > 128)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static bool TouchesRow(PixelCanvas canvas, int y)
    {
        for (int x = 0; x < canvas.Width; x++)
        {
            if (canvas.At(x, y).A > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string Signature(PixelCanvas canvas)
    {
        var text = new System.Text.StringBuilder(canvas.Width * canvas.Height);
        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                var color = canvas.At(x, y);
                text.Append(color.A == 0 ? '.' : (char)('a' + ((color.R + color.G * 3 + color.B * 7) % 26)));
            }
        }

        return text.ToString();
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
