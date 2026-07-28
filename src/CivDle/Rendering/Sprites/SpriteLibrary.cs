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
        Add(device, "icon.copper_ore", IconSize, canvas => OreIcon(canvas, new Color(196, 120, 66)));
        Add(device, "icon.iron_ore", IconSize, canvas => OreIcon(canvas, new Color(150, 140, 132)));
        Add(device, "icon.coal", IconSize, canvas => OreIcon(canvas, new Color(62, 60, 64)));
        Add(device, "icon.silicon", IconSize, canvas => OreIcon(canvas, new Color(158, 168, 184)));
        Add(device, "icon.bronze", IconSize, canvas => IngotIcon(canvas, new Color(206, 140, 76)));
        Add(device, "icon.iron", IconSize, canvas => IngotIcon(canvas, new Color(168, 170, 176)));
        Add(device, "icon.steel", IconSize, canvas => IngotIcon(canvas, new Color(126, 140, 158)));
        Add(device, "icon.nanomaterial", IconSize, canvas => IngotIcon(canvas, new Color(178, 132, 220)));
        Add(device, "icon.machine_parts", IconSize, GearIcon);
        Add(device, "icon.electronics", IconSize, ChipIcon);
        Add(device, "icon.computer", IconSize, ComputerIcon);
        Add(device, "icon.robot", IconSize, RobotIcon);

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

        // Éra kamene a dřeva.
        Add(device, "building.granary", SpriteSize, Granary);
        Add(device, "building.depot", SpriteSize, Depot);
        Add(device, "building.workshop", SpriteSize, Workshop);
        Add(device, "building.charcoal_kiln", SpriteSize, CharcoalKiln);
        Add(device, "building.fishing_hut", SpriteSize, FishingHut);
        Add(device, "building.hunters_lodge", SpriteSize, HuntersLodge);
        Add(device, "building.brick_house", SpriteSize, canvas => TownHouse(canvas, new Color(178, 96, 78), floors: 2));
        Add(device, "building.tenement", SpriteSize, canvas => TownHouse(canvas, new Color(150, 108, 92), floors: 3));

        // Doly a hutě.
        Add(device, "building.iron_mine", SpriteSize, canvas => MineShaft(canvas, new Color(120, 112, 108)));
        Add(device, "building.coal_mine", SpriteSize, canvas => MineShaft(canvas, new Color(58, 56, 60)));
        Add(device, "building.copper_mine", SpriteSize, canvas => MineShaft(canvas, new Color(178, 108, 62)));
        Add(device, "building.silicon_mine", SpriteSize, canvas => MineShaft(canvas, new Color(150, 160, 178)));
        Add(device, "building.smeltery", SpriteSize, canvas => Furnace(canvas, new Color(196, 118, 62)));
        Add(device, "building.blast_furnace", SpriteSize, canvas => Furnace(canvas, new Color(120, 104, 100)));
        Add(device, "building.bronze_smith", SpriteSize, canvas => Smithy(canvas, new Color(196, 130, 70)));
        Add(device, "building.iron_forge", SpriteSize, canvas => Smithy(canvas, new Color(130, 132, 140)));
        Add(device, "building.steel_mill", SpriteSize, SteelMill);

        // Průmysl a energie.
        Add(device, "building.factory", SpriteSize, Factory);
        Add(device, "building.machine_shop", SpriteSize, MachineShop);
        Add(device, "building.coal_power_plant", SpriteSize, PowerPlant);
        Add(device, "building.solar_array", SpriteSize, SolarArray);
        Add(device, "building.fusion_plant", SpriteSize, FusionPlant);
        Add(device, "building.electronics_fab", SpriteSize, canvas => TechPlant(canvas, new Color(86, 176, 150)));
        Add(device, "building.computer_plant", SpriteSize, canvas => TechPlant(canvas, new Color(96, 148, 200)));
        Add(device, "building.nano_forge", SpriteSize, canvas => TechPlant(canvas, new Color(168, 122, 208)));
        Add(device, "building.robotics_lab", SpriteSize, RoboticsLab);
        Add(device, "building.hydroponics", SpriteSize, Hydroponics);

        // Bydlení pozdních ér.
        Add(device, "building.apartment", SpriteSize, canvas => Tower(canvas, new Color(170, 156, 142), floors: 4));
        Add(device, "building.high_rise", SpriteSize, canvas => Tower(canvas, new Color(140, 152, 168), floors: 6));
        Add(device, "building.arcology", SpriteSize, Arcology);

        // Sloučené bloky (2×2 z bloku čtyř stejných budov) — vyšší a širší silueta,
        // aby se na mapě daly rozeznat od jednotlivých domů na první pohled.
        Add(device, "agent.boat", SpriteSize, Boat);
        Add(device, "agent.caravan", SpriteSize, Caravan);
        Add(device, "building.manor", SpriteSize, canvas => BigHouse(canvas, new Color(199, 122, 68), 2));
        Add(device, "building.townhouses", SpriteSize, canvas => BigHouse(canvas, new Color(212, 137, 74), 3));
        Add(device, "building.terrace_row", SpriteSize, canvas => BigHouse(canvas, new Color(179, 106, 70), 4));
        Add(device, "building.housing_block", SpriteSize, canvas => BigHouse(canvas, new Color(155, 90, 64), 5));
        Add(device, "building.timber_yard", SpriteSize, TimberYard);
        Add(device, "building.great_quarry", SpriteSize, GreatQuarry);

        // Zeleň a odpočinek.
        Add(device, "building.park", SpriteSize, canvas => Greenery(canvas, trees: 2));
        Add(device, "building.city_park", SpriteSize, canvas => Greenery(canvas, trees: 4));
        Add(device, "building.botanical_garden", SpriteSize, BotanicalGarden);
        Add(device, "building.fountain_square", SpriteSize, FountainSquare);

        // Monumenty — každý má být poznat podle obrysu, ne podle barvy.
        Add(device, "building.standing_stones", SpriteSize, StandingStones);
        Add(device, "building.obelisk", SpriteSize, Obelisk);
        Add(device, "building.great_statue", SpriteSize, GreatStatue);
        Add(device, "building.triumphal_arch", SpriteSize, TriumphalArch);
        Add(device, "building.clock_tower", SpriteSize, ClockTower);
        Add(device, "building.grand_library", SpriteSize, GrandLibrary);
        Add(device, "building.cathedral", SpriteSize, Cathedral);
        Add(device, "building.observatory", SpriteSize, Observatory);

        // Voda a doprava.
        Add(device, "building.harbor", SpriteSize, Harbor);
        Add(device, "building.fishery", SpriteSize, Fishery);
        Add(device, "building.deep_sea_port", SpriteSize, DeepSeaPort);
        Add(device, "building.airfield", SpriteSize, canvas => Airfield(canvas, big: false));
        Add(device, "building.airport", SpriteSize, canvas => Airfield(canvas, big: true));
        Add(device, "building.spaceport", SpriteSize, Spaceport);

        // Megastavby.
        Add(device, "building.megacity_spire", SpriteSize, MegacitySpire);
        Add(device, "building.grand_exchange", SpriteSize, GrandExchange);
        Add(device, "building.orbital_ring", SpriteSize, OrbitalRing);
        Add(device, "building.world_forge", SpriteSize, WorldForge);

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


    /// <summary>
    /// Sloučený obytný blok: řada štítů místo jednoho domu. Počet štítů roste
    /// s úrovní, takže panské sídlo a obytný blok jdou od sebe rozeznat i bez barvy.
    /// </summary>
    private static void BigHouse(PixelCanvas c, Color wall, int gables)
    {
        c.FillRect(2, 14, 28, 16, wall);
        c.FillRect(2, 27, 28, 3, new Color(70, 52, 40)); // podezdívka

        int width = 28 / gables;
        for (int i = 0; i < gables; i++)
        {
            float left = 2 + i * width;
            c.FillTriangle(left, 14f, left + width, 14f, left + width / 2f, 14f - width * 0.55f, new Color(150, 60, 48));
            c.FillRect((int)left + width / 2 - 2, 17, 4, 4, new Color(150, 205, 225)); // okno
            c.FillRect((int)left + width / 2 - 1, 23, 3, 7, new Color(96, 64, 38));    // dveře
        }
    }

    /// <summary>Rybářská loďka: trup, plachta, drobná postava. Kreslí se malá — je to detail, ne budova.</summary>
    private static void Boat(PixelCanvas c)
    {
        c.FillTriangle(8f, 22f, 24f, 22f, 21f, 27f, new Color(122, 88, 56)); // trup
        c.FillRect(8, 20, 16, 3, new Color(146, 108, 68));                   // paluba
        c.FillRect(15, 8, 2, 12, new Color(96, 74, 50));                     // stěžeň
        c.FillTriangle(17f, 9f, 17f, 19f, 25f, 19f, new Color(232, 226, 210)); // plachta
        c.FillCircle(12f, 18f, 1.8f, new Color(216, 176, 136));              // rybář
    }

    /// <summary>Obchodní karavana: krytý vůz s plachtou a nákladem — na první pohled jiná než běžný vozík.</summary>
    private static void Caravan(PixelCanvas c)
    {
        c.FillRect(5, 18, 22, 8, new Color(126, 92, 58));                 // korba
        c.FillTriangle(5f, 18f, 27f, 18f, 16f, 9f, new Color(226, 216, 192)); // plachta
        c.FillRect(7, 12, 18, 2, new Color(198, 186, 160));               // obruč plachty
        c.FillCircle(9f, 27f, 3.2f, new Color(78, 62, 46));               // kola
        c.FillCircle(22f, 27f, 3.2f, new Color(78, 62, 46));
        c.FillRect(24, 20, 5, 4, new Color(168, 132, 80));                // bedny vzadu
    }

    private static void TimberYard(PixelCanvas c)
    {
        c.FillRect(3, 15, 26, 15, new Color(122, 94, 58));
        c.FillTriangle(1f, 15f, 31f, 15f, 16f, 6f, new Color(92, 68, 42));
        // Tři štosy klád — velkoprovoz, ne tábor.
        for (int i = 0; i < 3; i++)
        {
            c.FillRect(4 + i * 8, 22, 7, 3, new Color(154, 114, 68));
            c.FillRect(4 + i * 8, 25, 7, 3, new Color(130, 94, 54));
        }
    }

    private static void GreatQuarry(PixelCanvas c)
    {
        c.FillRect(2, 18, 28, 12, new Color(104, 100, 94)); // vylámaná jáma
        c.FillTriangle(4f, 18f, 14f, 18f, 9f, 7f, new Color(126, 122, 114));
        c.FillTriangle(16f, 18f, 28f, 18f, 22f, 5f, new Color(112, 108, 100));
        c.FillRect(6, 24, 6, 4, new Color(78, 76, 72)); // odvalové vozíky
        c.FillRect(19, 25, 7, 3, new Color(78, 76, 72));
    }

    /// <summary>Zeleň: trávník s korunami stromů. Počet stromů odlišuje park od městského parku.</summary>
    private static void Greenery(PixelCanvas c, int trees)
    {
        c.FillRect(3, 10, 26, 20, new Color(96, 148, 78));
        c.FillRect(3, 19, 26, 3, new Color(196, 178, 130)); // pěšina napříč
        for (int i = 0; i < trees; i++)
        {
            float cx = 7 + i * (18f / Math.Max(1, trees - 1) + 2f);
            c.FillRect((int)cx - 1, 22, 2, 6, new Color(96, 70, 44));
            c.FillCircle(cx, 16f, 4.4f, new Color(58, 116, 56));
        }
    }

    private static void BotanicalGarden(PixelCanvas c)
    {
        c.FillRect(3, 12, 26, 18, new Color(84, 136, 72));
        // Skleník: prosklená klenba.
        c.FillRect(8, 14, 16, 12, new Color(178, 214, 220));
        c.FillTriangle(7f, 14f, 25f, 14f, 16f, 7f, new Color(206, 232, 236));
        for (int i = 0; i < 3; i++)
        {
            c.FillRect(10 + i * 5, 14, 1, 12, new Color(120, 150, 140)); // příčle
        }

        c.FillCircle(6f, 25f, 3f, new Color(58, 116, 56));
        c.FillCircle(26f, 25f, 3f, new Color(58, 116, 56));
    }

    private static void FountainSquare(PixelCanvas c)
    {
        c.FillRect(2, 10, 28, 20, new Color(186, 178, 160)); // dlážděné náměstí
        c.FillCircle(16f, 20f, 8f, new Color(150, 146, 134));
        c.FillCircle(16f, 20f, 6.4f, new Color(96, 166, 200)); // voda
        c.FillRect(15, 12, 2, 8, new Color(210, 206, 196));    // sloupek
        c.FillCircle(16f, 12f, 2.6f, new Color(178, 224, 238)); // vodní chochol
    }

    private static void StandingStones(PixelCanvas c)
    {
        c.FillRect(2, 22, 28, 8, new Color(112, 140, 92)); // travnatý pahorek
        c.FillRect(5, 10, 5, 14, new Color(146, 142, 134));
        c.FillRect(14, 7, 5, 17, new Color(158, 154, 146));
        c.FillRect(23, 11, 5, 13, new Color(140, 136, 128));
        c.FillRect(5, 6, 14, 3, new Color(166, 162, 154)); // překlad
    }

    private static void Obelisk(PixelCanvas c)
    {
        c.FillRect(11, 26, 10, 4, new Color(150, 142, 120)); // podstavec
        c.FillRect(13, 6, 6, 20, new Color(216, 196, 138));
        c.FillTriangle(13f, 6f, 19f, 6f, 16f, 1f, new Color(238, 220, 160)); // pyramidion
        c.FillRect(15, 12, 2, 10, new Color(186, 166, 112)); // rytý sloupec
    }

    private static void GreatStatue(PixelCanvas c)
    {
        c.FillRect(9, 25, 14, 5, new Color(146, 138, 116)); // sokl
        c.FillRect(14, 12, 4, 13, new Color(198, 172, 106)); // trup
        c.FillCircle(16f, 9f, 3.6f, new Color(214, 190, 124)); // hlava
        c.FillRect(8, 13, 6, 2, new Color(198, 172, 106));  // rozpažené ruce
        c.FillRect(18, 13, 6, 2, new Color(198, 172, 106));
    }

    private static void TriumphalArch(PixelCanvas c)
    {
        c.FillRect(3, 8, 26, 22, new Color(203, 182, 138));
        c.FillRect(12, 16, 8, 14, new Color(88, 76, 60));   // průchod
        c.FillCircle(16f, 16f, 4f, new Color(88, 76, 60));  // oblouk průchodu
        c.FillRect(3, 5, 26, 4, new Color(222, 202, 158));  // atika
        c.FillRect(7, 20, 3, 10, new Color(186, 166, 124)); // polosloupy
        c.FillRect(22, 20, 3, 10, new Color(186, 166, 124));
    }

    private static void ClockTower(PixelCanvas c)
    {
        c.FillRect(10, 8, 12, 22, new Color(170, 140, 100));
        c.FillTriangle(8f, 8f, 24f, 8f, 16f, 1f, new Color(120, 78, 62)); // jehlan
        c.FillCircle(16f, 14f, 4.2f, new Color(238, 232, 214));           // ciferník
        c.FillRect(15, 11, 2, 4, new Color(60, 54, 46));                  // ručičky
        c.FillRect(16, 13, 4, 2, new Color(60, 54, 46));
    }

    private static void GrandLibrary(PixelCanvas c)
    {
        c.FillRect(3, 12, 26, 18, new Color(178, 152, 108));
        c.FillTriangle(1f, 12f, 31f, 12f, 16f, 4f, new Color(154, 128, 88)); // tympanon
        for (int i = 0; i < 5; i++)
        {
            c.FillRect(5 + i * 5, 16, 3, 14, new Color(206, 186, 146)); // sloupořadí
        }

        c.FillRect(3, 28, 26, 2, new Color(140, 116, 82)); // schodiště
    }

    private static void Cathedral(PixelCanvas c)
    {
        c.FillRect(6, 12, 20, 18, new Color(196, 182, 156)); // hlavní loď
        c.FillTriangle(4f, 12f, 28f, 12f, 16f, 4f, new Color(150, 134, 112));
        c.FillRect(2, 6, 5, 24, new Color(184, 170, 144));   // věž vlevo
        c.FillTriangle(1f, 6f, 8f, 6f, 4.5f, 0f, new Color(120, 104, 88));
        c.FillRect(25, 6, 5, 24, new Color(184, 170, 144));  // věž vpravo
        c.FillTriangle(24f, 6f, 31f, 6f, 27.5f, 0f, new Color(120, 104, 88));
        c.FillCircle(16f, 18f, 3.6f, new Color(120, 168, 200)); // rozeta
        c.FillRect(14, 24, 5, 6, new Color(96, 78, 62));        // portál
    }

    private static void Observatory(PixelCanvas c)
    {
        c.FillRect(7, 18, 18, 12, new Color(158, 162, 176));
        c.FillCircle(16f, 17f, 8f, new Color(186, 190, 204)); // kopule
        c.FillRect(14, 6, 4, 12, new Color(120, 126, 142));   // štěrbina
        c.FillRect(15, 4, 8, 3, new Color(96, 102, 118));     // tubus dalekohledu
        c.FillRect(7, 27, 18, 3, new Color(126, 130, 144));
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

    // ----- éra kamene a dřeva -----

    private static void Granary(PixelCanvas c)
    {
        c.FillRect(7, 14, 18, 16, new Color(186, 158, 104)); // hliněné stěny
        c.FillTriangle(5f, 14f, 27f, 14f, 16f, 4f, new Color(146, 118, 72)); // došková střecha
        c.FillRect(13, 20, 6, 10, new Color(120, 88, 52)); // vrata
        c.FillRect(9, 17, 3, 3, new Color(120, 96, 60));    // větrací otvory
        c.FillRect(20, 17, 3, 3, new Color(120, 96, 60));
    }

    private static void Depot(PixelCanvas c)
    {
        c.FillRect(4, 15, 24, 15, new Color(150, 144, 132));
        c.FillRect(4, 12, 24, 4, new Color(112, 108, 100)); // plochá střecha
        for (int i = 0; i < 3; i++)
        {
            c.FillRect(7 + i * 7, 20, 5, 10, new Color(96, 104, 118)); // vrata v řadě
        }
    }

    private static void Workshop(PixelCanvas c)
    {
        c.FillRect(5, 16, 22, 14, new Color(160, 122, 82));
        c.FillTriangle(3f, 16f, 29f, 16f, 16f, 7f, new Color(122, 90, 58));
        c.FillRect(12, 21, 8, 9, new Color(92, 66, 40));  // dílenská vrata
        c.FillRect(21, 19, 4, 4, new Color(150, 205, 225));
        c.FillRect(7, 10, 3, 7, new Color(110, 100, 92)); // komín
    }

    private static void CharcoalKiln(PixelCanvas c)
    {
        c.FillCircle(16f, 22f, 9f, new Color(96, 82, 66)); // kupa hlíny
        c.FillCircle(16f, 22f, 6f, new Color(64, 54, 46));
        c.FillRect(14, 8, 4, 8, new Color(110, 100, 92));  // komínek
        c.FillCircle(16f, 8f, 3f, new Color(90, 90, 96) * 0.7f); // kouř
        c.FillCircle(18f, 5f, 2.2f, new Color(110, 110, 116) * 0.5f);
    }

    private static void FishingHut(PixelCanvas c)
    {
        c.FillRect(8, 16, 16, 12, new Color(150, 120, 88));
        c.FillTriangle(6f, 16f, 26f, 16f, 16f, 8f, new Color(112, 88, 62));
        c.FillRect(5, 27, 22, 2, new Color(120, 96, 66));   // molo
        c.FillRect(7, 29, 2, 3, new Color(100, 78, 52));    // piloty
        c.FillRect(23, 29, 2, 3, new Color(100, 78, 52));
        c.FillCircle(24f, 20f, 3f, new Color(210, 210, 200) * 0.8f); // síť
    }

    private static void HuntersLodge(PixelCanvas c)
    {
        c.FillRect(7, 17, 18, 13, new Color(122, 94, 62));
        c.FillTriangle(4f, 17f, 28f, 17f, 16f, 6f, new Color(88, 68, 46));
        c.FillRect(14, 23, 5, 7, new Color(70, 52, 34));
        // Sušící stojan s kůží.
        c.FillRect(24, 14, 1, 12, new Color(96, 74, 50));
        c.FillRect(21, 15, 6, 5, new Color(150, 112, 78));
    }

    /// <summary>Patrový městský dům — základ pro cihlový dům i činžák.</summary>
    private static void TownHouse(PixelCanvas c, Color wall, int floors)
    {
        int height = 6 + floors * 6;
        int top = 30 - height;
        c.FillRect(7, top, 18, height, wall);
        c.FillTriangle(5f, top, 27f, top, 16f, top - 6f, new Color(126, 62, 54));
        c.FillRect(14, 24, 5, 6, new Color(88, 60, 38)); // dveře
        for (int f = 0; f < floors; f++)
        {
            int y = top + 3 + f * 6;
            c.FillRect(9, y, 4, 4, new Color(150, 205, 225));
            c.FillRect(19, y, 4, 4, new Color(150, 205, 225));
        }
    }

    // ----- doly a hutě -----

    private static void MineShaft(PixelCanvas c, Color ore)
    {
        c.FillRect(6, 20, 20, 10, new Color(104, 96, 88)); // podezdívka
        // Těžní věž.
        c.FillTriangle(10f, 20f, 22f, 20f, 16f, 6f, new Color(120, 96, 66));
        c.FillRect(15, 6, 3, 6, new Color(96, 76, 52));
        c.FillCircle(16.5f, 7f, 2.5f, new Color(70, 62, 54)); // kladka
        // Vytěžená ruda na hromádce.
        c.FillCircle(9f, 27f, 3f, ore);
        c.FillCircle(23f, 27f, 2.4f, ore);
    }

    private static void Furnace(PixelCanvas c, Color glow)
    {
        c.FillRect(8, 14, 16, 16, new Color(104, 92, 84));
        c.FillTriangle(8f, 14f, 24f, 14f, 16f, 8f, new Color(84, 74, 68));
        c.FillRect(12, 20, 8, 8, new Color(48, 40, 36)); // ústí
        c.FillCircle(16f, 25f, 3.2f, glow);              // žhavá tavenina
        c.FillRect(6, 10, 3, 12, new Color(96, 88, 80));  // komín
        c.FillRect(23, 10, 3, 12, new Color(96, 88, 80));
    }

    private static void Smithy(PixelCanvas c, Color metal)
    {
        c.FillRect(5, 16, 22, 14, new Color(126, 104, 88));
        c.FillTriangle(3f, 16f, 29f, 16f, 16f, 8f, new Color(96, 78, 64));
        c.FillRect(11, 21, 10, 9, new Color(52, 44, 40)); // otevřená kovárna
        c.FillCircle(16f, 26f, 3f, new Color(232, 140, 60)); // výheň
        c.FillRect(22, 18, 5, 4, metal);                  // hotové výrobky
        c.FillRect(6, 9, 3, 8, new Color(104, 96, 88));
    }

    private static void SteelMill(PixelCanvas c)
    {
        c.FillRect(3, 15, 26, 15, new Color(112, 116, 124));
        c.FillRect(3, 12, 26, 4, new Color(88, 92, 100));
        for (int i = 0; i < 3; i++)
        {
            c.FillRect(6 + i * 9, 4, 4, 9, new Color(96, 100, 108)); // komíny
            c.FillCircle(8f + i * 9, 3f, 2.6f, new Color(120, 120, 126) * 0.55f);
        }

        c.FillRect(8, 22, 16, 5, new Color(226, 132, 58)); // rozžhavená ocel
    }

    // ----- průmysl a energie -----

    private static void Factory(PixelCanvas c)
    {
        c.FillRect(4, 16, 24, 14, new Color(132, 128, 136));
        // Pilovitá střecha — typický tvar továrny.
        for (int i = 0; i < 4; i++)
        {
            c.FillTriangle(4f + i * 6, 16f, 10f + i * 6, 16f, 4f + i * 6, 10f, new Color(104, 100, 110));
            c.FillRect(5 + i * 6, 12, 3, 4, new Color(150, 205, 225));
        }

        c.FillRect(12, 22, 8, 8, new Color(88, 92, 100));
        c.FillRect(24, 6, 4, 11, new Color(110, 106, 114));
    }

    private static void MachineShop(PixelCanvas c)
    {
        c.FillRect(5, 15, 22, 15, new Color(140, 136, 130));
        c.FillRect(5, 12, 22, 4, new Color(108, 104, 100));
        c.FillCircle(16f, 21f, 6f, new Color(96, 100, 108)); // velké ozubené kolo
        c.FillCircle(16f, 21f, 3f, new Color(150, 154, 162));
        for (int i = 0; i < 6; i++)
        {
            float angle = i * MathF.PI / 3f;
            c.FillRect((int)(16 + MathF.Cos(angle) * 7) - 1, (int)(21 + MathF.Sin(angle) * 7) - 1, 3, 3,
                new Color(96, 100, 108));
        }
    }

    private static void PowerPlant(PixelCanvas c)
    {
        c.FillRect(4, 18, 24, 12, new Color(118, 114, 112));
        // Dvě chladicí věže (užší nahoře).
        c.FillTriangle(6f, 18f, 14f, 18f, 10f, 6f, new Color(148, 144, 142));
        c.FillTriangle(18f, 18f, 26f, 18f, 22f, 6f, new Color(148, 144, 142));
        c.FillCircle(10f, 5f, 3f, new Color(200, 200, 205) * 0.5f); // pára
        c.FillCircle(22f, 4f, 2.6f, new Color(200, 200, 205) * 0.4f);
        c.FillRect(12, 24, 8, 6, new Color(70, 68, 66));
    }

    private static void SolarArray(PixelCanvas c)
    {
        c.FillRect(4, 24, 24, 4, new Color(120, 118, 116)); // rám
        for (int i = 0; i < 3; i++)
        {
            // Nakloněné panely.
            c.FillTriangle(5f + i * 8, 24f, 12f + i * 8, 24f, 12f + i * 8, 13f, new Color(58, 92, 150));
            c.FillTriangle(5f + i * 8, 24f, 12f + i * 8, 13f, 6f + i * 8, 15f, new Color(80, 130, 200));
        }
    }

    private static void FusionPlant(PixelCanvas c)
    {
        c.FillRect(5, 17, 22, 13, new Color(126, 132, 142));
        c.FillCircle(16f, 15f, 8f, new Color(96, 104, 118)); // torus
        c.FillCircle(16f, 15f, 5f, new Color(120, 220, 240));
        c.FillCircle(16f, 15f, 2.4f, new Color(240, 252, 255)); // plazma
        c.FillRect(6, 24, 5, 6, new Color(96, 100, 108));
        c.FillRect(21, 24, 5, 6, new Color(96, 100, 108));
    }

    private static void TechPlant(PixelCanvas c, Color accent)
    {
        c.FillRect(4, 14, 24, 16, new Color(146, 150, 158));
        c.FillRect(4, 11, 24, 4, new Color(112, 118, 128));
        // Čisté prostory za prosklením.
        for (int i = 0; i < 4; i++)
        {
            c.FillRect(6 + i * 6, 17, 4, 6, accent);
        }

        c.FillRect(13, 25, 6, 5, new Color(96, 100, 110));
        c.FillRect(25, 6, 2, 6, accent); // anténa
    }

    private static void RoboticsLab(PixelCanvas c)
    {
        c.FillRect(5, 15, 22, 15, new Color(150, 154, 160));
        c.FillRect(5, 12, 22, 4, new Color(112, 116, 124));
        // Robotické rameno.
        c.FillRect(11, 18, 3, 9, new Color(96, 100, 108));
        c.FillRect(11, 18, 10, 3, new Color(96, 100, 108));
        c.FillCircle(21f, 20f, 2.4f, new Color(232, 140, 60));
        c.FillRect(18, 25, 8, 5, new Color(120, 200, 190));
    }

    private static void Hydroponics(PixelCanvas c)
    {
        c.FillRect(4, 16, 24, 14, new Color(180, 200, 210) * 0.85f); // skleník
        c.FillTriangle(4f, 16f, 28f, 16f, 16f, 8f, new Color(200, 220, 230) * 0.8f);
        for (int i = 0; i < 3; i++)
        {
            c.FillRect(7, 20 + i * 4, 18, 2, new Color(80, 170, 90)); // patra sazenic
        }
    }

    // ----- bydlení pozdních ér -----

    private static void Tower(PixelCanvas c, Color wall, int floors)
    {
        int height = 4 + floors * 4;
        int top = 30 - height;
        c.FillRect(8, top, 16, height, wall);
        c.FillRect(8, top - 2, 16, 3, wall * 0.8f); // atika
        for (int f = 0; f < floors; f++)
        {
            int y = top + 2 + f * 4;
            c.FillRect(10, y, 4, 2, new Color(150, 205, 225));
            c.FillRect(18, y, 4, 2, new Color(150, 205, 225));
        }

        c.FillRect(14, 26, 4, 4, new Color(88, 84, 80)); // vchod
    }

    private static void Arcology(PixelCanvas c)
    {
        // Stupňovitá pyramida se zelení — soběstačné město v jedné budově.
        c.FillTriangle(2f, 30f, 30f, 30f, 16f, 4f, new Color(150, 158, 168));
        for (int i = 0; i < 4; i++)
        {
            int y = 26 - i * 5;
            int half = 11 - i * 2;
            c.FillRect(16 - half, y, half * 2, 2, new Color(90, 170, 110)); // terasy se zelení
        }

        c.FillCircle(16f, 6f, 2.6f, new Color(180, 230, 255));
    }

    // ----- voda a doprava -----

    private static void Harbor(PixelCanvas c)
    {
        c.FillRect(3, 22, 26, 4, new Color(140, 128, 110)); // molo
        c.FillRect(6, 26, 2, 5, new Color(112, 96, 74));
        c.FillRect(24, 26, 2, 5, new Color(112, 96, 74));
        c.FillRect(8, 12, 12, 10, new Color(158, 132, 96));  // skladiště
        c.FillTriangle(6f, 12f, 22f, 12f, 14f, 6f, new Color(120, 96, 68));
        // Plachetnice u mola.
        c.FillRect(22, 18, 7, 3, new Color(120, 88, 60));
        c.FillRect(25, 10, 1, 8, new Color(96, 76, 52));
        c.FillTriangle(26f, 10f, 26f, 17f, 30f, 16f, new Color(235, 235, 230));
    }

    private static void Fishery(PixelCanvas c)
    {
        c.FillRect(5, 16, 22, 12, new Color(150, 152, 156));
        c.FillRect(5, 13, 22, 4, new Color(110, 114, 120));
        c.FillRect(4, 28, 24, 3, new Color(130, 118, 100)); // rampa
        // Ryby v přepravkách.
        for (int i = 0; i < 3; i++)
        {
            c.FillRect(8 + i * 6, 20, 5, 4, new Color(120, 170, 190));
            c.FillCircle(10.5f + i * 6, 22f, 1.2f, new Color(220, 225, 230));
        }
    }

    private static void DeepSeaPort(PixelCanvas c)
    {
        c.FillRect(2, 24, 28, 5, new Color(126, 126, 130)); // betonové nábřeží
        // Portálový jeřáb.
        c.FillRect(7, 8, 2, 16, new Color(220, 140, 50));
        c.FillRect(21, 8, 2, 16, new Color(220, 140, 50));
        c.FillRect(6, 6, 18, 3, new Color(220, 140, 50));
        c.FillRect(14, 9, 2, 7, new Color(150, 150, 154)); // lano
        c.FillRect(12, 16, 7, 5, new Color(80, 140, 180));  // kontejner
        c.FillRect(2, 19, 6, 4, new Color(180, 90, 70));
    }

    private static void Airfield(PixelCanvas c, bool big)
    {
        c.FillRect(2, 20, 28, 8, new Color(96, 98, 104)); // dráha
        for (int i = 0; i < 5; i++)
        {
            c.FillRect(4 + i * 6, 23, 3, 2, new Color(220, 220, 210)); // středová čára
        }

        var body = big ? new Color(230, 232, 236) : new Color(200, 200, 190);
        c.FillRect(11, 12, 12, 4, body);                   // trup
        c.FillTriangle(23f, 12f, 23f, 16f, 29f, 14f, body); // příď
        c.FillTriangle(13f, 12f, 19f, 12f, 16f, 6f, body);  // křídlo
        c.FillTriangle(13f, 16f, 19f, 16f, 16f, 21f, body);
        if (big)
        {
            c.FillRect(3, 8, 6, 10, new Color(150, 154, 162)); // terminál
            c.FillRect(4, 4, 2, 5, new Color(120, 124, 132));  // věž
        }
    }

    private static void Spaceport(PixelCanvas c)
    {
        c.FillRect(4, 25, 24, 5, new Color(110, 112, 118)); // rampa
        // Raketa.
        c.FillRect(13, 8, 6, 17, new Color(226, 228, 232));
        c.FillTriangle(13f, 8f, 19f, 8f, 16f, 1f, new Color(200, 80, 70)); // špička
        c.FillTriangle(13f, 20f, 13f, 25f, 9f, 25f, new Color(200, 80, 70)); // stabilizátory
        c.FillTriangle(19f, 20f, 19f, 25f, 23f, 25f, new Color(200, 80, 70));
        c.FillCircle(16f, 14f, 2f, new Color(120, 190, 230)); // okénko
        c.FillRect(6, 14, 3, 12, new Color(140, 142, 148));   // obslužná věž
    }

    // ----- megastavby -----

    private static void MegacitySpire(PixelCanvas c)
    {
        c.FillTriangle(6f, 30f, 26f, 30f, 16f, 2f, new Color(120, 132, 150));
        c.FillTriangle(10f, 30f, 22f, 30f, 16f, 6f, new Color(150, 164, 182));
        for (int i = 0; i < 6; i++)
        {
            c.FillRect(12, 26 - i * 4, 8, 1, new Color(190, 225, 245)); // pásy oken
        }

        c.FillCircle(16f, 3f, 1.8f, new Color(255, 220, 120)); // maják na špici
    }

    private static void GrandExchange(PixelCanvas c)
    {
        c.FillRect(4, 14, 24, 16, new Color(196, 186, 164)); // mramor
        c.FillTriangle(2f, 14f, 30f, 14f, 16f, 5f, new Color(168, 156, 134)); // tympanon
        for (int i = 0; i < 5; i++)
        {
            c.FillRect(6 + i * 5, 17, 3, 13, new Color(226, 218, 200)); // sloupy
        }

        c.FillRect(4, 29, 24, 2, new Color(150, 140, 122)); // schodiště
    }

    private static void OrbitalRing(PixelCanvas c)
    {
        c.FillCircle(16f, 15f, 12f, new Color(110, 130, 160) * 0.55f);
        c.FillCircle(16f, 15f, 9f, Color.Transparent);      // prstenec
        c.FillRect(15, 15, 3, 15, new Color(150, 154, 162)); // kotvící stožár
        c.FillCircle(16f, 15f, 3.2f, new Color(180, 220, 245));
        c.FillCircle(6f, 10f, 1.6f, new Color(255, 235, 160)); // moduly na prstenci
        c.FillCircle(26f, 20f, 1.6f, new Color(255, 235, 160));
    }

    private static void WorldForge(PixelCanvas c)
    {
        c.FillRect(3, 16, 26, 14, new Color(96, 92, 96));
        c.FillTriangle(3f, 16f, 29f, 16f, 16f, 8f, new Color(72, 68, 72));
        c.FillCircle(16f, 22f, 6f, new Color(232, 120, 50)); // roztavené jádro
        c.FillCircle(16f, 22f, 3f, new Color(255, 220, 140));
        c.FillRect(5, 6, 4, 11, new Color(88, 84, 88));
        c.FillRect(23, 6, 4, 11, new Color(88, 84, 88));
        c.FillCircle(7f, 5f, 2.4f, new Color(150, 140, 140) * 0.5f);
        c.FillCircle(25f, 4f, 2f, new Color(150, 140, 140) * 0.45f);
    }


    // ----- ikony pozdějších surovin -----

    /// <summary>Ruda: hrubé kusy kamene s barevnou žílou.</summary>
    private static void OreIcon(PixelCanvas c, Color ore)
    {
        c.FillCircle(9f, 15f, 6f, new Color(112, 106, 100));
        c.FillCircle(16f, 11f, 5f, new Color(128, 122, 116));
        c.FillCircle(8f, 14f, 2.2f, ore);
        c.FillCircle(17f, 10f, 2f, ore);
    }

    /// <summary>Ingot: kovový hranol s odleskem.</summary>
    private static void IngotIcon(PixelCanvas c, Color metal)
    {
        c.FillTriangle(3f, 17f, 21f, 17f, 6f, 9f, metal);
        c.FillRect(6, 9, 12, 8, metal);
        c.FillRect(6, 9, 12, 2, metal * 1.25f); // odlesk na horní hraně
        c.FillRect(3, 17, 18, 2, metal * 0.7f);
    }

    private static void GearIcon(PixelCanvas c)
    {
        var metal = new Color(150, 156, 166);
        c.FillCircle(12f, 12f, 7f, metal);
        c.FillCircle(12f, 12f, 3f, new Color(70, 74, 82));
        for (int i = 0; i < 8; i++)
        {
            float angle = i * MathF.PI / 4f;
            c.FillRect((int)(12 + MathF.Cos(angle) * 8) - 1, (int)(12 + MathF.Sin(angle) * 8) - 1, 3, 3, metal);
        }
    }

    private static void ChipIcon(PixelCanvas c)
    {
        c.FillRect(6, 6, 12, 12, new Color(46, 108, 92));
        c.FillRect(9, 9, 6, 6, new Color(120, 200, 170));
        for (int i = 0; i < 3; i++)
        {
            c.FillRect(3, 8 + i * 4, 3, 2, new Color(210, 190, 90)); // nožičky
            c.FillRect(18, 8 + i * 4, 3, 2, new Color(210, 190, 90));
        }
    }

    private static void ComputerIcon(PixelCanvas c)
    {
        c.FillRect(3, 5, 18, 12, new Color(96, 104, 118)); // monitor
        c.FillRect(5, 7, 14, 8, new Color(110, 180, 220));
        c.FillRect(9, 17, 6, 2, new Color(80, 86, 96));    // stojan
        c.FillRect(5, 19, 14, 2, new Color(96, 104, 118));
    }

    private static void RobotIcon(PixelCanvas c)
    {
        c.FillRect(7, 8, 10, 9, new Color(160, 166, 176)); // trup
        c.FillRect(8, 3, 8, 5, new Color(180, 186, 196));  // hlava
        c.FillCircle(10f, 5.5f, 1.2f, new Color(230, 120, 90)); // oči
        c.FillCircle(14f, 5.5f, 1.2f, new Color(230, 120, 90));
        c.FillRect(4, 9, 3, 6, new Color(140, 146, 156));  // paže
        c.FillRect(17, 9, 3, 6, new Color(140, 146, 156));
        c.FillRect(8, 17, 3, 4, new Color(120, 126, 136)); // nohy
        c.FillRect(13, 17, 3, 4, new Color(120, 126, 136));
    }

}
