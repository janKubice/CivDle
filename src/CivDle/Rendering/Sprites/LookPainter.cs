using CivDle.Core.Content;
using Microsoft.Xna.Framework;

namespace CivDle.Rendering.Sprites;

/// <summary>
/// Nakreslí budovu podle vzhledu z dat (<see cref="BuildingLook"/>): tvar,
/// barvy a prvky. Data říkají „co", tahle třída „jak".
///
/// <para><b>Proč to existuje:</b> ručně psaná kresba pro každou ze sta nových
/// budov (pozdní hra, nové světy) by byla pomalá a každá by vypadala trochu
/// jinak. Malíř drží jeden rukopis: světlo zleva shora (levá hrana světlejší,
/// pravá tmavší), obrys a paletu dotáhne <c>SpriteLibrary</c> stejně jako
/// u ručních kreseb, okna se zapisují přes <see cref="PixelCanvas.Window"/>,
/// takže v noci svítí.</para>
///
/// <para><b>Souřadnice:</b> návrh je v mřížce 32×32 „jednotek" u spodní hrany
/// plátna a škáluje se na skutečnou velikost (32, 64 i 96 px). Vysoké tvary
/// (věž) využijí celou výšku plátna, které je u vysokých budov vyšší než
/// široké (viz <c>BuildingDef.VisualHeight</c>).</para>
///
/// <para>Vrstva: render, bez stavu. Jen kreslí na plátno — dá se testovat bez
/// grafické karty.</para>
/// </summary>
public static class LookPainter
{
    /// <summary>Nakreslí vzhled na plátno.</summary>
    public static void Paint(PixelCanvas canvas, BuildingLook look)
    {
        var f = new Frame(canvas, Palette.Of(look));
        var body = look.Shape switch
        {
            "hut" => Hut(f, look, storeys: 1),
            "house" => Hut(f, look, storeys: 2),
            "tower" => Tower(f, look),
            "dome" => Dome(f, look),
            "hall" => Hall(f, look),
            "workshop" => Workshop(f, look),
            "tanks" => Tanks(f, look),
            "pit" => Pit(f),
            "field" => Field(f),
            "grove" => Grove(f, look),
            "stilts" => Stilts(f, look),
            "raft" => Raft(f, look),
            "balloon" => Balloon(f, look),
            "mast" => Mast(f, look),
            "column" => Column(f),
            "obelisk" => Obelisk(f),
            "arch" => Arch(f),
            "crystal" => Crystal(f),
            "bulb" => Bulb(f, look),
            "tree" => Tree(f),
            "wall" => Wall(f),
            "channel" => Channel(f),
            "pool" => Pool(f),
            "platform" => Platform(f),
            "vortex" => Vortex(f),
            "ring" => Ring(f),
            "mirrors" => Mirrors(f),
            "pier" => Pier(f),
            "rig" => Rig(f),
            "pods" => Pods(f),
            _ => throw new ArgumentException($"Malíř neumí tvar '{look.Shape}'.", nameof(look)),
        };

        foreach (string feature in look.Features)
        {
            Feature(f, feature, body);
        }
    }

    // ----- barvy -----

    /// <summary>Barvy vzhledu i s odstíny pro světlou a stinnou stranu.</summary>
    private readonly record struct Palette(
        Color Wall, Color WallLight, Color WallDark,
        Color Roof, Color RoofLight, Color RoofDark,
        Color Accent, Color AccentDark, Color Glow, Color Glass)
    {
        public static Palette Of(BuildingLook look)
        {
            var wall = ToColor(look.Wall);
            var roof = ToColor(look.Roof);
            var accent = ToColor(look.Accent);
            var glow = look.Glow is { } g ? ToColor(g) : new Color(255, 214, 120);
            return new Palette(
                wall, Lighten(wall, 0.18f), Darken(wall, 0.28f),
                roof, Lighten(roof, 0.2f), Darken(roof, 0.3f),
                accent, Darken(accent, 0.3f), glow, new Color(150, 205, 225));
        }
    }

    private static Color ToColor(RgbColor c) => new(c.R, c.G, c.B);

    private static Color Lighten(Color c, float amount) => new(
        (int)(c.R + (255 - c.R) * amount), (int)(c.G + (255 - c.G) * amount), (int)(c.B + (255 - c.B) * amount));

    private static Color Darken(Color c, float amount) => new(
        (int)(c.R * (1 - amount)), (int)(c.G * (1 - amount)), (int)(c.B * (1 - amount)));

    private static Color Alpha(Color c, int alpha) => new(c.R, c.G, c.B, alpha);

    // ----- mřížka jednotek -----

