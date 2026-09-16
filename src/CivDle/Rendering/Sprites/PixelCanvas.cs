using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Sprites;

/// <summary>
/// Maličké kreslicí plátno pro procedurální sprity — dokud nejsou skutečné assety,
/// generují se ikonky a objekty přímo v kódu (kód = jak). Alfa se míchá, takže
/// tvary jdou vrstvit. <see cref="ToTexture"/> vyrobí hotovou <see cref="Texture2D"/>.
/// </summary>
public sealed class PixelCanvas
{
    private readonly Color[] _pixels;

    public PixelCanvas(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new Color[width * height];
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>
    /// Je kresba <b>plochá</b>, tedy leží na zemi místo aby z ní trčela?
    ///
    /// <para><b>K čemu to je:</b> v zimě se na sprity sype sníh — na horní
    /// třetinu siluety. U domu to obkreslí střechu a vypadá to přesně tak, jak
    /// má. U pole, které vyplňuje celé plátno, z toho ale byla <b>bílá deska</b>
    /// přes horní třetinu lánu: pole nemá střechu, na kterou by se sníh
    /// chytil, a zasněžená zem se stejně kreslí už v terénu.</para>
    ///
    /// <para>Pozná se to bez dat a bez seznamu výjimek: u kresby se najde
    /// nejvyšší neprázdný řádek a zjistí se, kolik ho je. Sedlová střecha má
    /// nahoře hřeben o pár pixelech, plochá střecha skladu většinu šířky, pole
    /// úplně všechno. Prahem projde jen to poslední.</para>
    /// </summary>
    public bool IsFlat
    {
        get
        {
            for (int y = 0; y < Height; y++)
            {
                int covered = 0;
                for (int x = 0; x < Width; x++)
                {
                    if (_pixels[y * Width + x].A > 128)
                    {
                        covered++;
                    }
                }

                if (covered > 0)
                {
                    // Ostrý práh: i plochá střecha skladu má kolem sebe kus
                    // prázdna, pole nemá nic.
                    return covered >= Width - 1;
                }
            }

            return false; // prázdné plátno; sníh nemá na co padat
        }
    }

    /// <summary>
    /// Okna, která sprite kreslí — v jeho vlastních souřadnicích.
    ///
    /// <para><b>Proč to musí být zaznamenané a ne uhodnuté:</b> v noci se okna
    /// rozsvěcí. Dokud se polohy losovaly z hashe, svítilo to kdekoli uvnitř
    /// obdélníku budovy — tedy i uprostřed střechy nebo ve zdi vedle skutečného
    /// okna. Na hotovém spritu je to okamžitě vidět a kazí to jinak pěknou
    /// scénu. Barvu skla přitom nelze poznat automaticky: každý malíř si míchá
    /// vlastní odstín.</para>
    /// </summary>
    public IReadOnlyList<Rectangle> Windows => _windows;

    private readonly List<Rectangle> _windows = new();

    /// <summary>
    /// Nakreslí okno a zapamatuje si, kde je. Malíř tím zároveň řekne, co se
    /// má v noci rozsvítit — a nemusí to nikde opisovat podruhé.
    /// </summary>
    public void Window(int x, int y, int w, int h, Color glass)
    {
        FillRect(x, y, w, h, glass);
        _windows.Add(new Rectangle(x, y, w, h));
    }

    /// <summary>
    /// Barva pixelu. Mimo plátno vrací průhlednou — čtení za okrajem je
    /// u testů běžné a výjimka by je nutila hlídat meze místo kresby.
    /// </summary>
    public Color At(int x, int y) =>
        x < 0 || x >= Width || y < 0 || y >= Height
            ? Color.Transparent
            : _pixels[y * Width + x];

    /// <summary>Nakreslí pixel s alfa blendingem přes stávající obsah.</summary>
    public void Blend(int x, int y, Color color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height || color.A == 0)
        {
            return;
        }

        int i = y * Width + x;
        if (color.A == 255)
        {
            _pixels[i] = color;
            return;
        }

        float a = color.A / 255f;
        var dst = _pixels[i];
        float inv = 1f - a;
        _pixels[i] = new Color(
            (int)(color.R * a + dst.R * inv),
            (int)(color.G * a + dst.G * inv),
            (int)(color.B * a + dst.B * inv),
            (int)(color.A + dst.A * inv));
    }

    /// <summary>
    /// Zapíše pixel <b>bez míchání</b> s tím, co pod ním je.
    ///
    /// <para>Na co to je: <see cref="Blend"/> počítá s tím, že pod pixelem
    /// něco leží. Nad prázdnem je ale podklad průhledná černá, takže
    /// průsvitná barva se namíchá s <em>černou</em> — křídlo vážky o krytí
    /// 0,5 se uloží s poloviční jasností a pak se ještě jednou prosvítí při
    /// kreslení na mapu. Výsledkem je šedá šmouha místo průsvitného křídla.</para>
    ///
    /// <para>Pro průsvitné <b>světlé</b> kresby na prázdném plátně (křídla,
    /// vodní tříšť, chapadla) je tedy správně zapsat barvu tak, jak je,
    /// a nechat míchání až na kreslení scény. Stín je opačný případ — ten je
    /// černý a přes <see cref="Blend"/> vyjde správně.</para>
    /// </summary>
    public void Paint(int x, int y, Color color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return;
        }

