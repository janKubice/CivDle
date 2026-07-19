# Data-Driven Content — Architektura

*Pracovní dokument · verze 0.1*

Odpověď na otázku „lze to?": **Ano — a je to přesně správný přístup pro tuhle hru.** Když chceš přidávat budovy, suroviny, vozidla, lidi a biomy bez sáhnutí do kódu, data-driven design je standardní řešení. Navíc sedí na tvůj „no balast" duch: v .NET je JSON vestavěný, žádná další závislost.

Tento dokument navazuje na **Tech Stack** (data-oriented C#, MonoGame) a **Content Design** / **Budovy**.

---

## 1. Klíčový princip: definice vs. instance

Nejdůležitější věc, kterou je potřeba pochopit:

| Vrstva | Co to je | Kde žije |
|---|---|---|
| **Definice (typ)** | šablona: „co je Huť" — cena, sloty, recept, sprite | JSON soubory na disku |
| **Instance (kus)** | konkrétní huť postavená na [x, y] se 3 dělníky | data-oriented pole v simulaci (RAM) |

JSON popisuje **typy**. Simulace drží **instance**, které na typ jen odkazují přes ID. Milion budov v paměti neznamená milion JSONů — znamená milion malých struktur, každá s odkazem „jsem typ `smelter`".

```
data/buildings.json  ──načte se při startu──►  Registry (Dictionary<string, Def>)
                                                      ▲
                                                      │ instance odkazuje přes index
simulace: BuildingInstance { defIndex, x, y, workers }  (ploché pole, viz tech dok)
```

Tím se data-driven přístup a data-oriented výkon z tech doku nevylučují — naopak spolu ladí.

---

## 2. Čím to udělat (a proč JSON)

**Doporučení: JSON přes `System.Text.Json`** (vestavěné v .NET 8, nulová závislost navíc).

| Formát | Pro | Proti |
|---|---|---|
| **JSON** ✅ | vestavěný v .NET, všude podporovaný, snadno moddovatelný | trochu upovídaný |
| YAML | čitelnější | potřebuje knihovnu, méně obvyklé v C# |
| TOML | pěkný na config | slabší na vnořená data |
| CSV | skvělý na tabulky (ceny, tiery) | neumí vnořené struktury (recepty) |

**Praktický tip:** klidně kombinuj — složité definice (budovy, biomy) v JSON, prosté tabulky (např. cena upgradů po tierech) klidně v CSV. Ale jako default drž JSON, ať máš jeden systém.

---

## 3. Konkrétní schémata

Ukázková podoba definičních souborů. Sprity se **odkazují cestou / ID**, ne že by binárka byla v JSON (viz sekce 4).

### 3.1 Surovina

```json
{
  "id": "iron",
  "name": "Železo",
  "tier": 3,
  "type": "stock",
  "icon": "icons/iron.png",
  "baseValue": 12
}
```

### 3.2 Budova

```json
{
  "id": "smelter",
  "name": "Huť",
  "category": "industry",
  "tier": 3,
  "footprint": [2, 2],
  "workerSlots": 4,
  "sprite": "buildings/smelter.png",
  "buildCost": { "stone": 40, "planks": 20 },
  "upkeep": { "coal": 2 },
  "recipe": {
    "input": { "ironOre": 3, "coal": 1 },
    "output": { "iron": 2 },
    "timeTicks": 5
  },
  "upgradesTo": "blast_furnace"
}
```

### 3.3 Vozidlo

```json
{
  "id": "cargo_truck",
  "name": "Náklaďák",
  "sprite": "vehicles/truck.png",
  "network": "road",
  "speed": 3.5,
  "era": 5,
  "role": "cargo_visual"
}
```

### 3.4 Práce / člověk

```json
{
  "id": "miner",
  "name": "Horník",
  "sprite": "people/miner.png",
  "worksIn": ["mine", "quarry"],
  "produces": "ore"
}
```

### 3.5 Biom

```json
{
  "id": "mountains",
  "name": "Hory",
  "tileSprites": ["terrain/mountain_1.png", "terrain/mountain_2.png"],
  "resources": ["ore", "coal", "stone"],
  "modifiers": {
    "farmingYield": 0.2,
    "buildCostMult": 1.5,
    "miningYield": 1.5
  },
  "events": ["rockslide"]
}
```

### 3.6 Megastruktura

```json
{
  "id": "great_dam",
  "name": "Velká přehrada",
  "category": "megastructure",
  "footprint": [6, 3],
  "sprite": "mega/dam.png",
  "buildStages": 4,
  "buildCost": { "concrete": 500, "steel": 300 },
  "globalBonus": { "energy": 50, "water": 30 },
  "terrainEffect": "flood_valley"
}
```

Všimni si `terrainEffect`: `"flood_valley"` je jen **odkaz na chování**, které je v kódu. To je hranice mezi daty a logikou — viz sekce 6.

---

## 4. Sprity a assety

- JSON drží **cestu nebo ID spritu jako řetězec** (`"buildings/smelter.png"`), nikdy ne binární data.
- Content loader při startu tuhle cestu namapuje na načtenou texturu (ideálně region v **texture atlasu** — viz tech dok, kvůli batchingu).
- Doporučený mezikrok: JSON odkazuje na **logické ID** (`"smelter"`), a separátní atlas-mapa říká, kde v atlasu ten sprite leží. Když překreslíš grafiku, měníš atlas, ne definice.

```
building.sprite = "smelter"  ──►  atlas lookup  ──►  Rectangle v texture atlasu
```

---

## 5. Jak to načíst (náčrt v C#)

Minimalistický vzor, bez frameworků navíc:

```csharp
// 1) Definice = neměnný record
public record BuildingDef(
    string Id, string Name, string Category, int Tier,
    int[] Footprint, int WorkerSlots, string Sprite,
    Dictionary<string,int> BuildCost, Recipe? Recipe);

// 2) Načtení všech definic při startu do registru
string json = File.ReadAllText("data/buildings.json");
BuildingDef[] defs = JsonSerializer.Deserialize<BuildingDef[]>(json)!;

// mapa id -> index (string se hledá jen jednou, pak se pracuje s int)
var idToIndex = new Dictionary<string,int>();
for (int i = 0; i < defs.Length; i++) idToIndex[defs[i].Id] = i;

// 3) Instance v simulaci drží jen INDEX, ne string ani celý objekt
struct BuildingInstance {
    public int DefIndex;   // rychlý odkaz do defs[]
    public int X, Y;
    public int Workers;
    public int Progress;
}
```

Pointa: string ID se přeloží na `int` index **jednou při načtení**. Za běhu simulace pracuje jen s indexy do plochých polí — cache-friendly, přesně jak chce tech dok.

---

## 6. Co patří do dat a co do kódu *(důležitý caveat)*

Data-driven neznamená „všechno v JSON". Do dat patří **deklarace** (čísla, ceny, tiery, vstup→výstup, odkazy, sprity). Do kódu patří **chování** (algoritmy, unikátní efekty).

| Do JSON (data) | Do kódu (logika) |
|---|---|
| cena, sloty, tier, footprint | jak probíhá tik výroby |
| recept vstup → výstup | jak se rozdělují dělníci |
| odkaz na sprite | jak se vykresluje atlas |
| `terrainEffect: "flood_valley"` | *co „flood_valley" skutečně udělá* |
| modifikátory biomu | jak se modifikátory aplikují |

Trik na unikátní chování: JSON obsahuje **behavior ID** (řetězec jako `"flood_valley"`, `"terraform"`), a v kódu máš tabulku `Dictionary<string, IBehavior>`, která ID mapuje na implementaci. Tím zůstane 95 % obsahu čistě v datech a jen pár speciálních efektů žije v kódu. Kdybys chtěl jít dál, dá se přidat malý skriptovací layer (Lua, C# scripting), ale pro start to není potřeba a je to balast navíc.

---

## 7. Co tím získáš

- **Rychlá iterace** — přidat surovinu nebo budovu = pár řádků JSON, žádná rekompilace.
- **Hot-reload při vývoji** — načti JSON za běhu a hned vidíš změnu; ladění balance bez restartu.
- **Moddovatelnost** — hráči můžou přidávat obsah editací / přidáním JSON. Přirozeně navazuje na **Steam Workshop** (viz tech dok), když se pro to jednou rozhodneš.
- **Čistota kódu** — logika a obsah oddělené; kód neroste s každou novou budovou.

---

## 8. Na co si dát pozor

- **Validace při načtení (fail-fast).** Když budova odkazuje na surovinu `"cooper"` místo `"copper"`, chceš spadnout hned se srozumitelnou hláškou, ne až za hodinu hraní. Po načtení projdi všechny odkazy a ověř, že cíle existují.
- **ID jako řetězce, stabilní.** Jakmile je `"smelter"` v savech a modech, nepřejmenovávej ho. Jméno pro hráče (`name`) měň klidně, `id` drž stabilní.
- **Verze schématu.** Přidej do souborů `"schemaVersion"`, ať poznáš starý formát a umíš ho migrovat.
- **Nepiš logiku do dat.** Když začneš do JSON cpát podmínky a vzorce, je čas přesunout to do kódu (nebo skriptů). JSON má popisovat *co*, ne *jak*.

---

## 9. Doporučená struktura složek

```
/data
  buildings.json
  resources.json
  vehicles.json
  jobs.json
  biomes.json
  megastructures.json
  decorations.json      ← props: kaktusy, kameny, ruiny (živá mapa)
  fauna.json            ← zvířata a ptáci (živá mapa)
  weather.json          ← počasí (živá mapa)
  sounds.json           ← SFX a hudba (odkazované odjinud)
  /schema
    schema-version.txt
/content
  /atlas
    buildings.png + buildings.atlas
    terrain.png + terrain.atlas
    ...
  /audio
    ...
```

---

## 10. Rozšíření schématu: živá mapa a zvuky

Ano — nový obsah = **nové definiční soubory**, a přesně proto je data-driven přístup výhra: rozšíření struktury je jen přidání dalšího JSON typu, žádný zásah do jádra. Tady jsou nové typy pro živou mapu (viz dokument **Living Map**) a centralizovaný zvuk.

### 10.1 Dekorace / prop (kaktus, kámen, ruina)

```json
{
  "id": "cactus_tall",
  "name": "Vysoký kaktus",
  "biomes": ["desert"],
  "sprites": ["deco/cactus_1.png", "deco/cactus_2.png", "deco/cactus_3.png"],
  "placement": { "density": 0.15, "clustering": "sparse" },
  "variation": { "scale": [0.9, 1.2], "flipX": true, "tint": 0.05 },
  "isLandmark": false,
  "harvestable": null
}
```

- `sprites` = **víc variant** → střídají se náhodně (anti-repetice, viz Living Map sekce 6).
- `variation` = jemná odchylka měřítka / překlopení / tintu na instanci → stejný prvek nevypadá dvakrát stejně.
- `harvestable` = buď `null` (jen dekorace), nebo odkaz na surovinu (např. strom v džungli → dřevo).
- `isLandmark: true` u vzácných výrazných prvků (gejzír, kaňon, obří strom).

### 10.2 Fauna (zvíře / pták)

```json
{
  "id": "deer",
  "name": "Jelen",
  "biomes": ["grassland", "forest"],
  "sprite": "fauna/deer.png",
  "behavior": "wander_flee",
  "timeOfDay": ["day", "dusk"],
  "spawnDensity": 0.04,
  "huntable": { "produces": { "meat": 2, "hide": 1 } }
}
```

- `behavior` = jednoduchý vzor (`flock`, `wander`, `wander_flee`).
- `timeOfDay` = kdy se objevuje (napojení na den/noc).
- `huntable` = `null` (kulisa) nebo výnos (napojení na job „lovec").

### 10.3 Počasí

```json
{
  "id": "sandstorm",
  "name": "Písečná bouře",
  "biomes": ["desert"],
  "visual": "weather/sandstorm.png",
  "sound": "sfx_sandstorm",
  "severity": "extreme",
  "eventEffect": "reduce_flow_outdoor",
  "frequency": 0.02
}
```

- `severity`: `ambient` (jen atmosféra) nebo `extreme` (napojení na **event**, dočasně sníží flow).
- `eventEffect` = behavior-ID (viz sekce 6) — samotný efekt je kód.

### 10.4 Zvuk / SFX (centralizovaný)

Zvuky mají **vlastní registr** a ostatní definice na ně jen odkazují přes ID — jeden zdroj pravdy pro audio (klíčové, protože zvuk je půlka pocitu, viz Game Feel).

```json
{
  "id": "sfx_chop",
  "file": "audio/chop.wav",
  "category": "action",
  "pitchRange": [0.92, 1.08],
  "loop": false
}
```

- `pitchRange` = náhodné kolísání výšky → zvuk se neomrzí ani po tisící (viz Game Feel sekce 1).
- Odkazuje se odjinud: `building.sound`, `action.sound`, `weather.sound = "sfx_sandstorm"`.

### 10.5 Rozšíření biomu

Biom z sekce 3.5 se doplní o odkazy na nový obsah — biom se stává „balíčkem identity":

```json
{
  "id": "desert",
  "name": "Poušť",
  "tileSprites": ["terrain/sand_1.png", "terrain/sand_2.png"],
  "resources": ["oil", "stone"],
  "modifiers": { "farmingYield": 0.05, "waterYield": 0.1 },
  "decorations": ["cactus_tall", "rock_mesa", "bones", "canyon"],
  "fauna": ["lizard", "vulture", "camel"],
  "weather": ["heat_shimmer", "sandstorm"],
  "ambientSounds": ["sfx_desert_wind"],
  "palette": "warm_arid",
  "resourceRichness": { "oil": "abundant", "wood": "none", "food": "scarce" }
}
```

- `decorations` / `fauna` / `weather` / `ambientSounds` = seznam ID → biom si „naskládá" svou identitu z existujících definic.
- `resourceRichness` = ekonomická identita (viz Living Map sekce 5) — džungle by měla `"wood": "abundant"`, poušť `"oil": "abundant"`.
- `palette` = odkaz na barevné ladění (podpora den/noc overlaye).

> **To je celá odpověď na „bude potřeba rozšířit strukturu?":** ano, ale rozšíření = přidat pár nových JSON typů a doplnit odkazy do biomu. Jádro kódu se nemění — přesně kvůli tomu jsme šli data-driven cestou.

---

## 11. Shrnutí

- **Lze to? Ano** — a je to doporučený způsob, jak stavět hru s hodně obsahem.
- **JSON + `System.Text.Json`** = žádná závislost navíc, sedí na „no balast".
- **Definice v JSON, instance v data-oriented polích** — data-driven a data-oriented se doplňují.
- **Sprity odkazem, ne binárkou.**
- **Data = co, kód = jak.** Unikátní chování přes behavior-ID hook.
- **Nový obsah = nový JSON typ**, ne zásah do jádra (dekorace, fauna, počasí, zvuky — viz sekce 10).
- Bonusy zdarma: hot-reload, snadná balance, cesta k moddování a Workshopu.

---

*Návrh k iteraci. Schémata výše jsou ukázková — finální pole si uprav podle toho, jak se vyvine design.*
