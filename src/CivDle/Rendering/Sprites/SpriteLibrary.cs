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

    /// <summary>Velikost znaku hry v hlavním menu.</summary>
    public const int LogoSize = 96;

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
        Add(device, "icon.science", IconSize, ScienceIcon);
        Add(device, "icon.faith", IconSize, FaithIcon);
        Add(device, "icon.machine_parts", IconSize, GearIcon);
        Add(device, "icon.electronics", IconSize, ChipIcon);
        Add(device, "icon.computer", IconSize, ComputerIcon);
        Add(device, "icon.robot", IconSize, RobotIcon);
        Add(device, "icon.uranium", IconSize, UraniumIcon);

        // Ikony do HUD. Lišta plná slov je v akční hře nečitelná: hráč hledá
        // tvar, ne text. Popis nese bublina, ikona nese poznání.
        // Znak hry do hlavního menu. Kreslí se stejně jako ostatní ikony —
        // procedurálně, ne ze souboru — jen ve větším rozlišení, protože
        // v menu sedí přes sto pixelů.
        Add(device, "ui.logo", LogoSize, Logo);

        Add(device, "ui.build", IconSize, UiBuild);
        Add(device, "ui.road", IconSize, UiRoad);
        Add(device, "ui.demolish", IconSize, UiDemolish);
        Add(device, "ui.zone", IconSize, UiZone);
        Add(device, "ui.plant", IconSize, UiPlant);
        Add(device, "ui.terraform", IconSize, UiTerraform);
        Add(device, "ui.faith", IconSize, FaithIcon);
        Add(device, "ui.festival", IconSize, UiFestival);
        Add(device, "ui.home", IconSize, UiHome);
        Add(device, "ui.settlements", IconSize, UiSettlements);
        Add(device, "ui.quests", IconSize, UiQuests);
        Add(device, "ui.contracts", IconSize, UiContracts);
        Add(device, "ui.tech", IconSize, ScienceIcon);
        Add(device, "ui.governor", IconSize, UiGovernor);
        Add(device, "ui.ascend", IconSize, UiAscend);
        Add(device, "ui.stats", IconSize, UiStats);
        Add(device, "ui.trophy", IconSize, UiTrophy);
        Add(device, "ui.chronicle", IconSize, UiChronicle);
        Add(device, "ui.vote", IconSize, UiVote);
        Add(device, "ui.play", IconSize, UiPlay);
        Add(device, "ui.pause", IconSize, UiPause);
        Add(device, "ui.fast", IconSize, UiFast);

        // Akce nad budovou. Ikonka nese význam rychleji než slovo — v panelu jsou
        // „šipka nahoru" a „čtyři čtverce v jeden" poznat na první pohled.
        Add(device, "icon.upgrade", IconSize, UpgradeIcon);
        Add(device, "icon.merge", IconSize, MergeIcon);

        // Těžitelné objekty na terénu.
        Add(device, "node.tree", SpriteSize, Tree);
        Add(device, "node.rock", SpriteSize, Rock);
        Add(device, "node.stump", SpriteSize, Stump);
        Add(device, "node.rubble", SpriteSize, Rubble);

        // Výrazná místa na mapě. Barevný čtvereček neřekl nic — vrak lodi má
        // vypadat jako vrak lodi, ruiny jako ruiny. Data říkají, který sprite
        // který landmark použije (viz landmarks.json).
        Add(device, "landmark.shipwreck", SpriteSize, Shipwreck);
        Add(device, "landmark.ruins", SpriteSize, Ruins);
        Add(device, "landmark.stone_circle", SpriteSize, StoneCircle);
        Add(device, "landmark.bones", SpriteSize, Bones);
        Add(device, "landmark.crystal", SpriteSize, Crystal);
        Add(device, "landmark.geyser", SpriteSize, Geyser);
        Add(device, "landmark.cave", SpriteSize, Cave);
        Add(device, "landmark.oasis", SpriteSize, Oasis);
        Add(device, "landmark.bigtree", SpriteSize, BigTree);
        Add(device, "landmark.berries", SpriteSize, Berries);
        Add(device, "landmark.herd", SpriteSize, Herd);
        Add(device, "landmark.shoal", SpriteSize, Shoal);
        Add(device, "landmark.vein", SpriteSize, OreVein);
        Add(device, "landmark.cliff", SpriteSize, Cliff);
        Add(device, "landmark.volcano", SpriteSize, Volcano);
        Add(device, "landmark.lava", SpriteSize, Lava);
        Add(device, "landmark.saltflat", SpriteSize, SaltFlat);
        Add(device, "landmark.waterfall", SpriteSize, Waterfall);
        Add(device, "landmark.reeds", SpriteSize, Reeds);
        Add(device, "landmark.driftwood", SpriteSize, Driftwood);
        Add(device, "landmark.tidepool", SpriteSize, TidePool);
        Add(device, "landmark.icearch", SpriteSize, IceArch);
        Add(device, "landmark.seal", SpriteSize, Seal);
        Add(device, "landmark.kelp", SpriteSize, Kelp);
        Add(device, "landmark.whale", SpriteSize, Whale);

        // Budovy.
        Add(device, "building.house", SpriteSize, canvas => House(canvas, new Color(196, 110, 66)));
        Add(device, "building.cottage", SpriteSize, canvas => House(canvas, new Color(176, 86, 74)));
        Add(device, "building.lumber_camp", SpriteSize, LumberCamp);
        Add(device, "building.lumberyard", SpriteSize, LumberCamp);
        Add(device, "building.tree_nursery", SpriteSize, TreeNursery);
        Add(device, "building.scout_balloon", SpriteSize, ScoutBalloon);
        Add(device, "building.radar_station", SpriteSize, RadarStation);
        Add(device, "building.sawmill", SpriteSize, Sawmill);
        Add(device, "building.quarry", SpriteSize, Quarry);
        Add(device, "building.mine", SpriteSize, Mine);
        Add(device, "building.farm", SpriteSize, Farm);
        Add(device, "building.plantation", SpriteSize, Farm);
        Add(device, "building.warehouse", SpriteSize, Warehouse);
        Add(device, "building.windmill", SpriteSize, Windmill);
        Add(device, "building.market", SpriteSize, Market);
        Add(device, "building.toolmaker", SpriteSize, Toolmaker);

        // Věda. Jeden tvar ve třech velikostech čte hráč jako jednu rodinu —
        // „tady se bádá" pozná i bez popisku.
        Add(device, "building.library", SpriteSize, canvas => Schoolhouse(canvas, floors: 1));
        Add(device, "building.school", SpriteSize, canvas => Schoolhouse(canvas, floors: 2));
        Add(device, "building.university", SpriteSize, canvas => Schoolhouse(canvas, floors: 3));
        Add(device, "building.research_lab", SpriteSize, ResearchLab);

        // Víra: rostoucí silueta se stejným znakem — svatyně, chrám, klášter.
        Add(device, "building.shrine", SpriteSize, canvas => Sanctuary(canvas, height: 12));
        Add(device, "building.temple", SpriteSize, canvas => Sanctuary(canvas, height: 20));
        Add(device, "building.monastery", SpriteSize, canvas => Sanctuary(canvas, height: 26));

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

        // Letouny (bod 42). ID musí sedět s 'aircraft' v data/vehicles.json —
        // AirTrafficSystem si sprite hledá pod "agent.<id>".
        Add(device, "agent.balloon", SpriteSize, Balloon);
        Add(device, "agent.airship", SpriteSize, Airship);
        Add(device, "agent.biplane", SpriteSize, Biplane);
        Add(device, "agent.airliner", SpriteSize, Airliner);
        Add(device, "agent.shuttle", SpriteSize, Shuttle);
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

        // Náprava krajiny — světlé, „čisté" tvary, ať se na mapě odliší od průmyslu,
        // který zamořil okolí.
        Add(device, "building.air_scrubber", SpriteSize, AirScrubber);
        Add(device, "building.water_treatment", SpriteSize, WaterTreatment);
        Add(device, "building.soil_remediation", SpriteSize, SoilRemediation);
        Add(device, "building.decontamination_yard", SpriteSize, DecontaminationYard);

        // Po meteoritu: co se z kráteru dá vytěžit a co se z toho dá postavit.
        Add(device, "building.uranium_mine", SpriteSize, UraniumMine);
        Add(device, "building.nuclear_plant", SpriteSize, NuclearPlant);

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
        Add(device, "building.particle_accelerator", SpriteSize, ParticleAccelerator);
        Add(device, "building.fusion_beacon", SpriteSize, FusionBeacon);

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

    // ----- ikony do HUD (24×24) -----

    private static readonly Color UiInk = new(232, 236, 244);
    private static readonly Color UiDim = new(170, 180, 196);

    /// <summary>
    /// Znak hry: slunce nad panoramatem rostoucího města.
    ///
    /// <para>Je to celý obsah hry v jednom obrázku — město, které roste samo,
    /// zatímco slunce jde nahoru. Kreslí se v odstínech, které používá i HUD,
    /// aby menu a hra vypadaly jako jedna věc.</para>
    /// </summary>
    private static void Logo(PixelCanvas c)
    {
        float w = c.Width;
        float h = c.Height;

        // Slunce nad obzorem.
        c.FillCircle(w * 0.5f, h * 0.34f, w * 0.20f, new Color(240, 205, 110));

        // Panorama: pět domů rostoucích zleva doprava — čte se to jako postup.
        var wall = new Color(58, 74, 96);
        var roof = new Color(212, 116, 96);
        float baseY = h * 0.82f;
        float[] heights = { 0.16f, 0.26f, 0.40f, 0.30f, 0.20f };
        float slot = w * 0.17f;
        for (int i = 0; i < heights.Length; i++)
        {
            float x = w * 0.09f + i * slot;
            float top = baseY - h * heights[i];
            c.FillRect((int)x, (int)top, (int)(slot * 0.78f), (int)(baseY - top), wall);
            c.FillTriangle(
                x - slot * 0.08f, top,
                x + slot * 0.39f, top - h * 0.07f,
                x + slot * 0.86f, top,
                roof);
        }

        // Zem pod městem.
        c.FillRect(0, (int)baseY, c.Width, (int)(h - baseY), new Color(74, 96, 66));
    }

    private static void UiBuild(PixelCanvas c)
    {
        c.FillTriangle(12f, 3f, 22f, 11f, 2f, 11f, new Color(212, 116, 96)); // střecha
        c.FillRect(4, 11, 16, 10, UiInk);
        c.FillRect(9, 15, 6, 6, new Color(90, 100, 118)); // dveře
    }

    private static void UiRoad(PixelCanvas c)
    {
        c.FillTriangle(7f, 2f, 17f, 2f, 21f, 22f, UiDim);
        c.FillTriangle(7f, 2f, 21f, 22f, 3f, 22f, UiDim);
        for (int i = 0; i < 3; i++)
        {
            c.FillRect(11 + i / 2, 4 + i * 7, 2, 4, new Color(250, 236, 170)); // středová čára
        }
    }

    private static void UiDemolish(PixelCanvas c)
    {
        c.FillRect(4, 10, 16, 3, new Color(214, 96, 88));  // krumpáč
        c.FillRect(11, 4, 3, 16, new Color(160, 120, 84));
        c.FillCircle(6f, 18f, 3f, UiDim);
        c.FillCircle(18f, 18f, 2.4f, UiDim);
    }

    private static void UiZone(PixelCanvas c)
    {
        for (int i = 0; i < 4; i++)
        {
            c.FillRect(3 + (i % 2) * 10, 3 + (i / 2) * 10, 8, 8,
                i % 3 == 0 ? new Color(120, 190, 230) : new Color(120, 190, 230) * 0.45f);
        }
    }

    private static void UiPlant(PixelCanvas c)
    {
        c.FillRect(11, 10, 2, 12, new Color(110, 82, 50));
        c.FillCircle(8f, 9f, 5f, new Color(72, 152, 70));
        c.FillCircle(16f, 11f, 4.5f, new Color(56, 128, 60));
        c.FillCircle(12f, 5f, 4f, new Color(88, 172, 78));
    }

    private static void UiTerraform(PixelCanvas c)
    {
        c.FillTriangle(12f, 4f, 22f, 16f, 2f, 16f, new Color(150, 140, 128)); // kopec
        c.FillRect(2, 16, 20, 5, new Color(110, 150, 92));                    // pláň
        c.FillRect(10, 2, 4, 6, new Color(250, 226, 150));                    // paprsek
    }

    private static void UiFestival(PixelCanvas c)
    {
        c.FillTriangle(12f, 20f, 5f, 6f, 19f, 6f, new Color(226, 136, 96)); // vlaječky
        c.FillCircle(8f, 6f, 2.4f, new Color(250, 214, 120));
        c.FillCircle(16f, 6f, 2.4f, new Color(160, 220, 240));
        c.FillCircle(12f, 3f, 2.4f, new Color(220, 140, 200));
    }

    private static void UiHome(PixelCanvas c)
    {
        c.FillTriangle(12f, 3f, 22f, 12f, 2f, 12f, new Color(226, 176, 96));
        c.FillRect(5, 12, 14, 9, UiInk);
        c.FillRect(10, 15, 4, 6, new Color(90, 100, 118));
    }

    private static void UiSettlements(PixelCanvas c)
    {
        c.FillRect(3, 12, 6, 9, UiDim);
        c.FillRect(10, 8, 6, 13, UiInk);
        c.FillRect(17, 14, 5, 7, UiDim);
        c.FillTriangle(13f, 3f, 17f, 8f, 9f, 8f, new Color(226, 176, 96));
    }

    private static void UiQuests(PixelCanvas c)
    {
        c.FillRect(4, 2, 16, 20, new Color(238, 232, 214)); // list
        for (int i = 0; i < 3; i++)
        {
            c.FillRect(7, 6 + i * 5, 10, 2, UiDim);
        }

        c.FillTriangle(6f, 12f, 11f, 18f, 9f, 19f, new Color(88, 180, 96)); // fajfka
    }

    private static void UiContracts(PixelCanvas c)
    {
        c.FillRect(3, 6, 18, 13, new Color(196, 156, 96)); // bedna
        c.FillRect(3, 11, 18, 3, new Color(150, 112, 66));
        c.FillRect(10, 6, 4, 13, new Color(150, 112, 66));
    }

    private static void UiGovernor(PixelCanvas c)
    {
        c.FillCircle(12f, 8f, 4.5f, new Color(226, 196, 150)); // hlava
        c.FillTriangle(12f, 12f, 21f, 22f, 3f, 22f, new Color(120, 150, 200)); // plášť
        c.FillRect(9, 2, 6, 3, new Color(226, 176, 96)); // čelenka
    }

    private static void UiAscend(PixelCanvas c)
    {
        c.FillTriangle(12f, 2f, 20f, 12f, 4f, 12f, new Color(190, 160, 235));
        c.FillRect(9, 12, 6, 10, new Color(150, 120, 205));
        c.FillCircle(12f, 18f, 2f, new Color(240, 226, 255));
    }

    private static void UiStats(PixelCanvas c)
    {
        c.FillRect(3, 14, 4, 8, new Color(120, 190, 230));
        c.FillRect(9, 9, 4, 13, new Color(150, 215, 160));
        c.FillRect(15, 4, 4, 18, new Color(250, 214, 120));
        c.FillRect(2, 21, 20, 2, UiDim);
    }

    private static void UiTrophy(PixelCanvas c)
    {
        c.FillCircle(12f, 8f, 6f, new Color(246, 206, 110));
        c.FillRect(10, 13, 4, 5, new Color(200, 160, 80));
        c.FillRect(6, 18, 12, 3, new Color(200, 160, 80));
        c.FillCircle(12f, 7f, 2.4f, new Color(255, 236, 180));
    }

    private static void UiChronicle(PixelCanvas c)
    {
        c.FillRect(3, 4, 18, 16, new Color(140, 96, 70)); // desky
        c.FillRect(5, 6, 14, 12, new Color(238, 232, 214));
        c.FillRect(11, 4, 2, 16, new Color(110, 74, 54)); // hřbet
    }

    private static void UiVote(PixelCanvas c)
    {
        c.FillRect(3, 9, 18, 12, new Color(150, 160, 180)); // urna
        c.FillRect(8, 3, 8, 7, new Color(238, 232, 214));   // lístek
        c.FillRect(9, 8, 6, 2, new Color(90, 100, 118));    // štěrbina
    }

    private static void UiPlay(PixelCanvas c) =>
        c.FillTriangle(6f, 3f, 20f, 12f, 6f, 21f, new Color(150, 220, 150));

    private static void UiPause(PixelCanvas c)
    {
        c.FillRect(6, 4, 4, 16, new Color(250, 214, 120));
        c.FillRect(14, 4, 4, 16, new Color(250, 214, 120));
    }

    private static void UiFast(PixelCanvas c)
    {
        c.FillTriangle(2f, 4f, 12f, 12f, 2f, 20f, new Color(150, 220, 190));
        c.FillTriangle(11f, 4f, 21f, 12f, 11f, 20f, new Color(150, 220, 190));
    }

    // ----- výrazná místa na mapě (32×32) -----

    /// <summary>Vrak lodi: nakloněný trup, zlomený stěžeň, cáry plachty.</summary>
    private static void Shipwreck(PixelCanvas c)
    {
        var hull = new Color(92, 66, 44);
        var plank = new Color(118, 86, 58);

        // Trup ležící na boku — proto lichoběžník, ne obdélník.
        c.FillTriangle(4f, 24f, 27f, 20f, 26f, 28f, hull);
        c.FillTriangle(4f, 24f, 26f, 28f, 8f, 29f, hull);
        c.FillRect(9, 22, 14, 2, plank);   // paluba
        c.FillRect(11, 25, 10, 1, plank);  // žebro

        // Zlomený stěžeň a zbytek ráhna.
        c.FillRect(17, 8, 2, 13, new Color(104, 76, 50));
        c.FillRect(13, 12, 9, 1, new Color(104, 76, 50));
        c.FillTriangle(19f, 9f, 25f, 15f, 19f, 15f, new Color(196, 190, 172)); // cár plachty
    }

    /// <summary>Ruiny: zbytky sloupů a padlý překlad.</summary>
    private static void Ruins(PixelCanvas c)
    {
        var stone = new Color(178, 168, 146);
        var shade = new Color(146, 136, 116);

        c.FillRect(7, 12, 4, 17, stone);   // stojící sloup
        c.FillRect(7, 10, 6, 2, shade);    // hlavice
        c.FillRect(15, 17, 4, 12, stone);  // ulomený sloup
        c.FillRect(23, 21, 4, 8, shade);   // pahýl
        c.FillRect(6, 28, 22, 2, shade);   // základ
        c.FillRect(19, 14, 9, 2, stone);   // padlý překlad
    }

    /// <summary>Kamenný kruh: menhiry v půlkruhu.</summary>
    private static void StoneCircle(PixelCanvas c)
    {
        var stone = new Color(150, 148, 142);
        var shade = new Color(120, 118, 112);
        c.FillRect(5, 16, 4, 12, stone);
        c.FillRect(12, 13, 4, 15, shade);
        c.FillRect(19, 13, 4, 15, stone);
        c.FillRect(26, 16, 4, 12, shade);
        c.FillRect(4, 28, 26, 2, new Color(104, 102, 96));
    }

    /// <summary>Kosti: žebra a kel trčící ze země.</summary>
    private static void Bones(PixelCanvas c)
    {
        var bone = new Color(226, 218, 196);
        for (int i = 0; i < 4; i++)
        {
            c.FillRect(9 + i * 4, 18 + (i % 2), 2, 10, bone);
        }

        c.FillCircle(24f, 22f, 4f, bone);        // lebka
        c.FillCircle(22f, 24f, 1.4f, new Color(120, 112, 96)); // očnice
        c.FillTriangle(26f, 20f, 31f, 26f, 27f, 26f, new Color(238, 232, 214)); // kel
    }

    /// <summary>Krystalová žíla: hranaté štěpy, ne kuličky.</summary>
    private static void Crystal(PixelCanvas c)
    {
        c.FillTriangle(16f, 5f, 21f, 20f, 11f, 20f, new Color(150, 210, 240));
        c.FillTriangle(16f, 5f, 21f, 20f, 17f, 20f, new Color(190, 235, 255));
        c.FillTriangle(9f, 13f, 13f, 26f, 5f, 26f, new Color(120, 180, 220));
        c.FillTriangle(24f, 15f, 28f, 26f, 20f, 26f, new Color(134, 196, 232));
        c.FillRect(6, 26, 21, 2, new Color(70, 96, 120));
    }

    /// <summary>Gejzír: kužel a sloup páry.</summary>
    private static void Geyser(PixelCanvas c)
    {
        c.FillTriangle(16f, 18f, 27f, 30f, 5f, 30f, new Color(126, 116, 104)); // kužel
        c.FillCircle(16f, 18f, 4f, new Color(96, 88, 80));                     // jícen
        c.FillCircle(16f, 12f, 5f, new Color(226, 240, 246) * 0.85f);
        c.FillCircle(16f, 6f, 4f, new Color(226, 240, 246) * 0.6f);
        c.FillCircle(16f, 2f, 3f, new Color(226, 240, 246) * 0.35f);
    }

    /// <summary>Ústí jeskyně: tmavý oblouk ve skále.</summary>
    private static void Cave(PixelCanvas c)
    {
        c.FillCircle(16f, 20f, 12f, new Color(122, 116, 108));
        c.FillRect(4, 20, 24, 10, new Color(122, 116, 108));
        c.FillCircle(16f, 22f, 7f, new Color(24, 22, 26));
        c.FillRect(9, 22, 14, 8, new Color(24, 22, 26));
        c.FillCircle(11f, 14f, 3f, new Color(148, 142, 134)); // odlesk na skále
    }

    /// <summary>Oáza: tůň a dvě palmy.</summary>
    private static void Oasis(PixelCanvas c)
    {
        c.FillCircle(16f, 23f, 9f, new Color(58, 140, 170));
        c.FillCircle(16f, 23f, 6f, new Color(84, 176, 200));

        c.FillRect(8, 8, 2, 12, new Color(104, 76, 50));
        c.FillCircle(9f, 8f, 4f, new Color(56, 128, 60));
        c.FillRect(23, 10, 2, 10, new Color(104, 76, 50));
        c.FillCircle(24f, 10f, 3.5f, new Color(46, 112, 52));
    }

    /// <summary>Prastarý strom: mohutný kmen a široká koruna.</summary>
    private static void BigTree(PixelCanvas c)
    {
        c.FillRect(13, 16, 6, 15, new Color(84, 56, 34));
        c.FillTriangle(13f, 22f, 8f, 30f, 14f, 30f, new Color(84, 56, 34)); // kořen
        c.FillTriangle(19f, 22f, 24f, 30f, 18f, 30f, new Color(84, 56, 34));
        c.FillCircle(16f, 12f, 11f, new Color(38, 92, 42));
        c.FillCircle(10f, 10f, 7f, new Color(48, 110, 48));
        c.FillCircle(22f, 11f, 7f, new Color(32, 82, 38));
        c.FillCircle(16f, 5f, 6f, new Color(56, 124, 54));
    }

    /// <summary>Keře s bobulemi.</summary>
    private static void Berries(PixelCanvas c)
    {
        c.FillCircle(11f, 22f, 7f, new Color(46, 104, 50));
        c.FillCircle(21f, 21f, 7f, new Color(38, 92, 44));
        for (int i = 0; i < 6; i++)
        {
            c.FillCircle(8f + i * 3.4f, 19f + (i % 3) * 3f, 1.6f, new Color(206, 62, 78));
        }
    }

    /// <summary>Stádo: tři siluety zvířat.</summary>
    private static void Herd(PixelCanvas c)
    {
        for (int i = 0; i < 3; i++)
        {
            int x = 4 + i * 9;
            int y = 14 + (i % 2) * 6;
            c.FillRect(x, y, 7, 4, new Color(140, 96, 60));   // tělo
            c.FillRect(x + 5, y - 2, 3, 3, new Color(124, 84, 52)); // hlava
            c.FillRect(x + 1, y + 4, 1, 3, new Color(96, 66, 42));  // nohy
            c.FillRect(x + 5, y + 4, 1, 3, new Color(96, 66, 42));
        }
    }

    /// <summary>Hejno ryb.</summary>
    private static void Shoal(PixelCanvas c)
    {
        for (int i = 0; i < 5; i++)
        {
            float x = 6f + (i % 3) * 9f;
            float y = 10f + (i / 3) * 10f + (i % 2) * 4f;
            c.FillCircle(x, y, 3f, new Color(120, 190, 210));
            c.FillTriangle(x - 3f, y, x - 6f, y - 2.5f, x - 6f, y + 2.5f, new Color(96, 165, 190));
        }
    }

    /// <summary>Rudná žíla: kámen s lesklými zrny.</summary>
    private static void OreVein(PixelCanvas c)
    {
        c.FillCircle(16f, 21f, 11f, new Color(104, 100, 96));
        c.FillCircle(12f, 18f, 6f, new Color(126, 122, 116));
        c.FillCircle(12f, 19f, 2.2f, new Color(214, 158, 78));
        c.FillCircle(20f, 22f, 2f, new Color(214, 158, 78));
        c.FillCircle(17f, 15f, 1.6f, new Color(236, 190, 110));
    }

    /// <summary>Sráz / mesa: stupňovitá skála.</summary>
    private static void Cliff(PixelCanvas c)
    {
        c.FillRect(4, 18, 24, 13, new Color(146, 106, 76));
        c.FillRect(7, 12, 18, 6, new Color(168, 124, 88));
        c.FillRect(11, 7, 10, 5, new Color(186, 140, 100));
        c.FillRect(4, 18, 24, 2, new Color(120, 86, 62)); // vrstva
        c.FillRect(7, 12, 18, 2, new Color(140, 102, 72));
    }

    /// <summary>Sopka: kužel, kráter a dým.</summary>
    private static void Volcano(PixelCanvas c)
    {
        c.FillTriangle(16f, 6f, 30f, 30f, 2f, 30f, new Color(92, 78, 74));
        c.FillTriangle(16f, 9f, 24f, 30f, 8f, 30f, new Color(112, 96, 90));
        c.FillCircle(16f, 8f, 4f, new Color(214, 92, 48));  // kráter
        c.FillCircle(16f, 4f, 3f, new Color(90, 84, 82) * 0.8f); // dým
        c.FillTriangle(16f, 10f, 20f, 22f, 13f, 22f, new Color(226, 118, 56)); // láva
    }

    /// <summary>Lávové jezero.</summary>
    private static void Lava(PixelCanvas c)
    {
        c.FillCircle(16f, 18f, 12f, new Color(72, 44, 40));
        c.FillCircle(16f, 18f, 9f, new Color(198, 74, 36));
        c.FillCircle(13f, 16f, 4f, new Color(240, 150, 60));
        c.FillCircle(20f, 21f, 3f, new Color(250, 196, 90));
    }

    /// <summary>Solná pláň: bílé desky s prasklinami.</summary>
    private static void SaltFlat(PixelCanvas c)
    {
        c.FillRect(3, 10, 26, 16, new Color(232, 230, 222));
        c.FillRect(3, 17, 26, 1, new Color(196, 194, 186));
        c.FillRect(11, 10, 1, 16, new Color(196, 194, 186));
        c.FillRect(21, 10, 1, 16, new Color(196, 194, 186));
    }

    /// <summary>Vodopád: sráz a padající voda.</summary>
    private static void Waterfall(PixelCanvas c)
    {
        c.FillRect(3, 6, 26, 22, new Color(118, 110, 100));
        c.FillRect(11, 4, 10, 24, new Color(120, 186, 216));
        c.FillRect(13, 4, 2, 24, new Color(186, 226, 240));
        c.FillCircle(16f, 28f, 6f, new Color(150, 205, 226)); // tůň
        c.FillCircle(16f, 28f, 3f, new Color(210, 238, 246));
    }

    /// <summary>Rákosí / rašeliniště.</summary>
    private static void Reeds(PixelCanvas c)
    {
        c.FillCircle(16f, 25f, 10f, new Color(78, 82, 60));
        for (int i = 0; i < 7; i++)
        {
            int x = 5 + i * 4;
            c.FillRect(x, 10 + (i % 3) * 3, 1, 16, new Color(126, 138, 78));
            c.FillCircle(x + 0.5f, 10f + (i % 3) * 3f, 1.4f, new Color(150, 116, 68));
        }
    }

    /// <summary>Naplavené dřevo.</summary>
    private static void Driftwood(PixelCanvas c)
    {
        c.FillRect(4, 20, 24, 4, new Color(158, 140, 116));
        c.FillRect(9, 15, 16, 3, new Color(178, 160, 134));
        c.FillTriangle(24f, 20f, 30f, 16f, 28f, 23f, new Color(148, 130, 108));
    }

    /// <summary>Přílivová tůň.</summary>
    private static void TidePool(PixelCanvas c)
    {
        c.FillCircle(16f, 20f, 11f, new Color(126, 122, 114));
        c.FillCircle(16f, 20f, 8f, new Color(84, 168, 186));
        c.FillCircle(14f, 19f, 2f, new Color(210, 120, 130)); // sasanka
        c.FillCircle(20f, 22f, 1.5f, new Color(240, 190, 110));
    }

    /// <summary>Ledový oblouk.</summary>
    private static void IceArch(PixelCanvas c)
    {
        c.FillCircle(16f, 22f, 13f, new Color(196, 226, 240));
        c.FillCircle(16f, 26f, 8f, new Color(70, 120, 160));
        c.FillRect(8, 26, 16, 6, new Color(70, 120, 160));
        c.FillCircle(11f, 13f, 3f, new Color(230, 245, 252));
    }

    /// <summary>Kolonie tuleňů.</summary>
    private static void Seal(PixelCanvas c)
    {
        for (int i = 0; i < 3; i++)
        {
            float x = 8f + i * 8f;
            float y = 18f + (i % 2) * 6f;
            c.FillCircle(x, y, 4f, new Color(96, 96, 104));
            c.FillCircle(x + 3.5f, y - 2f, 2.2f, new Color(112, 112, 120));
            c.FillTriangle(x - 4f, y, x - 8f, y - 2f, x - 8f, y + 2f, new Color(84, 84, 92));
        }
    }

    /// <summary>Chaluhový les.</summary>
    private static void Kelp(PixelCanvas c)
    {
        for (int i = 0; i < 5; i++)
        {
            int x = 5 + i * 5;
            c.FillRect(x, 6 + (i % 3) * 4, 2, 24, new Color(56, 104, 66));
            c.FillCircle(x + 1f, 8f + (i % 3) * 4f, 2.4f, new Color(74, 130, 78));
            c.FillCircle(x + 1f, 16f + (i % 2) * 4f, 2f, new Color(66, 118, 72));
        }
    }

    /// <summary>Hejno velryb: hřbet a fontána.</summary>
    private static void Whale(PixelCanvas c)
    {
        c.FillCircle(15f, 22f, 10f, new Color(64, 84, 110));
        c.FillCircle(15f, 25f, 10f, new Color(74, 132, 168)); // ponořená část splývá s vodou
        c.FillTriangle(26f, 20f, 31f, 15f, 30f, 23f, new Color(58, 76, 100)); // ocas
        c.FillCircle(11f, 10f, 2.5f, new Color(220, 240, 248) * 0.9f); // fontána
        c.FillCircle(11f, 6f, 2f, new Color(220, 240, 248) * 0.6f);
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
    /// <summary>Horkovzdušný balon: pruhovaná kopule a košík pod ní.</summary>
    private static void Balloon(PixelCanvas c)
    {
        c.FillCircle(16f, 13f, 8f, new Color(216, 101, 79));
        c.FillRect(12, 6, 3, 14, new Color(240, 232, 214)); // světlý pruh
        c.FillRect(18, 6, 3, 14, new Color(240, 232, 214));
        c.FillTriangle(10f, 18f, 22f, 18f, 16f, 24f, new Color(190, 84, 66)); // hrdlo
        c.FillRect(14, 24, 5, 4, new Color(126, 92, 56));  // koš
        c.FillRect(13, 22, 1, 3, new Color(96, 82, 70));   // lana
        c.FillRect(19, 22, 1, 3, new Color(96, 82, 70));
    }

    /// <summary>Vzducholoď: doutník s ocasními plochami a gondolou.</summary>
    private static void Airship(PixelCanvas c)
    {
        c.FillCircle(15f, 14f, 7f, new Color(201, 207, 214));
        c.FillRect(8, 10, 16, 9, new Color(214, 220, 226));
        c.FillCircle(8f, 14.5f, 4.5f, new Color(201, 207, 214));
        c.FillTriangle(24f, 10f, 30f, 8f, 24f, 15f, new Color(176, 184, 192)); // kormidlo
        c.FillTriangle(24f, 18f, 30f, 20f, 24f, 14f, new Color(176, 184, 192));
        c.FillRect(13, 19, 7, 3, new Color(120, 116, 110)); // gondola
    }

    /// <summary>Dvouplošník: dvě křídla nad sebou a vrtule vepředu.</summary>
    private static void Biplane(PixelCanvas c)
    {
        c.FillRect(6, 15, 20, 4, new Color(224, 192, 96));   // trup
        c.FillTriangle(26f, 15f, 26f, 19f, 31f, 17f, new Color(200, 168, 80));
        c.FillRect(9, 10, 14, 2, new Color(238, 210, 120));  // horní křídlo
        c.FillRect(9, 20, 14, 2, new Color(238, 210, 120));  // dolní křídlo
        c.FillRect(15, 12, 2, 8, new Color(180, 150, 70));   // vzpěra
        c.FillRect(4, 12, 2, 10, new Color(150, 152, 156));  // vrtule
        c.FillRect(5, 8, 4, 3, new Color(200, 168, 80));     // směrovka
    }

    /// <summary>Dopravní letadlo: šípová křídla a řada okének.</summary>
    private static void Airliner(PixelCanvas c)
    {
        c.FillRect(3, 14, 26, 5, new Color(234, 239, 244)); // trup
        c.FillCircle(29f, 16.5f, 2.5f, new Color(234, 239, 244));
        c.FillTriangle(10f, 16f, 22f, 16f, 14f, 27f, new Color(210, 218, 226)); // křídlo dolů
        c.FillTriangle(10f, 16f, 22f, 16f, 14f, 5f, new Color(220, 228, 236));  // křídlo nahoru
        c.FillTriangle(3f, 16f, 9f, 16f, 4f, 8f, new Color(96, 140, 200));      // ocas
        for (int i = 0; i < 6; i++)
        {
            c.FillRect(9 + i * 3, 16, 1, 1, new Color(120, 160, 210)); // okénka
        }
    }

    /// <summary>Raketoplán: delta křídlo a plamen z motorů.</summary>
    private static void Shuttle(PixelCanvas c)
    {
        c.FillTriangle(4f, 12f, 4f, 21f, 28f, 16.5f, new Color(226, 236, 246)); // trup
        c.FillTriangle(6f, 16f, 18f, 16f, 8f, 28f, new Color(184, 216, 240));   // delta dolů
        c.FillTriangle(6f, 17f, 18f, 17f, 8f, 5f, new Color(198, 226, 246));    // delta nahoru
        c.FillRect(3, 13, 2, 7, new Color(120, 130, 140));                      // motory
        c.FillCircle(1.5f, 16.5f, 2.4f, new Color(255, 190, 90));               // plamen
        c.FillCircle(0.5f, 16.5f, 1.4f, new Color(255, 240, 190));
    }

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

    /// <summary>Čistička vzduchu: filtrační věž, ze které stoupá čistý závan.</summary>
    private static void AirScrubber(PixelCanvas c)
    {
        c.FillRect(7, 20, 18, 10, new Color(150, 158, 160));   // technologický blok
        c.FillRect(10, 8, 12, 13, new Color(196, 204, 202));   // filtrační věž
        c.FillRect(12, 11, 8, 3, new Color(120, 206, 190));    // filtrační patro
        c.FillRect(12, 16, 8, 3, new Color(120, 206, 190));
        c.FillCircle(16f, 6f, 3.2f, new Color(214, 240, 236)); // čistý výdech
        c.FillRect(8, 24, 16, 2, new Color(118, 124, 128));
    }

    /// <summary>Čistička vod: kruhové usazovací nádrže s čeřicím ramenem.</summary>
    private static void WaterTreatment(PixelCanvas c)
    {
        c.FillRect(2, 8, 28, 22, new Color(158, 160, 156)); // betonová deska
        c.FillCircle(11f, 16f, 6.4f, new Color(120, 124, 124));
        c.FillCircle(11f, 16f, 5.2f, new Color(96, 166, 200)); // nádrž
        c.FillCircle(22f, 23f, 5.4f, new Color(120, 124, 124));
        c.FillCircle(22f, 23f, 4.3f, new Color(112, 182, 208));
        c.FillRect(10, 10, 2, 12, new Color(206, 210, 208)); // čeřicí rameno
        c.FillRect(6, 15, 10, 2, new Color(206, 210, 208));
    }

    /// <summary>Sanace půdy: rozorané pásy, které se pod fólií vracejí do zeleně.</summary>
    /// <summary>Uran: ruda se zeleným svitem — na první pohled patrné, že to nesvítí normálně.</summary>
    private static void UraniumIcon(PixelCanvas c)
    {
        c.FillCircle(11f, 13f, 7f, new Color(74, 82, 62));
        c.FillCircle(11f, 13f, 4.5f, new Color(122, 156, 62));
        c.FillCircle(9.5f, 11.5f, 2.2f, new Color(186, 232, 84));
        c.FillCircle(14f, 16f, 1.4f, new Color(205, 245, 110));
    }

    /// <summary>Uranový důl: těžní věž nad zeleně svítící jámou.</summary>
    private static void UraniumMine(PixelCanvas c)
    {
        c.FillRect(2, 20, 28, 10, new Color(86, 94, 58));      // spečená zem
        c.FillCircle(16f, 25f, 7f, new Color(118, 150, 64));   // jáma
        c.FillCircle(16f, 25f, 4f, new Color(168, 214, 78));   // svit z hloubky

        c.FillRect(8, 6, 3, 16, new Color(120, 118, 112));     // nohy těžní věže
        c.FillRect(21, 6, 3, 16, new Color(120, 118, 112));
        c.FillRect(8, 6, 16, 3, new Color(146, 144, 136));     // rám
        c.FillCircle(16f, 7.5f, 3f, new Color(96, 94, 90));    // kladka
        c.FillRect(15, 9, 2, 12, new Color(88, 86, 82));       // lano do jámy
    }

    /// <summary>Jaderná elektrárna: chladicí věž s párou a reaktorová kopule.</summary>
    private static void NuclearPlant(PixelCanvas c)
    {
        c.FillRect(2, 24, 28, 6, new Color(120, 124, 118));    // areál

        // Chladicí věž — nezaměnitelný obrys, poznat i v oddálení.
        c.FillTriangle(5f, 24f, 11f, 24f, 7f, 12f, new Color(198, 202, 200));
        c.FillTriangle(11f, 24f, 15f, 12f, 7f, 12f, new Color(212, 216, 214));
        c.FillRect(7, 10, 8, 3, new Color(182, 186, 184));
        c.FillCircle(11f, 7f, 3.4f, new Color(226, 232, 230));  // pára
        c.FillCircle(14f, 5f, 2.2f, new Color(238, 242, 240));

        c.FillCircle(23f, 22f, 6f, new Color(150, 172, 168));   // kopule reaktoru
        c.FillCircle(23f, 22f, 3f, new Color(120, 196, 178));
        c.FillRect(17, 22, 12, 3, new Color(136, 150, 148));
    }

    /// <summary>Dekontaminační stanice: sprchová brána a nádrž na svlečenou hlínu.</summary>
    private static void DecontaminationYard(PixelCanvas c)
    {
        c.FillRect(2, 20, 28, 10, new Color(104, 112, 96));    // zamořená plocha
        c.FillRect(4, 24, 24, 2, new Color(158, 196, 130));    // vyčištěný pruh

        c.FillRect(6, 8, 3, 14, new Color(196, 204, 208));     // sloupy brány
        c.FillRect(20, 8, 3, 14, new Color(196, 204, 208));
        c.FillRect(6, 6, 17, 3, new Color(220, 228, 232));     // příčník
        for (int i = 0; i < 4; i++)
        {
            c.FillRect(9 + i * 3, 10, 1, 8, new Color(178, 220, 236)); // trysky
        }

        c.FillCircle(27f, 16f, 4f, new Color(150, 158, 150));  // nádrž na kal
        c.FillCircle(27f, 15f, 2f, new Color(120, 150, 96));
    }

    private static void SoilRemediation(PixelCanvas c)
    {
        c.FillRect(2, 12, 28, 18, new Color(120, 92, 62)); // otrávená zem
        for (int i = 0; i < 4; i++)
        {
            c.FillRect(4, 14 + i * 4, 24, 2, new Color(96, 156, 78)); // ozdravené pásy
        }

        c.FillRect(2, 9, 28, 3, new Color(186, 206, 196)); // fólie nad plochou
        c.FillRect(5, 4, 3, 6, new Color(150, 158, 160));  // odsávací sloupek
        c.FillCircle(6.5f, 3f, 2.2f, new Color(196, 220, 210));
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

    /// <summary>
    /// Budova vědy: cihlový dům se štítem, hodinami a rozsvícenými okny. Počet
    /// pater odlišuje knihovnu, školu a univerzitu — stejná rodina, jiná váha.
    /// </summary>
    private static void Schoolhouse(PixelCanvas c, int floors)
    {
        var wall = new Color(178, 156, 132);
        var roof = new Color(96, 110, 134);
        var window = new Color(126, 178, 226);

        int top = Math.Max(4, 22 - floors * 6);
        c.FillRect(4, top, 24, 30 - top, wall);
        c.FillTriangle(2f, top, 30f, top, 16f, top - 5f, roof);

        // Okna po patrech — rozsvícená, protože se v nich pracuje i po setmění.
        for (int floor = 0; floor < floors; floor++)
        {
            int y = top + 3 + floor * 6;
            for (int i = 0; i < 3; i++)
            {
                c.FillRect(7 + i * 7, y, 4, 4, window);
            }
        }

        c.FillRect(14, 24, 5, 6, new Color(120, 92, 64)); // dveře
    }

    /// <summary>Výzkumný ústav: nízká hala s kopulí a anténou — věda pozdní éry.</summary>
    private static void ResearchLab(PixelCanvas c)
    {
        c.FillRect(3, 16, 26, 14, new Color(196, 202, 212));
        c.FillCircle(16, 15, 8, new Color(150, 176, 206)); // kopule
        c.FillRect(15, 2, 2, 8, new Color(120, 128, 140)); // anténa
        c.FillCircle(16, 3, 2, new Color(226, 138, 92));
        for (int i = 0; i < 4; i++)
        {
            c.FillRect(5 + i * 6, 20, 4, 5, new Color(126, 178, 226));
        }
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

    /// <summary>Lesní školka: nízký srub a řádky sazenic — opak hromady klád.</summary>
    private static void TreeNursery(PixelCanvas c)
    {
        c.FillRect(3, 20, 12, 10, new Color(118, 96, 62));
        c.FillTriangle(1f, 20f, 17f, 20f, 9f, 13f, new Color(90, 72, 46));

        // Řádky sazenic ve školce.
        for (int i = 0; i < 3; i++)
        {
            int x = 19 + i * 4;
            c.FillRect(x, 26, 1, 4, new Color(110, 84, 52));
            c.FillCircle(x + 0.5f, 24f, 2.6f, new Color(88, 150, 72));
        }

        c.FillRect(18, 30, 12, 1, new Color(70, 96, 58));
    }

    /// <summary>Pátrací balon: kotviště a nad ním balon na laně.</summary>
    private static void ScoutBalloon(PixelCanvas c)
    {
        c.FillRect(10, 26, 12, 5, new Color(110, 92, 64));       // kotviště
        c.FillRect(15, 14, 1, 12, new Color(180, 170, 150));     // lano
        c.FillCircle(16f, 10f, 7.5f, new Color(200, 106, 106));  // obal
        c.FillCircle(16f, 10f, 4.5f, new Color(224, 140, 132));
        c.FillRect(14, 17, 5, 3, new Color(120, 96, 62));        // koš
    }

    /// <summary>Radar: budova s otočnou parabolou.</summary>
    private static void RadarStation(PixelCanvas c)
    {
        c.FillRect(6, 20, 20, 11, new Color(96, 108, 118));
        c.FillRect(8, 22, 16, 3, new Color(70, 82, 92));
        c.FillRect(15, 12, 2, 9, new Color(150, 160, 170));      // stožár
        c.FillTriangle(9f, 6f, 23f, 12f, 16f, 14f, new Color(190, 205, 215)); // parabola
        c.FillCircle(16f, 11f, 1.6f, new Color(120, 200, 210));
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

    /// <summary>Urychlovač částic: zapuštěný prstenec se svítící drahou uvnitř.</summary>
    private static void ParticleAccelerator(PixelCanvas c)
    {
        c.FillCircle(16f, 17f, 13f, new Color(58, 66, 72));
        c.FillCircle(16f, 17f, 11f, new Color(88, 98, 104));
        c.FillCircle(16f, 17f, 8.5f, new Color(46, 54, 60));

        // Svítící dráha uvnitř prstence — to je ta věc, kvůli které se tam kouká.
        c.FillCircle(16f, 17f, 7f, new Color(127, 227, 196));
        c.FillCircle(16f, 17f, 5.5f, new Color(24, 32, 38));

        // Injektory na obvodu.
        c.FillRect(14, 2, 4, 6, new Color(120, 132, 138));
        c.FillRect(2, 15, 6, 4, new Color(120, 132, 138));
        c.FillRect(24, 15, 6, 4, new Color(120, 132, 138));
        c.FillCircle(16f, 4f, 1.6f, new Color(190, 255, 235));
    }

    /// <summary>Fúzní maják: štíhlá věž s žhnoucím jádrem na vrcholu.</summary>
    private static void FusionBeacon(PixelCanvas c)
    {
        c.FillTriangle(9f, 30f, 23f, 30f, 16f, 6f, new Color(74, 78, 88));
        c.FillTriangle(11f, 30f, 21f, 30f, 16f, 9f, new Color(104, 110, 122));
        c.FillRect(6, 28, 20, 3, new Color(62, 66, 74));

        // Jádro nahoře: dvě vrstvy, ať vypadá rozpálené zevnitř.
        c.FillCircle(16f, 7f, 4.2f, new Color(255, 217, 138));
        c.FillCircle(16f, 7f, 2.2f, new Color(255, 252, 226));
        c.FillCircle(16f, 18f, 1.6f, new Color(255, 217, 138) * 0.6f);
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

    /// <summary>Víra: prostá stavba se štítem a stoupajícím dýmem oběti.</summary>
    private static void Sanctuary(PixelCanvas c, int height)
    {
        int top = 30 - height;
        c.FillRect(7, top + 4, 18, height - 4, new Color(216, 200, 156));
        c.FillTriangle(5f, top + 4f, 27f, top + 4f, 16f, top, new Color(176, 150, 96));
        c.FillRect(15, Math.Max(0, top - 6), 2, 6, new Color(200, 176, 120)); // sloupek
        c.FillRect(13, Math.Max(0, top - 5), 6, 2, new Color(200, 176, 120)); // příčka
        c.FillRect(14, 24, 5, 6, new Color(120, 96, 60));                     // vchod
    }

    /// <summary>Víra: plamínek oběti — teplý bod, čitelný i v malém.</summary>
    private static void FaithIcon(PixelCanvas c)
    {
        c.FillRect(6, 17, 12, 3, new Color(150, 120, 70)); // miska
        c.FillTriangle(12f, 4f, 8f, 16f, 16f, 16f, new Color(240, 190, 90));
        c.FillTriangle(12f, 8f, 10f, 16f, 14f, 16f, new Color(252, 238, 180));
    }

    /// <summary>Věda: otevřená kniha se záložkou — čitelná i na 24 pixelech.</summary>
    private static void ScienceIcon(PixelCanvas c)
    {
        var cover = new Color(74, 116, 168);
        var page = new Color(238, 240, 246);

        c.FillRect(3, 6, 18, 13, cover);
        c.FillRect(4, 7, 7, 11, page);   // levá strana
        c.FillRect(13, 7, 7, 11, page);  // pravá strana
        c.FillRect(11, 6, 2, 13, new Color(52, 84, 126)); // hřbet
        c.FillRect(17, 4, 3, 8, new Color(214, 118, 92)); // záložka
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

    /// <summary>Vylepšení: šipka nahoru nad základnou — „tahle budova povyroste".</summary>
    private static void UpgradeIcon(PixelCanvas c)
    {
        var glow = new Color(150, 220, 150);
        for (int i = 0; i < 7; i++)
        {
            c.FillRect(12 - i, 6 + i, 1 + i * 2, 2, glow);
        }

        c.FillRect(9, 13, 6, 6, glow);
        c.FillRect(5, 19, 14, 2, new Color(90, 140, 95));
    }

    /// <summary>Sloučení: čtyři čtverce, které se stáhnou do jednoho většího.</summary>
    private static void MergeIcon(PixelCanvas c)
    {
        var small = new Color(120, 160, 210);
        c.FillRect(3, 3, 6, 6, small);
        c.FillRect(15, 3, 6, 6, small);
        c.FillRect(3, 15, 6, 6, small);
        c.FillRect(15, 15, 6, 6, small);

        // Velký uprostřed překryje rohy — je vidět, že z nich vzniká.
        c.FillRect(8, 8, 8, 8, new Color(230, 200, 110));
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
