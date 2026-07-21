using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Sprites;

/// <summary>
/// Procedurálně vygenerovaná knihovna spritů a ikon (dokud nejsou skutečné assety):
/// ikony surovin, sprity budov, těžitelné objekty (strom, kámen) a agenti (chodci,
/// vozíky). Sprity se generují jednou při startu a odkazují se stabilním ID
/// (stejný princip jako budoucí atlas). Neznámé ID vrací <c>null</c> → volající
/// se elegantně vrátí k barevnému obdélníku.
///
/// „Kód = jak" (kresba), „data = co" (které ID která budova/surovina používá).
/// Až přijdou hotové textury, tahle třída se nahradí načtením atlasu beze změny
/// volajících.
/// </summary>
public sealed class SpriteLibrary : IDisposable
{
    /// <summary>Rozlišení, ve kterém se sprity kreslí (pak se škálují na dlaždice).</summary>
    public const int SpriteSize = 32;

    /// <summary>Rozlišení ikon surovin.</summary>
    public const int IconSize = 24;

    private readonly Dictionary<string, Texture2D> _sprites = new(StringComparer.Ordinal);

    public SpriteLibrary(GraphicsDevice device)
    {
        // Suroviny (ikony do HUD).
        Add(device, "icon.wood", IconSize, WoodIcon);
        Add(device, "icon.planks", IconSize, PlanksIcon);
        Add(device, "icon.stone", IconSize, StoneIcon);
        Add(device, "icon.food", IconSize, FoodIcon);
        Add(device, "icon.tools", IconSize, ToolsIcon);

        // Těžitelné objekty na terénu.
        Add(device, "node.tree", SpriteSize, Tree);
        Add(device, "node.rock", SpriteSize, Rock);
        Add(device, "node.stump", SpriteSize, Stump);
        Add(device, "node.rubble", SpriteSize, Rubble);

        // Budovy.
        Add(device, "building.house", SpriteSize, canvas => House(canvas, new Color(196, 110, 66)));
        Add(device, "building.cottage", SpriteSize, canvas => House(canvas, new Color(176, 86, 74)));
        Add(device, "building.lumber_camp", SpriteSize, LumberCamp);
        Add(device, "building.lumberyard", SpriteSize, LumberCamp);
        Add(device, "building.sawmill", SpriteSize, Sawmill);
        Add(device, "building.quarry", SpriteSize, Quarry);
        Add(device, "building.mine", SpriteSize, Mine);
        Add(device, "building.farm", SpriteSize, Farm);
        Add(device, "building.plantation", SpriteSize, Farm);
        Add(device, "building.warehouse", SpriteSize, Warehouse);
        Add(device, "building.windmill", SpriteSize, Windmill);
        Add(device, "building.market", SpriteSize, Market);
        Add(device, "building.toolmaker", SpriteSize, Toolmaker);

        // Agenti (živý svět).
        Add(device, "agent.person", 12, Person);
        Add(device, "agent.cart", 18, Cart);

        // Efekty: měkký kontaktní stín pod objekty (ať „sedí" na terénu).
        Add(device, "fx.shadow", SpriteSize, Shadow);
        Add(device, "fx.bubble", SpriteSize, Bubble);   // sběrná bublina nad budovou
        Add(device, "fx.golden", SpriteSize, Golden);   // zlatý spawn (klikni!)
    }

    /// <summary>Sprite podle ID, nebo <c>null</c>, když neexistuje.</summary>
    public Texture2D? Get(string id) => _sprites.GetValueOrDefault(id);

    public void Dispose()
    {
        foreach (var texture in _sprites.Values)
        {
            texture.Dispose();
        }

        _sprites.Clear();
    }

    private void Add(GraphicsDevice device, string id, int size, Action<PixelCanvas> draw)
    {
        var canvas = new PixelCanvas(size, size);
        draw(canvas);
        _sprites[id] = canvas.ToTexture(device);
    }

    /// <summary>Sběrná bublina: světlý poloprůhledný puchýř s odleskem (nese ikonu suroviny).</summary>
    private static void Bubble(PixelCanvas canvas)
    {
        float cx = canvas.Width * 0.5f, cy = canvas.Height * 0.5f;
        canvas.FillCircle(cx, cy, canvas.Width * 0.44f, new Color(150, 205, 255, 150));
        canvas.FillCircle(cx, cy, canvas.Width * 0.36f, new Color(225, 242, 255, 170));
        canvas.FillCircle(cx - canvas.Width * 0.12f, cy - canvas.Height * 0.12f, canvas.Width * 0.09f, new Color(255, 255, 255, 220));
    }

    /// <summary>Zlatý spawn: zářivý kosočtverec se třpytem — „klikni na mě".</summary>
    private static void Golden(PixelCanvas canvas)
    {
        float cx = canvas.Width * 0.5f, cy = canvas.Height * 0.5f, r = canvas.Width * 0.4f;
        var gold = new Color(255, 208, 84);
        canvas.FillCircle(cx, cy, r * 1.15f, new Color(255, 235, 150, 110)); // záře
        canvas.FillTriangle(cx, cy - r, cx - r * 0.68f, cy, cx + r * 0.68f, cy, gold);
        canvas.FillTriangle(cx, cy + r, cx - r * 0.68f, cy, cx + r * 0.68f, cy, gold);
        canvas.FillCircle(cx - r * 0.18f, cy - r * 0.18f, r * 0.13f, Color.White);
    }

