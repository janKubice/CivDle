using System.Reflection;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Co je plocha a co je stavba — a co z toho plyne pro sníh a pro odznaky.
///
/// <para>V zimě se přes horní třetinu každé siluety kreslí bílá čepice. Na domě
/// to obkreslí střechu. Na poli, které vyplňuje celé plátno, z toho byla
/// <b>bílá deska</b> přes horní třetinu lánu — pole žádnou střechu nemá
/// a zasněžená zem se stejně kreslí už v terénu pod ním.</para>
///
/// <para>Řeší se to pravidlem odvozeným z kresby, ne seznamem výjimek v datech:
/// u spritu se najde nejvyšší neprázdný řádek a změří se, jak je zaplněný.
/// Test hlídá obě strany toho pravidla — že pole projde a že dům ne.</para>
/// </summary>
public class FlatSpriteTests
{
    [Fact]
    public void APloughedFieldCountsAsGround()
    {
        // Přesně to volání, kterým se kreslí 'building.farm'. Kdyby pravidlo
        // přestalo platit pro pole, vrátí se zimní bílá deska.
        var canvas = new PixelCanvas(32, 32);
        FieldSprite.Draw(canvas, new Color(140, 106, 58), new Color(146, 166, 78), 5, true, 3);

        Assert.True(canvas.IsFlat, "pole se nepozná jako plocha — v zimě dostane sněhovou desku");
    }

    [Theory]
    [InlineData(4, false, 7)]  // building.grain_field
    [InlineData(5, true, 3)]   // building.farm
    [InlineData(3, false, 19)] // building.plantation
    public void EveryFieldInTheGameIsGround(int rowStep, bool vertical, int seed)
    {
        // Tři sprity v knihovně kreslí pole a každý má jinou orbu. Vodorovná
        // orba má první řádek jinak zaplněný než svislá, takže pravidlo musí
        // sedět na všechny tři, ne jen na ten, na kterém se ladilo.
        var canvas = new PixelCanvas(32, 32);
        FieldSprite.Draw(canvas, new Color(140, 106, 58), new Color(146, 166, 78), rowStep, vertical, seed);

        Assert.True(canvas.IsFlat, $"pole (rowStep {rowStep}, vertical {vertical}) se nepozná jako plocha");
    }

    [Fact]
    public void APitchedRoofIsNotGround()
    {
        // Hřeben sedlové střechy je pár pixelů. Kdyby tudy pravidlo prošlo,
        // zmizel by sníh ze všech domů naráz.
        var canvas = new PixelCanvas(32, 32);
        canvas.FillRect(8, 12, 18, 18, new Color(186, 168, 130));
        canvas.FillTriangle(6f, 12f, 28f, 12f, 17f, 4f, new Color(128, 92, 66));

        Assert.False(canvas.IsFlat, "dům se sedlovou střechou se tváří jako plocha");
    }

    [Fact]
    public void AFlatRoofedWarehouseIsStillABuilding()
    {
        // Hraniční případ: sklad má plochou střechu přes většinu šířky, ale
        // pořád je to stavba, která z terénu trčí — a sníh na ni patří.
        // Kresba je opsaná z 'building.warehouse'.
        var canvas = new PixelCanvas(32, 32);
        canvas.FillRect(3, 12, 26, 18, new Color(126, 96, 66));
        canvas.FillRect(3, 12, 26, 4, new Color(96, 72, 50));

        Assert.False(canvas.IsFlat, "sklad se tváří jako plocha — přijde o sníh na střeše");
    }

    [Fact]
    public void AnEmptyCanvasIsNotGround()
    {
        // Prázdné plátno nemá nejvyšší neprázdný řádek. Hledání nesmí spadnout
        // ani projít celé plátno jako „plochu".
        Assert.False(new PixelCanvas(32, 32).IsFlat);
    }

    [Fact]
    public void TransparentPixelsDoNotCount()
    {
        // Kresba se míchá přes alfu, takže nad siluetou bývají skoro průhledné
        // zbytky po vyhlazení. Kdyby se počítaly, byla by „plná" i řada, kterou
        // není vidět.
        var canvas = new PixelCanvas(8, 8);
        for (int x = 0; x < 8; x++)
        {
            canvas.Blend(x, 0, new Color(255, 255, 255) * 0.2f);
        }

        canvas.FillRect(3, 2, 2, 6, new Color(120, 90, 60));

        Assert.False(canvas.IsFlat, "průhledný závoj nad kresbou se počítá jako plocha");
    }

    [Fact]
    public void TheOnlyFlatSpritesInTheGameAreTheFields()
    {
        // Pravidlo se dotýká všech dvou set kreseb v knihovně naráz, a když se
        // splete, nikdo to nenahlásí — budově prostě v zimě zmizí sníh ze
        // střechy. Sprity se kreslí až na grafické kartě, ale kreslicí metody
        // samy grafiku nepotřebují, takže se dají projít všechny.
        var draws = typeof(SpriteLibrary)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(void)
                     && m.GetParameters().Length == 1
                     && m.GetParameters()[0].ParameterType == typeof(PixelCanvas))
            .ToList();

        Assert.True(draws.Count > 100, $"reflexe našla jen {draws.Count} kreseb — test nic nehlídá");

        var flat = new List<string>();
        foreach (var draw in draws)
        {
            // Obě velikosti plátna, které knihovna používá. Kreslí se do obou,
            // protože menší plátno kresbu ořízne a ořez je právě to, co by
            // mohlo udělat z domu „plochu".
            foreach (int size in new[] { SpriteLibrary.SpriteSize, SpriteLibrary.BigSpriteSize })
            {
                var canvas = new PixelCanvas(size, size);
                try
                {
                    draw.Invoke(null, new object[] { canvas });
                }
                catch
                {
                    continue; // kresba na tuhle velikost nepasuje; posoudí se v té druhé
                }

                if (canvas.IsFlat && !flat.Contains(draw.Name))
                {
                    flat.Add(draw.Name);
                }
            }
        }

        flat.Sort();

        // Pole jsou tři a jsou to jediné budovy, které leží na zemi. Kdyby
        // přibyla čtvrtá, patří sem — a kdyby odsud něco zmizelo, vrátila se
        // bílá deska.
        Assert.Equal(new[] { "Farm", "GrainField", "Plantation" }, flat);
    }
}