    /// <summary>Plátno přepočtené na mřížku 32 jednotek u spodní hrany.</summary>
    private readonly struct Frame
    {
        public Frame(PixelCanvas canvas, Palette palette)
        {
            C = canvas;
            K = palette;
            U = canvas.Width / 32f;
            Offset = canvas.Height - canvas.Width;
        }

        public PixelCanvas C { get; }

        public Palette K { get; }

        /// <summary>Pixelů na jednotku.</summary>
        public float U { get; }

        /// <summary>O kolik je plátno vyšší než široké (vysoké budovy).</summary>
        public int Offset { get; }

        /// <summary>Výška plátna v jednotkách (32 u čtvercového).</summary>
        public float HeightUnits => C.Height / U;

        public int X(float v) => (int)MathF.Round(v * U);

        /// <summary>Svislá souřadnice ve čtverci u spodní hrany.</summary>
        public int Y(float v) => Offset + (int)MathF.Round(v * U);

        /// <summary>Svislá souřadnice od horní hrany celého plátna.</summary>
        public int YTop(float v) => (int)MathF.Round(v * U);

        public int Size(float v) => Math.Max(1, (int)MathF.Round(v * U));

        public void Rect(float x, float y, float w, float h, Color color) =>
            C.FillRect(X(x), Y(y), Math.Max(1, X(x + w) - X(x)), Math.Max(1, Y(y + h) - Y(y)), color);

        public void Circle(float cx, float cy, float r, Color color) => C.FillCircle(cx * U, Offset + cy * U, r * U, color);

        public void Tri(float ax, float ay, float bx, float by, float cx, float cy, Color color) =>
            C.FillTriangle(ax * U, Offset + ay * U, bx * U, Offset + by * U, cx * U, Offset + cy * U, color);

        /// <summary>Horní polovina kruhu (kopule na stěně) — spodní půlka by překryla zeď.</summary>
        public void HalfDisc(float cx, float cy, float r, Color color)
        {
            int minX = (int)MathF.Floor((cx - r) * U), maxX = (int)MathF.Ceiling((cx + r) * U);
            int minY = (int)MathF.Floor(Offset + (cy - r) * U), maxY = Offset + (int)MathF.Round(cy * U);
            float pcx = cx * U, pcy = Offset + cy * U, pr2 = r * U * r * U;
            for (int y = minY; y < maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x + 0.5f - pcx, dy = y + 0.5f - pcy;
                    if (dx * dx + dy * dy <= pr2)
                    {
                        C.Blend(x, y, color);
                    }
                }
            }
        }

        /// <summary>Vymaže obdélník (otvor, průhled) — míchání s průhlednou by nesmazalo nic.</summary>
        public void EraseRect(float x, float y, float w, float h) =>
            C.PaintRect(X(x), Y(y), Math.Max(1, X(x + w) - X(x)), Math.Max(1, Y(y + h) - Y(y)), Color.Transparent);

        public void EraseCircle(float cx, float cy, float r)
        {
            float pcx = cx * U, pcy = Offset + cy * U, pr = r * U;
            for (int y = (int)MathF.Floor(pcy - pr); y <= (int)MathF.Ceiling(pcy + pr); y++)
            {
                for (int x = (int)MathF.Floor(pcx - pr); x <= (int)MathF.Ceiling(pcx + pr); x++)
                {
                    float dx = x + 0.5f - pcx, dy = y + 0.5f - pcy;
                    if (dx * dx + dy * dy <= pr * pr)
                    {
                        C.Paint(x, y, Color.Transparent);
                    }
                }
            }
        }

