using Microsoft.Xna.Framework;

namespace CivDle.Rendering.Sprites;

/// <summary>
/// Kresba zoraného pole: brázdy, úvrať kolem dokola a nerovný okraj porostu.
///
/// <para><b>Proč to má vlastní třídu:</b> pole zabírají na obrazovce největší
/// souvislý kus plochy ze všech budov a byla to dosud nejplošší věc ve hře —
/// obarvený čtverec s pár rovnými pruhy. Čtverec vedle čtverce dělal mřížku
/// a z krajiny byl tabulkový list. Zároveň je to jediný sprite, který si žádá
/// vlastní algoritmus místo pár obdélníků, takže patří ven z knihovny ručních
/// kreseb — a takhle se dá otestovat bez grafické karty.</para>
///
/// <para>Tři věci lámou dojem mřížky a všechny jsou tady:</para>
/// <list type="number">
/// <item><description><b>Brázda má bok.</b> Každý řádek dostane světlou hranu
/// ze strany slunce a tmavou ze strany odvrácené. Z plochy je tím vlnitý
/// povrch, aniž by přibyl jediný pixel navíc.</description></item>
/// <item><description><b>Úvrať.</b> Pás holé hlíny kolem dokola — místo, kde
/// pluh otáčí. Dvě sousední pole tak mezi sebou mají dvojitý hliněný pruh
/// a přestanou splývat v jednu plochu.</description></item>
/// <item><description><b>Nerovný konec řádků.</b> Délka každého řádku se
/// o kousek liší podle deterministického hashe, takže porost nemá pravítkem
/// uříznutou hranu.</description></item>
/// </list>
///
/// <para>Směr světla je tentýž jako u stínů budov (<see cref="SceneLight"/>):
/// slunce vlevo nahoře. Kdyby si pole svítilo po svém, bylo by to na první
/// pohled vidět jako jediná věc, která do scény nepatří.</para>
///
/// <para>Vrstva: čistý render, a navíc bez závislosti na zařízení — kreslí se
/// do <see cref="PixelCanvas"/>, texturu z něj dělá až volající.</para>
/// </summary>
public static class FieldSprite
{
    /// <summary>Šířka úvrati: pás holé hlíny, kde pluh otáčí.</summary>
    public const int Headland = 3;

    /// <summary>
    /// Nakreslí pole přes celé plátno.
    /// </summary>
    /// <param name="soil">Barva zorané hlíny mezi řádky.</param>
    /// <param name="crop">Barva porostu.</param>
    /// <param name="rowStep">Rozteč řádků v pixelech; menší = jemnější kultura.</param>
    /// <param name="vertical">Orba shora dolů místo zleva doprava.</param>
    /// <param name="seed">Rozhoduje o nepravidelnostech — jiný seed, jiné pole.</param>
    public static void Draw(PixelCanvas c, Color soil, Color crop, int rowStep, bool vertical, int seed)
    {
        int w = c.Width;
        int h = c.Height;
        var soilDark = Shade(soil, 0.74f);
        var soilLight = Shade(soil, 1.14f);
        var cropDark = Shade(crop, 0.72f);
        var cropLight = Shade(crop, 1.18f);

        // Hlína pod vším. Porost ji z větší části zakryje, ale na úvrati
        // a v brázdách zůstane vidět — a právě ta hlína dělá z pole pole,
        // ne zelený obdélník.
        c.FillRect(0, 0, w, h, soilLight);

        // Tmavý lem úplně na kraji: hranice pozemku. Bez něj se dvě sousední
        // pole slijí a mřížka je zpátky.
        c.FillRect(0, 0, w, 1, soilDark);
        c.FillRect(0, h - 1, w, 1, soilDark);
        c.FillRect(0, 0, 1, h, soilDark);
        c.FillRect(w - 1, 0, 1, h, soilDark);

        int across = vertical ? w : h;
        int along = vertical ? h : w;
        int span = along - 2 * Headland;
        if (span <= 0 || rowStep < 2)
        {
            return; // na tak malé plátno se pole nevejde; zůstane holá hlína
        }

        for (int row = 0; ; row++)
        {
            int at = Headland + row * rowStep;
            if (at + rowStep > across - Headland)
            {
                break;
            }

            // Konce řádků se o kus liší. Pravítkem uříznutý porost je přesně to,
            // co z pole dělá ikonu v tabulce.
            int head = (int)(Hash(row, seed, 11) % 3);
            int tail = (int)(Hash(row, seed, 29) % 3);
            int start = Headland + head;
            int length = span - head - tail;
            int thickness = rowStep - 1;

            if (vertical)
            {
                c.FillRect(at, start, thickness, length, crop);
                c.FillRect(at, start, 1, length, cropLight);                // bok ke slunci
                c.FillRect(at + thickness - 1, start, 1, length, cropDark); // odvrácený bok
                c.FillRect(at + thickness, start, 1, length, soilDark);     // brázda
            }
            else
            {
                c.FillRect(start, at, length, thickness, crop);
                c.FillRect(start, at, length, 1, cropLight);
                c.FillRect(start, at + thickness - 1, length, 1, cropDark);
                c.FillRect(start, at + thickness, length, 1, soilDark);
            }
        }
    }

    /// <summary>
    /// Tatáž barva světlejší nebo tmavší. Násobením, ne mícháním s bílou —
    /// míchání odstín vybledne do šedi a hlína by přestala být hlína.
    /// </summary>
    private static Color Shade(Color color, float factor) => new(
        Math.Clamp((int)(color.R * factor), 0, 255),
        Math.Clamp((int)(color.G * factor), 0, 255),
        Math.Clamp((int)(color.B * factor), 0, 255));

    /// <summary>Deterministický hash — totéž pole vypadá pokaždé stejně.</summary>
    private static uint Hash(int a, int b, int salt)
    {
        unchecked
        {
            uint h = (uint)(a * 374761393 + b * 668265263 + salt * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }
    }
}
