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
        var drawn = FaunaSprites.All.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);

        var missing = LoadContent().Fauna
            .Where(f => !drawn.Contains(FaunaSprites.IdFor(f.Id)))
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
        var species = LoadContent().Fauna.Select(f => FaunaSprites.IdFor(f.Id))
            .ToHashSet(StringComparer.Ordinal);

        var orphans = FaunaSprites.All
            .Where(s => !species.Contains(s.Id))
            .Select(s => s.Id)
            .ToList();

        Assert.True(orphans.Count == 0,
            $"Kresby bez druhu v datech: {string.Join(", ", orphans)}");
    }

    [Fact]
    public void EverySpriteActuallyDrawsSomething()
    {
        foreach (var sprite in FaunaSprites.All)
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
        foreach (var sprite in FaunaSprites.All)
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
        foreach (var sprite in FaunaSprites.All)
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
        foreach (var sprite in FaunaSprites.All.Where(s => s.Anchor == FaunaAnchor.Ground))
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

        foreach (var sprite in FaunaSprites.All)
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
        foreach (var sprite in FaunaSprites.All)
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