        public void EraseTri(float ax, float ay, float bx, float by, float cx, float cy)
        {
            float pax = ax * U, pay = Offset + ay * U, pbx = bx * U, pby = Offset + by * U, pcx = cx * U, pcy = Offset + cy * U;
            int minX = (int)MathF.Floor(Math.Min(pax, Math.Min(pbx, pcx))), maxX = (int)MathF.Ceiling(Math.Max(pax, Math.Max(pbx, pcx)));
            int minY = (int)MathF.Floor(Math.Min(pay, Math.Min(pby, pcy))), maxY = (int)MathF.Ceiling(Math.Max(pay, Math.Max(pby, pcy)));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float d1 = (px - pbx) * (pay - pby) - (pax - pbx) * (py - pby);
                    float d2 = (px - pcx) * (pby - pcy) - (pbx - pcx) * (py - pcy);
                    float d3 = (px - pax) * (pcy - pay) - (pcx - pax) * (py - pay);
                    bool negative = d1 < 0 || d2 < 0 || d3 < 0, positive = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(negative && positive))
                    {
                        C.Paint(x, y, Color.Transparent);
                    }
                }
            }
        }

        public void Window(float x, float y, float w, float h) =>
            C.Window(X(x), Y(y), Math.Max(1, X(x + w) - X(x)), Math.Max(1, Y(y + h) - Y(y)), K.Glass);

        public void GlowSpot(float x, float y, float w, float h) =>
            C.Window(X(x), Y(y), Math.Max(1, X(x + w) - X(x)), Math.Max(1, Y(y + h) - Y(y)), K.Glow);
    }

    /// <summary>Kde je tělo budovy (v jednotkách) — prvky se kreslí vůči němu.</summary>
    private readonly record struct Body(float X, float Y, float W, float H, float TopX, float TopY);

    /// <summary>Stěna se světlou levou a stinnou pravou hranou.</summary>
    private static void ShadedBox(Frame f, float x, float y, float w, float h, Color face, Color light, Color dark)
    {
        f.Rect(x, y, w, h, face);
        f.Rect(x, y, Math.Min(1.5f, w / 4), h, light);
        f.Rect(x + w - Math.Min(2f, w / 4), y, Math.Min(2f, w / 4), h, dark);
    }

    private static void WindowGrid(Frame f, float x, float y, float w, float h, int columns, int rows)
    {
        float cw = w / columns, rh = h / rows;
        float ww = Math.Max(1.2f, cw * 0.45f), wh = Math.Max(1.2f, rh * 0.5f);
        for (int r = 0; r < rows; r++)
        {
            for (int col = 0; col < columns; col++)
            {
                f.Window(x + col * cw + (cw - ww) / 2, y + r * rh + (rh - wh) / 2, ww, wh);
            }
        }
    }

    private static void Roof(Frame f, BuildingLook look, float x, float y, float w, float peak)
    {
        var k = f.K;
        if (look.Has("flat_roof"))
        {
            f.Rect(x - 1, y - 2, w + 2, 2, k.RoofDark);
            f.Rect(x - 1, y - 3, w + 2, 1, k.RoofLight);
        }
        else if (look.Has("dome_roof"))
        {
            float r = w / 2;
            f.HalfDisc(x + r, y, r, k.Roof);
            f.HalfDisc(x + r - r * 0.3f, y - r * 0.3f, r * 0.35f, k.RoofLight);
        }
        else if (look.Has("thatch_roof"))
        {
            f.Tri(x - 2, y, x + w + 2, y, x + w / 2, y - peak, k.Roof);
            for (float s = y - peak + 2; s < y; s += 2)
            {
                float half = (s - (y - peak)) / peak * (w / 2 + 2);
                f.Rect(x + w / 2 - half, s, half * 2, 0.6f, k.RoofDark);
            }
        }
        else
        {
            f.Tri(x - 2, y, x + w + 2, y, x + w / 2, y - peak, k.Roof);
            f.Tri(x - 2, y, x + w / 2, y, x + w / 2, y - peak, k.RoofLight);
        }
    }

    // ----- tvary -----

    private static Body Hut(Frame f, BuildingLook look, int storeys)
    {
        var k = f.K;
        float top = storeys == 1 ? 16 : 10;
        ShadedBox(f, 5, top, 22, 29 - top, k.Wall, k.WallLight, k.WallDark);
        f.Rect(14, 23, 4, 6, k.AccentDark); // dveře
        if (look.Has("windows"))
        {
            WindowGrid(f, 6.5f, top + 1.5f, 19, 29 - top - 8, 3, storeys);
        }

        Roof(f, look, 5, top, 22, storeys == 1 ? 10 : 8);
        return new Body(5, top, 22, 29 - top, 16, top - (look.Has("flat_roof") ? 3 : 9));
    }

    private static Body Tower(Frame f, BuildingLook look)
    {
        var k = f.K;
        float height = f.HeightUnits;
        float topY = 5; // od horní hrany celého plátna
        float x = 9, w = 14;
        int y0 = f.YTop(topY), y1 = f.C.Height - f.Size(1);
        f.C.FillRect(f.X(x), y0, f.X(x + w) - f.X(x), y1 - y0, k.Wall);
        f.C.FillRect(f.X(x), y0, f.Size(1.5f), y1 - y0, k.WallLight);
        f.C.FillRect(f.X(x + w) - f.Size(2), y0, f.Size(2), y1 - y0, k.WallDark);

        if (look.Has("windows"))
        {
            int floors = Math.Max(3, (int)((height - topY - 4) / 3.2f));
            float floorH = (height - topY - 4) / floors;
            for (int i = 0; i < floors; i++)
            {
                float wy = topY + 1.5f + i * floorH;
                for (int col = 0; col < 3; col++)
                {
                    f.C.Window(f.X(x + 2 + col * 4f), f.YTop(wy), f.Size(1.8f), f.Size(Math.Min(1.8f, floorH * 0.6f)), k.Glass);
                }
            }
        }

        if (look.Has("spire_top"))
        {
            f.C.FillTriangle(f.X(x), f.YTop(topY), f.X(x + w), f.YTop(topY), f.X(16), f.YTop(0.5f), k.Roof);
        }
        else if (look.Has("dome_roof"))
        {
            f.C.FillCircle(f.X(16), f.YTop(topY), f.Size(7), k.Roof);
            f.C.FillCircle(f.X(14), f.YTop(topY - 2), f.Size(2.5f), k.RoofLight);
        }
        else
        {
            f.C.FillRect(f.X(x - 1), f.YTop(topY - 1.5f), f.X(x + w + 1) - f.X(x - 1), f.Size(1.5f), k.RoofDark);
        }

        float bodyTopUnits = (f.YTop(topY) - f.Offset) / f.U;
        return new Body(x, bodyTopUnits, w, 31 - bodyTopUnits, 16, bodyTopUnits - 2);
    }

    private static Body Dome(Frame f, BuildingLook look)
    {
        var k = f.K;
        ShadedBox(f, 4, 20, 24, 9, k.Wall, k.WallLight, k.WallDark);
        f.HalfDisc(16, 20, 11, k.Roof);
        f.HalfDisc(12.5f, 16, 4, k.RoofLight);
        f.Rect(4, 20, 24, 0.8f, k.RoofDark);
        if (look.Has("windows"))
        {
            WindowGrid(f, 6, 22.5f, 20, 3.5f, 5, 1);
        }

        f.Rect(14.5f, 24, 3, 5, k.AccentDark);
        return new Body(4, 9, 24, 20, 16, 8);
    }

    private static Body Hall(Frame f, BuildingLook look)
    {
        var k = f.K;
        f.Rect(2, 27, 28, 2.5f, k.WallDark); // schody
        f.Rect(3, 25.5f, 26, 1.5f, k.WallLight);
        ShadedBox(f, 4, 14, 24, 11.5f, k.Wall, k.WallLight, k.WallDark);
        for (int i = 0; i < 5; i++)
        {
            f.Rect(5.5f + i * 4.8f, 15, 1.6f, 10.5f, k.Accent); // sloupy
        }

        f.Tri(2, 14, 30, 14, 16, 6, k.Roof);
        f.Tri(2, 14, 16, 14, 16, 6, k.RoofLight);
        f.Rect(2, 13, 28, 1.2f, k.RoofDark);
        if (look.Has("windows"))
        {
            WindowGrid(f, 7, 17, 18, 5, 4, 1);
        }

        return new Body(4, 14, 24, 13, 16, 6);
    }

    private static Body Workshop(Frame f, BuildingLook look)
    {
        var k = f.K;
        ShadedBox(f, 3, 14, 18, 15, k.Wall, k.WallLight, k.WallDark);
        f.Tri(2, 14, 22, 14, 2, 8, k.Roof); // pultová střecha
        f.Tri(2, 14, 22, 14, 22, 12, k.RoofDark);
        ShadedBox(f, 21, 19, 8, 10, k.Wall, k.WallLight, k.WallDark);
        f.Rect(20.5f, 18, 9, 1.4f, k.RoofDark);
        f.Rect(8, 22, 6, 7, k.AccentDark); // vrata
        f.Rect(8, 22, 6, 0.8f, k.Accent);
        if (look.Has("windows"))
        {
            WindowGrid(f, 22.5f, 21, 5, 3, 2, 1);
        }

        return new Body(3, 14, 26, 15, 12, 8);
    }

    private static Body Tanks(Frame f, BuildingLook look)
    {
        var k = f.K;
        float[] xs = { 5, 13, 21 };
        float[] hs = { 13, 17, 11 };
        for (int i = 0; i < xs.Length; i++)
        {
            float top = 29 - hs[i];
            ShadedBox(f, xs[i], top, 7, hs[i], k.Wall, k.WallLight, k.WallDark);
            f.Circle(xs[i] + 3.5f, top, 3.5f, k.Roof);
            f.Circle(xs[i] + 2.6f, top - 1, 1.2f, k.RoofLight);
            if (look.Has("stripes"))
            {
                f.Rect(xs[i], top + hs[i] * 0.4f, 7, 1.2f, k.Accent);
            }
        }

        f.Rect(4, 25, 25, 1, k.AccentDark); // potrubí u země
        return new Body(5, 12, 23, 17, 16.5f, 10);
    }

    private static Body Pit(Frame f)
    {
        var k = f.K;
        f.Rect(1, 5, 30, 26, k.WallLight);
        f.Rect(4, 8, 24, 20, k.Wall);
        f.Rect(7, 11, 18, 14, k.WallDark);
        f.Rect(10, 14, 12, 8, Darken(k.WallDark, 0.25f));
        f.Tri(22, 9, 27, 9, 22, 26, k.Accent); // rampa
        f.Rect(26, 3, 1, 9, k.RoofDark); // jeřáb
        f.Rect(20, 3, 7, 1, k.RoofDark);
        return new Body(1, 5, 30, 26, 26, 3);
    }

    private static Body Field(Frame f)
    {
        var k = f.K;
        f.Rect(0, 3, 32, 29, k.Wall); // půda
        for (float row = 5; row < 30; row += 4)
        {
            f.Rect(1.5f, row, 29, 1.8f, k.Accent);
            f.Rect(1.5f, row, 29, 0.6f, Lighten(k.Accent, 0.25f));
        }

        f.Rect(0, 3, 32, 1, k.WallDark);
        return new Body(0, 3, 32, 29, 16, 4);
    }

    private static Body Grove(Frame f, BuildingLook look)
    {
        var k = f.K;
        (float X, float Y, float R)[] trees = { (8, 17, 6), (22, 15, 6.5f), (15, 24, 6), (26, 25, 4.5f) };
        foreach (var (x, y, r) in trees)
        {
            f.Rect(x - 0.8f, y, 1.6f, 6, k.WallDark);
            if (look.Has("crystals"))
            {
                f.Tri(x - r * 0.7f, y + 1, x + r * 0.7f, y + 1, x, y - r * 1.4f, k.Roof);
                f.Tri(x - r * 0.7f, y + 1, x, y + 1, x, y - r * 1.4f, k.RoofLight);
            }
            else
            {
                f.Circle(x, y - 1, r, k.Roof);
                f.Circle(x - r * 0.35f, y - 1 - r * 0.35f, r * 0.45f, k.RoofLight);
            }
        }

        return new Body(2, 8, 28, 22, 22, 8);
    }

    private static Body Stilts(Frame f, BuildingLook look)
    {
        var k = f.K;
        for (int i = 0; i < 4; i++)
        {
            f.Rect(6 + i * 6.3f, 21, 1.4f, 10, k.WallDark); // kůly
        }

        f.Rect(4, 20, 24, 2, k.Accent); // podlaha
        ShadedBox(f, 7, 12, 18, 8, k.Wall, k.WallLight, k.WallDark);
        f.Rect(14, 15, 4, 5, k.AccentDark);
        if (look.Has("windows"))
        {
            WindowGrid(f, 8, 13.5f, 16, 3.5f, 2, 1);
        }

        Roof(f, look, 7, 12, 18, 7);
        return new Body(7, 12, 18, 8, 16, 5);
    }

    private static Body Raft(Frame f, BuildingLook look)
    {
        var k = f.K;
        for (int i = 0; i < 6; i++)
        {
            f.Rect(3 + i * 4.3f, 23, 3.6f, 5, i % 2 == 0 ? k.Wall : k.WallDark); // klády
        }

        f.Rect(2, 27.5f, 28, 1, Alpha(new Color(210, 235, 245), 170)); // vlnky
        f.Rect(9, 30, 14, 0.8f, Alpha(new Color(210, 235, 245), 140));
        ShadedBox(f, 9, 15, 14, 8, k.Roof, k.RoofLight, k.RoofDark);
        if (look.Has("windows"))
        {
            WindowGrid(f, 10, 17, 12, 3, 2, 1);
        }

        Roof(f, look, 9, 15, 14, 6);
        return new Body(3, 15, 26, 13, 16, 9);
    }

    private static Body Balloon(Frame f, BuildingLook look)
    {
        var k = f.K;
        f.Rect(10, 27, 12, 2.5f, k.WallDark); // kotva
        f.Rect(11, 26, 10, 1.2f, k.Wall);
        (float X, float Y, float R)[] balloons = { (16, 11, 8), (7, 17, 4.5f), (25, 16, 4.5f) };
        foreach (var (x, y, r) in balloons)
        {
            f.Rect(x - 0.3f, y + r * 0.8f, 0.6f, 26 - y - r * 0.8f, k.AccentDark); // lano
            f.Circle(x, y, r, k.Roof);
            if (look.Has("stripes"))
            {
                f.Rect(x - r * 0.25f, y - r, r * 0.5f, r * 2, k.Accent);
            }

            f.Circle(x - r * 0.35f, y - r * 0.35f, r * 0.35f, k.RoofLight);
        }

        return new Body(3, 3, 26, 26, 16, 3);
    }

    private static Body Mast(Frame f, BuildingLook look)
    {
        var k = f.K;
        f.Rect(7, 26.5f, 18, 3, k.WallDark); // patka
        f.Rect(8, 25.5f, 16, 1.2f, k.WallLight);
        f.Tri(16, 10, 16.4f, 10, 5, 26.5f, Alpha(k.AccentDark, 200)); // kotevní lana
        f.Tri(16, 10, 15.6f, 10, 27, 26.5f, Alpha(k.AccentDark, 200));
        f.Tri(12, 28, 20, 28, 16, 3, k.Wall); // příhradový stožár
        f.EraseTri(14, 28, 18, 28, 16, 7);
        for (float y = 8; y < 27; y += 3.5f)
        {
            float half = (y - 3) / 25f * 4f;
            f.Rect(16 - half, y, half * 2, 0.7f, k.WallDark);
        }

        f.Circle(16, 3, 1.4f, k.Accent);
        return new Body(12, 3, 8, 26, 16, 2);
    }

    private static Body Column(Frame f)
    {
        var k = f.K;
        f.Rect(8, 26, 16, 3.5f, k.WallDark); // podstavec
        f.Rect(9, 24.5f, 14, 1.5f, k.WallLight);
        ShadedBox(f, 13, 7, 6, 17.5f, k.Wall, k.WallLight, k.WallDark);
        f.Rect(11.5f, 5.5f, 9, 1.8f, k.Roof); // hlavice
        f.Circle(16, 3.5f, 2.3f, k.Accent);
        f.GlowSpot(15.3f, 2.8f, 1.4f, 1.4f);
        return new Body(13, 7, 6, 18, 16, 1);
    }

    private static Body Obelisk(Frame f)
    {
        var k = f.K;
        f.Rect(10, 27, 12, 2.5f, k.WallDark);
        f.Tri(12, 27, 20, 27, 16.8f, 6, k.Wall);
        f.Tri(12, 27, 16, 27, 15.2f, 6, k.WallLight);
        f.Tri(14.2f, 7, 17.8f, 7, 16, 2, k.Roof); // pyramidion
        for (float y = 11; y < 24; y += 4)
        {
            f.Rect(15.4f, y, 1.2f, 1.5f, k.Accent); // znaky
        }

        return new Body(12, 6, 8, 21, 16, 2);
    }

    private static Body Arch(Frame f)
    {
        var k = f.K;
        ShadedBox(f, 4, 10, 6, 19, k.Wall, k.WallLight, k.WallDark);
        ShadedBox(f, 22, 10, 6, 19, k.Wall, k.WallLight, k.WallDark);
        f.Rect(3, 6, 26, 5, k.Wall);
        f.Rect(3, 5, 26, 1.2f, k.Roof);
        f.EraseCircle(16, 13, 6);
        f.EraseRect(10, 13, 12, 16);
        f.Rect(14.5f, 7, 3, 3, k.Accent); // klenák
        return new Body(4, 6, 24, 23, 16, 4);
    }

    private static Body Crystal(Frame f)
    {
        var k = f.K;
        f.Rect(6, 27, 20, 2.5f, k.WallDark);
        (float X, float H, float W)[] spikes = { (10, 14, 5), (16, 22, 6.5f), (22, 16, 5), (13, 9, 3.5f), (20, 10, 3.5f) };
        foreach (var (x, h, w) in spikes)
        {
            f.Tri(x - w / 2, 28, x + w / 2, 28, x, 28 - h, k.Roof);
            f.Tri(x - w / 2, 28, x, 28, x, 28 - h, k.RoofLight);
        }

        f.GlowSpot(15, 17, 2, 3);
        return new Body(6, 6, 20, 22, 16, 5);
    }

    private static Body Bulb(Frame f, BuildingLook look)
    {
        var k = f.K;
        f.Rect(14, 18, 4, 11, k.Wall); // stonek
        f.Circle(16, 13, 9, k.Roof);
        f.Circle(12.5f, 9.5f, 3.4f, k.RoofLight);
        f.Circle(21, 29, 3, k.Wall);
        f.Circle(10.5f, 29, 2.6f, k.Wall);
        f.GlowSpot(18, 12, 2, 2);
        f.GlowSpot(13, 15, 1.5f, 1.5f);
        if (look.Has("windows"))
        {
            f.Window(15, 21, 2, 2);
        }

        return new Body(7, 4, 18, 25, 16, 4);
    }

    private static Body Tree(Frame f)
    {
        var k = f.K;
        f.Tri(12, 30, 20, 30, 16, 12, k.Wall); // kmen
        f.Rect(14.5f, 12, 3, 18, k.Wall);
        f.Rect(14.5f, 12, 1, 18, k.WallLight);
        f.Circle(16, 10, 10, k.Roof);
        f.Circle(9, 13, 5.5f, k.Roof);
        f.Circle(23, 13, 5.5f, k.Roof);
        f.Circle(12.5f, 6.5f, 4.5f, k.RoofLight);
        f.GlowSpot(10, 12, 1.4f, 1.4f);
        f.GlowSpot(21, 9, 1.4f, 1.4f);
        f.GlowSpot(16, 15, 1.4f, 1.4f);
        return new Body(6, 1, 20, 29, 16, 0);
    }

    private static Body Wall(Frame f)
    {
        var k = f.K;
        ShadedBox(f, 0, 17, 32, 12, k.Wall, k.WallLight, k.WallDark);
        for (float y = 19; y < 29; y += 3)
        {
            f.Rect(0, y, 32, 0.6f, k.WallDark); // spáry
        }

        for (float x = 0; x < 32; x += 6)
        {
            f.Rect(x, 14.5f, 3.5f, 2.5f, k.Roof); // cimbuří
        }

        return new Body(0, 15, 32, 14, 16, 14);
    }

    private static Body Channel(Frame f)
    {
        var k = f.K;
        f.Rect(0, 10, 32, 14, k.WallDark);
        f.Rect(0, 12, 32, 10, k.Accent);
        f.Rect(0, 12, 32, 1.2f, Lighten(k.Accent, 0.35f));
        f.Rect(0, 10, 32, 1.5f, k.WallLight);
        f.Rect(0, 22.5f, 32, 1.5f, k.Wall);
        return new Body(0, 10, 32, 14, 16, 10);
    }

    private static Body Pool(Frame f)
    {
        var k = f.K;
        f.Circle(16, 19, 12, k.Wall);
        f.Circle(16, 19, 9.5f, k.Accent);
        f.Circle(13, 16.5f, 3, Lighten(k.Accent, 0.35f));
        return new Body(4, 7, 24, 24, 16, 7);
    }

    private static Body Platform(Frame f)
    {
        var k = f.K;
        f.Rect(0, 2, 32, 30, k.Wall);
        for (float x = 0; x < 32; x += 4)
        {
            f.Rect(x, 2, 0.6f, 30, k.WallDark); // prkna mříže
        }

        f.Rect(0, 2, 32, 1.2f, k.Roof); // zábradlí
        f.Rect(0, 30.5f, 32, 1.2f, k.RoofDark);
        f.Rect(0, 2, 1.2f, 30, k.Roof);
        f.Rect(30.8f, 2, 1.2f, 30, k.RoofDark);
        return new Body(0, 2, 32, 30, 16, 2);
    }

    private static Body Vortex(Frame f)
    {
        var k = f.K;
        f.Circle(16, 16, 15, k.WallDark);
        for (int arm = 0; arm < 3; arm++)
        {
            for (float t = 0; t < 1f; t += 0.04f)
            {
                float angle = arm * MathF.Tau / 3 + t * 5.5f;
                float r = 2 + t * 12;
                f.Circle(16 + MathF.Cos(angle) * r, 16 + MathF.Sin(angle) * r, 1.2f + t * 1.6f, t < 0.5f ? k.Roof : k.Accent);
            }
        }

        f.Circle(16, 16, 2.5f, Darken(k.WallDark, 0.5f));
        f.GlowSpot(19, 11, 1.2f, 1.2f);
        f.GlowSpot(10, 20, 1.2f, 1.2f);
        return new Body(1, 1, 30, 30, 16, 1);
    }

    private static Body Ring(Frame f)
    {
        var k = f.K;
        f.Rect(6, 27, 20, 3, k.WallDark); // podstava
        f.Circle(16, 15, 13, k.Wall);
        f.Circle(16, 15, 10, Alpha(k.Glow, 210));
        f.Circle(16, 15, 6, Alpha(Lighten(k.Glow, 0.5f), 230));
        f.Circle(9, 8.5f, 2, k.WallLight);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * MathF.Tau / 8;
            f.Rect(16 + MathF.Cos(angle) * 11.5f - 0.8f, 15 + MathF.Sin(angle) * 11.5f - 0.8f, 1.6f, 1.6f, k.Accent);
        }

        f.GlowSpot(14, 13, 4, 4);
        return new Body(3, 2, 26, 28, 16, 1);
    }

    private static Body Mirrors(Frame f)
    {
        var k = f.K;
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                if (row == 1 && (col == 1 || col == 2))
                {
                    continue; // místo pro věž
                }

                float x = 2 + col * 7.5f, y = 6 + row * 8;
                f.Rect(x, y + 4, 5.5f, 2.5f, k.Accent);
                f.Rect(x, y + 4, 5.5f, 0.8f, Lighten(k.Accent, 0.5f));
                f.Rect(x + 2.5f, y + 6.5f, 0.6f, 1.5f, k.WallDark);
            }
        }

        ShadedBox(f, 14, 6, 4, 22, k.Wall, k.WallLight, k.WallDark); // věž
        f.GlowSpot(14.5f, 5, 3, 2);
        return new Body(2, 5, 28, 25, 16, 4);
    }

    private static Body Pier(Frame f)
    {
        var k = f.K;
        f.Rect(12, 2, 8, 30, k.Wall); // molo
        for (float y = 3; y < 32; y += 2.5f)
        {
            f.Rect(12, y, 8, 0.5f, k.WallDark);
        }

        for (float y = 6; y < 32; y += 8)
        {
            f.Rect(11, y, 1.2f, 1.6f, k.WallDark);
            f.Rect(19.8f, y, 1.2f, 1.6f, k.WallDark);
        }

        f.Tri(3, 13, 9, 13, 6, 22, k.Roof); // loďky
        f.Rect(4, 13, 4, 1.2f, k.RoofDark);
        f.Tri(23, 20, 29, 20, 26, 29, k.Roof);
        f.Rect(24, 20, 4, 1.2f, k.RoofDark);
        return new Body(12, 2, 8, 30, 16, 2);
    }

    private static Body Rig(Frame f)
    {
        var k = f.K;
        f.Circle(16, 26, 8, Alpha(k.Accent, 220)); // jezírko/zdroj
        f.Tri(9, 27, 23, 27, 16, 3, k.Wall); // věž
        f.EraseTri(11.5f, 27, 20.5f, 27, 16, 8);
        for (float y = 9; y < 26; y += 4)
        {
            float half = (y - 3) / 24f * 7f;
            f.Rect(16 - half, y, half * 2, 0.7f, k.WallDark);
        }

        f.Rect(15.3f, 3, 1.4f, 24, k.WallDark); // vrtná trubka
        f.Rect(13, 2, 6, 1.4f, k.Roof);
        return new Body(9, 3, 14, 24, 16, 2);
    }

    private static Body Pods(Frame f)
    {
        var k = f.K;
        f.Rect(4, 25, 24, 2, k.WallDark); // rám
        (float X, float Y, float R)[] pods = { (9, 18, 5.5f), (21, 17, 6), (15, 10, 5) };
        foreach (var (x, y, r) in pods)
        {
            f.Rect(x - 0.6f, y, 1.2f, 25 - y, k.Wall);
            f.Circle(x, y, r, k.Roof);
            f.Circle(x - r * 0.3f, y - r * 0.3f, r * 0.35f, k.RoofLight);
            f.Window(x - 1.2f, y - 1.2f, 2.4f, 2.4f);
        }

        return new Body(3, 5, 26, 22, 15, 5);
    }

    // ----- prvky -----

    private static void Feature(Frame f, string feature, Body b)
    {
        var k = f.K;
        switch (feature)
        {
            case "windows":
            case "flat_roof":
            case "dome_roof":
            case "thatch_roof":
            case "spire_top":
            case "stripes":
                // Kreslí je tvar sám — patří do jeho stavby, ne navrch.
                break;
            case "chimney":
                f.Rect(b.X + b.W - 5, b.Y - 5, 2.5f, 5, k.WallDark);
                f.Rect(b.X + b.W - 5.5f, b.Y - 5.5f, 3.5f, 1, k.Wall);
                break;
            case "antenna":
                f.Rect(b.TopX - 0.3f, b.TopY - 6, 0.6f, 6, k.WallDark);
                f.GlowSpot(b.TopX - 0.6f, b.TopY - 6.8f, 1.2f, 1.2f);
                break;
            case "flag":
                f.Rect(b.TopX - 0.3f, b.TopY - 7, 0.6f, 7, k.WallDark);
                f.Tri(b.TopX + 0.3f, b.TopY - 7, b.TopX + 0.3f, b.TopY - 4, b.TopX + 4.5f, b.TopY - 5.5f, k.Accent);
                break;
            case "lanterns":
                for (float x = b.X + 1.5f; x < b.X + b.W - 1; x += 5)
                {
                    f.GlowSpot(x, b.Y + b.H - 3, 1.2f, 1.2f);
                }

                break;
            case "glow":
                f.GlowSpot(b.X + b.W * 0.3f, b.Y + b.H * 0.35f, 2, 2);
                f.GlowSpot(b.X + b.W * 0.65f, b.Y + b.H * 0.55f, 2, 2);
                break;
            case "plants":
                f.Circle(b.X - 0.5f, 28, 2.6f, new Color(84, 142, 72));
                f.Circle(b.X + b.W + 0.5f, 28.5f, 2.2f, new Color(96, 158, 80));
                f.Circle(b.X + 1, 27.3f, 1.2f, new Color(126, 186, 98));
                break;
            case "snow":
                f.Rect(b.X - 1, b.TopY + 1, b.W + 2, 1.2f, new Color(240, 246, 250));
                f.Circle(b.TopX, b.TopY + 1, 2.2f, new Color(240, 246, 250));
                break;
            case "pipes":
                f.Rect(b.X - 2, b.Y + b.H * 0.5f, 2, 0.9f, k.AccentDark);
                f.Rect(b.X - 2, b.Y + b.H * 0.5f, 0.9f, b.H * 0.5f, k.AccentDark);
                f.Rect(b.X + b.W, b.Y + b.H * 0.3f, 2, 0.9f, k.AccentDark);
                f.Rect(b.X + b.W + 1.1f, b.Y + b.H * 0.3f, 0.9f, b.H * 0.7f, k.AccentDark);
                break;
            case "sails":
                f.Rect(b.X + b.W + 1, b.Y - 6, 0.6f, b.H + 6, k.WallDark);
                f.Tri(b.X + b.W + 1.6f, b.Y - 6, b.X + b.W + 1.6f, b.Y + 2, b.X + b.W + 6, b.Y + 1, new Color(236, 230, 214));
                break;
            case "solar":
                f.Rect(b.X + 1, b.Y - 1.8f, b.W * 0.45f, 1.6f, new Color(52, 78, 128));
                f.Rect(b.X + 1, b.Y - 1.8f, b.W * 0.45f, 0.5f, new Color(120, 160, 210));
                break;
            case "crystals":
                f.Tri(b.X - 1.5f, 29, b.X + 1.5f, 29, b.X, 24, Lighten(k.Glow, 0.3f));
                f.Tri(b.X + b.W - 1.5f, 29, b.X + b.W + 1.5f, 29, b.X + b.W, 23, Lighten(k.Glow, 0.3f));
                break;
            case "rings":
                f.Rect(b.TopX - 9, b.TopY + 3, 18, 0.9f, Alpha(k.Glow, 200));
                f.Rect(b.TopX - 6, b.TopY + 1.5f, 12, 0.7f, Alpha(k.Glow, 150));
                break;
            case "steam":
                f.Circle(b.TopX + 2, b.TopY - 3, 2.4f, Alpha(Color.White, 150));
                f.Circle(b.TopX + 4, b.TopY - 6, 1.8f, Alpha(Color.White, 110));
                break;
            case "water":
                f.Rect(0, 29.5f, 32, 2.5f, Alpha(new Color(70, 150, 200), 200));
                f.Rect(3, 30, 8, 0.6f, Alpha(Color.White, 150));
                f.Rect(19, 30.8f, 9, 0.6f, Alpha(Color.White, 130));
                break;
            case "sand":
                f.Circle(b.X + 2, 30, 3.5f, new Color(222, 196, 140));
                f.Circle(b.X + b.W - 3, 30.5f, 3, new Color(212, 184, 128));
                break;
            case "arches":
                for (float x = b.X + 2; x < b.X + b.W - 3; x += 5.5f)
                {
                    f.Circle(x + 1.5f, b.Y + b.H - 5, 1.5f, k.WallDark);
                    f.Rect(x, b.Y + b.H - 5, 3, 5, k.WallDark);
                }

                break;
            case "tendrils":
                for (int i = 0; i < 4; i++)
                {
                    float x = b.X + i * (b.W / 3.2f);
                    f.Rect(x, 26 - i % 2 * 2, 0.8f, 4 + i % 2 * 2, k.Roof);
                    f.Circle(x + 0.4f, 26 - i % 2 * 2, 1, k.RoofLight);
                }

                break;
            case "rotor":
                f.Rect(b.TopX - 0.5f, b.TopY - 0.5f, 1, 1, k.WallDark);
                f.Tri(b.TopX, b.TopY, b.TopX - 1, b.TopY - 8, b.TopX + 1, b.TopY - 8, k.WallLight);
                f.Tri(b.TopX, b.TopY, b.TopX + 7, b.TopY + 3, b.TopX + 6, b.TopY + 5, k.WallLight);
                f.Tri(b.TopX, b.TopY, b.TopX - 7, b.TopY + 3, b.TopX - 6, b.TopY + 5, k.WallLight);
                break;
            case "spark":
                f.Tri(b.TopX + 1, b.TopY - 7, b.TopX - 1.5f, b.TopY - 3, b.TopX + 0.5f, b.TopY - 3, k.Glow);
                f.Tri(b.TopX + 0.5f, b.TopY - 3.5f, b.TopX - 1, b.TopY + 1, b.TopX + 1.5f, b.TopY - 3.5f, k.Glow);
                f.GlowSpot(b.TopX - 0.5f, b.TopY - 1, 1, 1);
                break;
            case "ropes":
                f.Tri(b.TopX, b.TopY, b.TopX - 0.3f, b.TopY, 2, 30, Alpha(k.WallDark, 180));
                f.Tri(b.TopX, b.TopY, b.TopX + 0.3f, b.TopY, 30, 30, Alpha(k.WallDark, 180));
                break;
            default:
                throw new ArgumentException($"Malíř neumí prvek '{feature}'.", nameof(feature));
        }
    }
}