    /// <summary>Měkký kruhový stín (černá s radiálním doběhem alfy) — kreslí se zploštělý pod objekt.</summary>
    private static void Shadow(PixelCanvas canvas)
    {
        float cx = canvas.Width * 0.5f, cy = canvas.Height * 0.5f;
        float radius = canvas.Width * 0.5f;
        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                float dx = (x + 0.5f - cx) / radius, dy = (y + 0.5f - cy) / radius;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                if (d < 1f)
                {
                    float a = 1f - d;
                    canvas.Blend(x, y, new Color(0, 0, 0, (int)(a * a * 150f)));
                }
            }
        }
    }

    // ----- ikony surovin (24×24) -----

    private static void WoodIcon(PixelCanvas c)
    {
        // Tři naskládané klády s letokruhy.
        var bark = new Color(120, 78, 44);
        var inner = new Color(197, 150, 96);
        for (int i = 0; i < 3; i++)
        {
            int y = 5 + i * 6;
            c.FillRect(3, y, 18, 5, bark);
            c.FillCircle(5f, y + 2.5f, 1.8f, inner);
            c.FillCircle(19f, y + 2.5f, 1.8f, inner);
        }
    }

    private static void PlanksIcon(PixelCanvas c)
    {
        var board = new Color(206, 170, 108);
        var edge = new Color(150, 116, 66);
        for (int i = 0; i < 4; i++)
        {
            int y = 4 + i * 4;
            c.FillRect(3, y, 18, 3, board);
            c.FillRect(3, y + 2, 18, 1, edge);
        }
    }

    private static void StoneIcon(PixelCanvas c)
    {
        c.FillCircle(9f, 14f, 6f, new Color(140, 143, 150));
        c.FillCircle(16f, 11f, 5f, new Color(165, 168, 175));
        c.FillCircle(13f, 16f, 4f, new Color(120, 123, 130));
    }

    private static void FoodIcon(PixelCanvas c)
    {
        // Jablko + lístek.
        c.FillCircle(12f, 14f, 7f, new Color(206, 74, 62));
        c.FillCircle(9f, 12f, 2.5f, new Color(236, 120, 108));
        c.FillRect(11, 4, 2, 4, new Color(120, 78, 44));
        c.FillCircle(15f, 6f, 2.5f, new Color(90, 168, 74));
    }

    private static void ToolsIcon(PixelCanvas c)
    {
        // Zkřížené kladivo a klíč.
        c.FillRect(6, 5, 3, 14, new Color(120, 92, 56));   // topůrko kladiva
        c.FillRect(4, 4, 8, 4, new Color(150, 153, 160));  // hlava kladiva
        c.FillRect(15, 5, 3, 14, new Color(150, 153, 160)); // klíč
        c.FillCircle(16.5f, 5.5f, 3f, new Color(180, 183, 190));
        c.FillCircle(16.5f, 5.5f, 1.4f, new Color(90, 92, 98));
    }

    // ----- těžitelné objekty (32×32, kotva dole uprostřed) -----

    private static void Tree(PixelCanvas c)
    {
        c.FillRect(14, 20, 4, 11, new Color(96, 64, 38)); // kmen
        c.FillCircle(16f, 14f, 9f, new Color(46, 104, 46));
        c.FillCircle(11f, 12f, 6f, new Color(58, 124, 56));
        c.FillCircle(21f, 12f, 6f, new Color(40, 96, 42));
        c.FillCircle(16f, 8f, 6f, new Color(64, 138, 60));
    }

    private static void Rock(PixelCanvas c)
    {
        c.FillCircle(12f, 22f, 8f, new Color(120, 123, 130));
        c.FillCircle(20f, 20f, 7f, new Color(146, 149, 156));
        c.FillCircle(16f, 24f, 8f, new Color(103, 106, 113));
        c.FillCircle(19f, 17f, 3f, new Color(170, 173, 180)); // odlesk
    }

    private static void Stump(PixelCanvas c)
    {
        c.FillRect(13, 24, 6, 6, new Color(96, 64, 38));
        c.FillCircle(16f, 24f, 3.4f, new Color(150, 110, 70));
    }

    private static void Rubble(PixelCanvas c)
    {
        // Rozbitý kámen = pár drobných úlomků, žádný „pařez".
        c.FillCircle(13f, 27f, 2.5f, new Color(120, 123, 130));
        c.FillCircle(18f, 26f, 3f, new Color(140, 143, 150));
        c.FillCircle(16f, 28f, 2f, new Color(110, 113, 120));
    }

    // ----- budovy (32×32) -----

    private static void House(PixelCanvas c, Color wall)
    {
        c.FillRect(6, 16, 20, 14, wall);
        c.FillTriangle(4f, 16f, 28f, 16f, 16f, 5f, new Color(150, 60, 48)); // střecha
        c.FillRect(14, 22, 5, 8, new Color(96, 64, 38)); // dveře
        c.FillRect(9, 19, 4, 4, new Color(150, 205, 225)); // okno
        c.FillRect(20, 19, 4, 4, new Color(150, 205, 225));
    }

    private static void LumberCamp(PixelCanvas c)
    {
        c.FillRect(5, 18, 22, 12, new Color(120, 92, 56));
        c.FillTriangle(3f, 18f, 29f, 18f, 16f, 9f, new Color(92, 68, 42));
        // Hromada klád vedle.
        c.FillRect(6, 24, 8, 3, new Color(150, 110, 66));
        c.FillRect(6, 27, 8, 3, new Color(128, 92, 52));
    }

    private static void Sawmill(PixelCanvas c)
    {
        c.FillRect(5, 16, 22, 14, new Color(140, 110, 70));
        c.FillTriangle(3f, 16f, 29f, 16f, 16f, 7f, new Color(110, 84, 52));
        // Kotouč pily.
        c.FillCircle(20f, 23f, 5f, new Color(210, 210, 215));
        c.FillCircle(20f, 23f, 2f, new Color(120, 120, 128));
    }

    private static void Quarry(PixelCanvas c)
    {
        // Jáma s kameny.
        c.FillCircle(16f, 20f, 12f, new Color(96, 92, 84));
        c.FillCircle(16f, 21f, 9f, new Color(120, 116, 108));
        c.FillCircle(11f, 22f, 3f, new Color(150, 150, 156));
        c.FillCircle(20f, 19f, 3.5f, new Color(140, 140, 146));
        c.FillCircle(17f, 24f, 3f, new Color(160, 160, 166));
    }

    private static void Farm(PixelCanvas c)
    {
        c.FillRect(2, 4, 28, 26, new Color(150, 116, 60)); // pole
        var crop = new Color(196, 168, 78);
        for (int row = 0; row < 5; row++)
        {
            c.FillRect(4, 6 + row * 5, 24, 3, crop);
        }
    }

    private static void Warehouse(PixelCanvas c)
    {
        c.FillRect(3, 12, 26, 18, new Color(126, 96, 66));
        c.FillRect(3, 12, 26, 4, new Color(96, 72, 50)); // horní pruh
        c.FillRect(12, 18, 8, 12, new Color(80, 60, 42)); // vrata
        c.FillRect(12, 18, 8, 2, new Color(160, 130, 96));
    }

    private static void Mine(PixelCanvas c)
    {
        Quarry(c);
        // Vstup do dolu s trámovým rámem.
        c.FillRect(13, 16, 6, 8, new Color(50, 46, 42));
        c.FillRect(12, 15, 8, 2, new Color(110, 84, 52));
    }

    private static void Windmill(PixelCanvas c)
    {
        c.FillRect(12, 14, 8, 16, new Color(210, 196, 150)); // věž
        c.FillTriangle(10f, 14f, 22f, 14f, 16f, 8f, new Color(150, 110, 70));
        // Lopatky mlýna.
        c.FillRect(15, 3, 2, 14, new Color(120, 92, 56));
        c.FillRect(9, 9, 14, 2, new Color(120, 92, 56));
    }

    private static void Market(PixelCanvas c)
    {
        c.FillRect(4, 16, 24, 14, new Color(180, 138, 84)); // pult
        // Pruhovaná markýza.
        for (int i = 0; i < 6; i++)
        {
            var stripe = i % 2 == 0 ? new Color(200, 80, 70) : new Color(235, 225, 210);
            c.FillTriangle(3 + i * 4, 16, 7 + i * 4, 16, 5 + i * 4, 20, stripe);
        }

        c.FillRect(4, 12, 24, 5, new Color(190, 74, 64));
    }

    private static void Toolmaker(PixelCanvas c)
    {
        c.FillRect(5, 16, 22, 14, new Color(150, 120, 78));
        c.FillTriangle(3f, 16f, 29f, 16f, 16f, 8f, new Color(110, 84, 52));
        // Kladivo ve štítu.
        c.FillRect(15, 19, 2, 8, new Color(90, 68, 44));
        c.FillRect(12, 18, 8, 3, new Color(170, 173, 180));
    }

    // ----- agenti -----

    private static void Person(PixelCanvas c)
    {
        c.FillCircle(6f, 3.5f, 2.4f, new Color(232, 194, 160)); // hlava
        c.FillRect(4, 5, 4, 6, new Color(70, 110, 180)); // tělo
    }

    private static void Cart(PixelCanvas c)
    {
        c.FillRect(2, 4, 12, 6, new Color(140, 100, 60)); // korba
        c.FillCircle(5f, 12f, 2.6f, new Color(50, 40, 32)); // kola
        c.FillCircle(12f, 12f, 2.6f, new Color(50, 40, 32));
    }
}