        _pixels[y * Width + x] = color;
    }

    /// <summary>Obdélník zapsaný přes <see cref="Paint"/> — bez míchání s podkladem.</summary>
    public void PaintRect(int x, int y, int w, int h, Color color)
    {
        for (int yy = y; yy < y + h; yy++)
        {
            for (int xx = x; xx < x + w; xx++)
            {
                Paint(xx, yy, color);
            }
        }
    }

    public void FillRect(int x, int y, int w, int h, Color color)
    {
        for (int yy = y; yy < y + h; yy++)
        {
            for (int xx = x; xx < x + w; xx++)
            {
                Blend(xx, yy, color);
            }
        }
    }

    /// <summary>Vyplněný kruh se středem (cx, cy) a poloměrem r.</summary>
    public void FillCircle(float cx, float cy, float r, Color color)
    {
        int minX = (int)MathF.Floor(cx - r), maxX = (int)MathF.Ceiling(cx + r);
        int minY = (int)MathF.Floor(cy - r), maxY = (int)MathF.Ceiling(cy + r);
        float r2 = r * r;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= r2)
                {
                    Blend(x, y, color);
                }
            }
        }
    }

    /// <summary>Rovnoramenný trojúhelník (např. střecha) mezi vrcholem a základnou.</summary>
    public void FillTriangle(float ax, float ay, float bx, float by, float cx, float cy, Color color)
    {
        int minX = (int)MathF.Floor(Math.Min(ax, Math.Min(bx, cx)));
        int maxX = (int)MathF.Ceiling(Math.Max(ax, Math.Max(bx, cx)));
        int minY = (int)MathF.Floor(Math.Min(ay, Math.Min(by, cy)));
        int maxY = (int)MathF.Ceiling(Math.Max(ay, Math.Max(by, cy)));
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                if (SameSide(px, py, ax, ay, bx, by, cx, cy)
                    && SameSide(px, py, bx, by, cx, cy, ax, ay)
                    && SameSide(px, py, cx, cy, ax, ay, bx, by))
                {
                    Blend(x, y, color);
                }
            }
        }
    }

    /// <summary>
    /// Obtáhne siluetu: krajní pixely kresby ztmaví.
    ///
    /// <para><b>Proč to obrázek potřebuje:</b> budova ležela na terénu bez
    /// hranice. Střecha o podobném jasu jako tráva pod ní splynula a z bloku
    /// domů byla skvrna — oko nemělo za co chytit tvar. Tmavší okraj je
    /// nejstarší trik pixel artu a dělá přesně tohle: odlepí objekt od pozadí,
    /// ať je pozadí jakékoli.</para>
    ///
    /// <para>Ztmavuje se <b>uvnitř</b> siluety, nepřidává se prstenec ven —
    /// obrázek tím nemění velikost ani se nemusí kreslit vícekrát. A dělá se
    /// to při stavbě spritu, takže za běhu nestojí nic; obtahování čtyřmi
    /// kresbami navíc by v husté zástavbě bylo cítit.</para>
    ///
    /// <para>Za okrajem plátna se počítá prázdno, takže lem dostane i kresba,
    /// která plátno vyplní celé — dvě takové budovy vedle sebe by jinak
    /// splynuly v jednu.</para>
    /// </summary>
    /// <param name="strength">Jak moc lem ztmavne (0 = nic, 1 = do černa).</param>
    public void Outline(float strength)
    {
        var source = (Color[])_pixels.Clone();
        float keep = 1f - Math.Clamp(strength, 0f, 1f);

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int i = y * Width + x;

                // Průsvitné pixely se neobtahují. Stín pod stromem je taky
                // kresba — obtáhnout ho by znamenalo tmavý prstenec kolem
                // stínu, tedy přesně ten „nálepkový" dojem, proti kterému
                // obrys je.
                if (source[i].A < Solid || !IsOnRim(source, x, y))
                {
                    continue;
                }

                var c = _pixels[i];
                _pixels[i] = new Color((int)(c.R * keep), (int)(c.G * keep), (int)(c.B * keep), c.A);
            }
        }
    }

    /// <summary>Od jaké krytí se pixel počítá za tělo kresby, ne za stín či závoj.</summary>
    private const byte Solid = 200;

    /// <summary>Sousedí pixel s prázdnem, s průsvitem, nebo s okrajem plátna?</summary>
    private bool IsOnRim(Color[] source, int x, int y) =>
        IsEmpty(source, x - 1, y) || IsEmpty(source, x + 1, y)
        || IsEmpty(source, x, y - 1) || IsEmpty(source, x, y + 1);

    private bool IsEmpty(Color[] source, int x, int y) =>
        x < 0 || x >= Width || y < 0 || y >= Height || source[y * Width + x].A < Solid;

    public Texture2D ToTexture(GraphicsDevice device)
    {
        var texture = new Texture2D(device, Width, Height);
        texture.SetData(_pixels);
        return texture;
    }

    /// <summary>
    /// Srovná hotovou kresbu na společnou paletu hry (<see cref="GamePalette"/>).
    ///
    /// <para>Dělá se to až <b>po</b> kreslení, ne během něj: sprity si můžou
    /// dál míchat barvy a stínovat průhledností, jak potřebují, a teprve
    /// výsledek se srovná. Kdyby se snapovalo při každém <see cref="Blend"/>,
    /// každá poloprůhledná vrstva by kresbu posunula o kus jinam a měkké
    /// přechody by se rozpadly.</para>
    /// </summary>
    public void SnapToPalette()
    {
        for (int i = 0; i < _pixels.Length; i++)
        {
            _pixels[i] = GamePalette.Snap(_pixels[i]);
        }
    }

    private static bool SameSide(float px, float py, float ax, float ay, float bx, float by, float refx, float refy)
    {
        float cross1 = (bx - ax) * (py - ay) - (by - ay) * (px - ax);
        float cross2 = (bx - ax) * (refy - ay) - (by - ay) * (refx - ax);
        return cross1 * cross2 >= 0;
    }
}
