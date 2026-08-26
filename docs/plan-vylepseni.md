# CivDle — postup řešení návrhů z `potential_upgrades.md`

Ke každému bodu původního dokumentu: **co už stojí**, **jak to udělat**, **co do dat a co do testů**, **odhad a riziko**.

Tři věci předem:

1. **Odhady platí pro plnou větev** (tu z bundle, ~95 commitů), ne pro strom bez trailer/demo/Odkaz práce.
2. **Steam App ID: 5045220.** Body 7.1, 6.4, žebříčky, achievementy a haptika Decku visí na **jedné** integraci Steamworks.NET, ne na pěti. Viz §7.0.
3. **Jednotka odhadu je „člověkoden soustředěné práce"**, ne kalendářní den.

---

## 1. Výkon

### 1.1 Prostorové indexování

**Stav:** dotazy na dlaždici jsou už teď O(1) (`_occupancy`, `_roads` jako `Dictionary<long,…>`). Problém, který dokument popisuje, v simulaci není. **Je ale v renderu:** `BuildingRenderer.Draw` projede `simulation.Buildings` od nuly do konce každý snímek a culluje po jedné budově.

**Postup:** index drží simulace (ví o přidání, zboření i přesunu), render z něj jen čte.

1. `BuildingIndex` v `CivDle.Core/Sim/` — `Dictionary<long, List<int>>` klíčovaný chunkem 32×32 přes `TileKey.Pack(x >> 5, y >> 5)`.
2. Napojit na tři místa, kudy prochází každá změna zástavby: `AddBuilding`, `TryDemolish`, `TryMoveBuilding`. Nikde jinde ne — jinak index tiše zestárne.
3. `Simulation.BuildingsIn(minX, minY, maxX, maxY, List<int> results)` plní **předaný** buffer (žádná alokace za snímek).
4. Renderer si drží jeden `List<int>` a volá to z `camera.VisibleWorldBounds()`.

**Testy:** po náhodné sérii stavba/bourání/přesun musí index vrátit **přesně totéž** co hrubé projití pole. To je ten test, který chytí zapomenutou aktualizaci.

**Odhad:** 1,5 dne. **Riziko:** nízké. **Ale:** napřed pusť `--perf` a podívej se, jestli je render budov opravdu ta drahá část. Máš na to nástroj, není důvod hádat.

> **Hotovo.** `BuildingIndex` (chunky 32×32) drží simulace a plní ho tři
> místa, kudy prochází každá změna zástavby — stavba, odebrání a přesun.
> Klíčové bylo čtvrté: **swap-remove z plochého pole přečísluje poslední
> budovu**, a kdyby se to index nedozvěděl, ukazoval by na budovu, která tam
> už není. To hlídá test, který po čtyřech stech náhodných úpravách porovnává
> index s hrubým projitím pole.
>
> Čte z něj **čtvero kreslení**, ne jen budovy: `BuildingRenderer`,
> `LightsRenderer` (noční zář), `StallOverlayRenderer` (inspektor) a
> `FrontierRenderer` (poškození). Všechny čtyři projížděly celé město každý
> snímek. `CityScaleRenderer` a minimapa zůstávají u celého pole schválně —
> ty všechny budovy opravdu potřebují.

### 1.2 Vícevláknové úlohy na pozadí

**Pozor — návrh v dokumentu rozbíjí determinismus.** Píše se tam „jakmile je výpočet hotov, v nejbližším tiku se výsledek přehodí". Jenže „nejbližší tik" závisí na rychlosti stroje, takže tentýž seed dá na dvou počítačích jiný svět. Tím padá reprodukovatelnost savů, časosběru i testů — a to je jedno z mála pravidel, na kterých ta hra stojí.

**Postup:** úloha se zadá v tiku *N* a výsledek se použije **v pevně daném tiku** *N + K* (K je konstanta v datech, ne „až to doběhne"). Když do té doby nedoběhne, hlavní vlákno si to dopočítá samo.

1. `DeferredJob<T>` s polem `AppliesAtTick`.
2. Vstup je **kopie** (snapshot), ne živý stav — jinak vlákno čte pole, do kterého se zapisuje.
3. Kandidáti: A* silnic k NPC městům, difúze znečištění, detekce distriktů. Časosběr ne — ten běží jen při ukládání snímku.

**Testy:** tentýž seed a tentýž počet tiků musí dát bit po bitu tentýž stav se zapnutými i vypnutými vlákny.

**Odhad:** 3 dny. **Riziko:** vysoké. Doporučuji **odložit**, dokud `--perf` neukáže, že ty systémy opravdu žerou tik.

### 1.3 Render: LOD, culling, atlasy

**Stav:** LOD i culling existují, prahy jsou v `DetailLevel`.

**Postup:** ze tří návrhů má smysl jeden a půl.

* **Hustotní mapa při velkém oddálení** — skutečný posun. Upeč zastavěnost do textury chunku po vzoru `TerrainRenderer` (1 texel = 1 dlaždice, barva podle převažující kategorie), překresluj líně a jen když se zástavba změní. Jeden draw call na chunk místo tisíců. Sedne na 1.1.
* **Atlas** — sprity se generují procedurálně při startu do samostatných textur; sbalit je do jedné 1024×1024 při startu je mechanická práce a ušetří přepínání textur. Udělej **až** po hustotní mapě, přínos bude menší.
* **Frustum culling** — hotové, není co dělat.

**Odhad:** hustotní mapa 2 dny, atlas 1 den. **Riziko:** nízké.

### 1.4 Zero-allocation

**Postup:** neřeš to plošně, měř.

1. Do `PerfRun` přidej `GC.GetAllocatedBytesForCurrentThread()` před a po měřeném úseku a vypiš **bajty na snímek** a **bajty na tik**.
2. Teprve podle čísel: `Numbers.Format` na `ISpanFormattable` (formátování velkých čísel běží v HUDu 60×/s pro každou surovinu), pracovní buffery místo `new List<>` v tikových systémech, pooling částic.

**Testy:** test, který odtiká 1000 tiků a tvrdí, že alokace nepřekročí strop. Ten drží zisk i za rok.

**Odhad:** 0,5 dne měření + 1,5 dne oprav. **Riziko:** nízké, přínos přesně tak velký, jak ukáže měření.

> **Hotovo — a měření řeklo „není co opravovat".** `--perf` teď vypisuje
> bajty na snímek i vlastní tabulku pro tik. Čísla:
>
> * **tik: 426 B** při 900 budovách — a **neroste s městem** (malé i velké
>   město alokují stejně, což je ta jediná věc, na které záleží).
> * **`Numbers.Format`: 54 B na volání**, tedy 63 kB/s při dvaceti surovinách
>   a šedesáti snímcích. Přepis na `ISpanFormattable` by ušetřil odpad, který
>   GC ani nezaznamená.
>
> Takže žádné „opravy podle čísel" — plán počítal s 1,5 dnem práce, která by
> nic nepřinesla. Zůstávají tři testy, které to hlídají do budoucna: strop
> alokací na tik, „alokace neroste s městem" a strop pro HUD. Tohle je ten
> zisk, ne mikrooptimalizace.

---

## 2. Herní obsah

### 2.1 Expedice, anomálie, zlatá fauna, konjunktury

Čtyři různě velké věci, dokument je slévá do jedné.

**Zlatá fauna** — `GoldenSpawnSystem.cs` existuje: občas se zatřpytí objekt, klik dá surovinu. Posun je v obsahu, ne v mechanice.
*Postup:* `data/golden.json` s typy (id, sprite, doba života, dráha pohybu, zvuk, odměna). Odměna dostane vedle „balík suroviny" i „dočasný násobič" — ten už umí slavnost, jen se na to napojit. **0,5 dne.**

**Anomálie a expedice** — místa na mapě existují (landmarky, speciální objekty). Nové je **okno s cenou a odměnou**.
*Postup:* (1) `data/poi.json` — id, biom, minimální vzdálenost od města, cena, doba, tabulka odměn. (2) `PointOfInterestSystem` v Core generuje deterministicky ze seedu, **nedrží žádný vlastní seznam** — pozice se dopočítá z hashe, do savu jde jen „které už jsou vybrané". (3) Běžící expedice je stav v simulaci a **musí do savu**. (4) Relikvie jsou trvalý modifikátor — napoj je na `RecomputeBonuses`, ať se skládají stejně jako prestižní vylepšení, ne vedle nich.
*Testy:* tentýž seed dá tatáž místa; expedice přežije uložení a načtení; relikvie se projeví v násobiči.
**2,5 dne.** Riziko: střední (nový stav v savu).

**Tržní konjunktury** — NPC města už obchodují, tohle je časově omezená cena.
*Postup:* pole `demandSpike` do `npc-cities.json` + hláška do notifikací. **0,5 dne.**

### 2.2 Řetězce, energetika, železnice

**Energetika** je hotová jako **jedno globální číslo** — `sim.PowerFactor` násobí `ProductionSystem`. Posun je v prostoru a je to dobrý posun: udělá z toho rozhodnutí „kam".

*Postup:*
1. `PowerGridSystem` v Core na **hrubé mřížce 8×8 dlaždic**, přepočet na nízké frekvenci (CLAUDE.md: růstové systémy nejedou každý tik).
2. Elektrárna zaplní svou buňku a šíří se do sousedních, dokud stačí výkon. Žádné kreslení drátů — dosah je poloměr z dat. Ušetří to hráči mikromanagement a tobě pathfinding.
3. `Simulation.PowerAt(x, y)` vrací 0…1, `ProductionSystem` použije místo globálu.
4. **Bonusy za biom nepiš znovu** — `AdjacencyRule` v `BuildingDef` už umí „bonus podle okolí". Větrná turbína v horách a solár na poušti je záznam v datech, ne kód.

*Testy:* budova v dosahu má 1.0, mimo 0.0; při nedostatku výkonu klesnou všechny poměrně (ne že první tři dostanou a zbytek nic).
**2,5 dne.** Riziko: střední — mění se křivka ekonomiky, chce projet `--perf` i balanční nástroj.

> **Hotovo.** `PowerGridSystem` na mřížce 8×8, přepočet líný (až se zeptá
> výroba poté, co se změnila zástavba). Elektrárna se rozlévá záplavou do
> dosahu z dat a **rozděluje výkon poměrně podle poptávky** — díky tomu je
> výsledek nezávislý na pořadí elektráren a při nedostatku klesnou všichni
> stejně, místo aby dvě továrny jely naplno a třetí o dlaždici dál vůbec.
>
> Bez bloku `power` v datech zůstane staré globální číslo, takže mody
> a starší obsah dostanou přesně tu hru, jakou měly.
>
> Přibyl **pohled na pokrytí** (klávesa **E**, sám se rozsvítí, když má hráč
> v ruce elektrárnu nebo budovu na proud). Bez něj je dosah pravidlo, které
> se nedá odhadnout — továrna by jen tiše jela na třetinu a hráč by hledal
> důvod v surovinách.

**Hlubší řetězce** (mouka→chléb, chmel→pivo, ruda→ocel→stroje) jsou **čistě data**: `resources.json` + `buildings.json` + lokalizace do pěti jazyků + ikony. Kód nula.
**1 den na řetězec**, dominantní náklad jsou ikony a překlady.

**Železnice** je samostatná dopravní vrstva — vlastní graf, vlastní entity, vlastní vykreslování. **5+ dní**, nepatří do stejné položky jako pekárna. Odlož.

### 2.3 Fázové megastruktury

**Stav:** divy se staví v čase (`BuildTicks`, `TakesTimeToBuild`), rozestavěná budova má progres.

**Postup:** `stages` do definice budovy — pole `{ atProgress, sprite }`. `BuildingRenderer` vybere sprite podle `progress`, což už má po ruce. Fail-fast při načtení: fáze musí být vzestupné a poslední musí být 1.0.

**Testy:** výběr fáze pro progres 0 / 0,49 / 0,5 / 1,0; validace rozbitých dat.

**Odhad:** 1 den kódu + kresba spritů (to je ta skutečná práce). **Riziko:** nízké.

### 2.4 Civilizační doktríny

Největší položka celé sekce 2 a nejvíc mění hru.

*Postup:*
1. `data/doctrines.json` — id, ikona, uzly, ceny v bodech Vzestupu, efekty.
2. Efekty **výhradně přes existující mechanismus modifikátorů** (`RecomputeBonuses` + behavior-ID hooky na unikátní věci). Kdyby doktríny dostaly vlastní cestu, budeš mít dvě soustavy násobičů, které se rozejdou.
3. Volba doktríny je stav Vzestupu → do savu, do `RunSummaryScreen` a do kroniky.
4. UI: strom se dá vykreslit `TechGraphLayout`, který už máš.

*Testy:* dvě doktríny se nesčítají; efekt se projeví hned po výběru; save/load; a hlavně **balanční test**, že žádná doktrína nedává víc než 2× oproti ostatním.

**Odhad:** 4 dny. **Riziko:** vysoké — je to zásah do ekonomiky. Až po dema.

---

## 3. Audiovizuál

### 3.1 Mikro-animace obyvatel

**Stav:** `AgentSystem.cs` existuje, chodci se pohybují.

*Postup:* rozšířit o druhy (`Fisherman`, `BenchSitter`, `UmbrellaWalker`, `WorkerCart`) — každý je stav + kotva (molo, lavička, pila). Podmínka z CLAUDE.md: **pooling, nulová alokace**, agenti se vytvářejí jen v zorném poli a při oddálení se vracejí do poolu.

*Testy:* při 60 s běhu s posouváním kamery nesmí počet agentů růst (chytí zapomenuté vracení do poolu).

**Odhad:** 2,5 dne + sprity. **Riziko:** nízké. **Nejlepší poměr wow/práce v celé sekci 3.**

### 3.2 Sníh, listí, vítr

**Stav:** roční období existují jako barevný nádech scény.

*Postup:* (1) `BuildingRenderer` nanese v zimě bílou masku na horní pruh spritu (levné, není potřeba druhá sada spritů). (2) `DecorationRenderer` moduluje barvu stromů podle období — už tam ta modulace je pro prosperitu, přidá se druhý vstup. (3) Kouř: vektor větru posune každý díl obláčku podle jeho stáří.

**Odhad:** 1,5 dne. **Riziko:** nízké.

### 3.3 Tilt-shift

**V projektu není jediný shader ani content pipeline.** Zavést `.fx` znamená MGCB do buildu a musí to fungovat na DirectX (Windows) i OpenGL (zbytek) — viz `UseDirectX` v csproj. To je proti „no balast" a proti dvěma dnům z matice.

*Návrh bez shaderu:* scéna už se kreslí do `RenderTarget2D` (fotorežim to dělá). Rozostření se dá udělat čistě `SpriteBatch`em: zmenšit RT na 1/4 s lineárním filtrem, natáhnout zpátky (to **je** rozostření) a smíchat s ostrým obrazem přes svislou masku alfy. Kontrast a saturaci uděláš barevným překryvem, což `DayNightCycle` už umí.

Vypadá to o chlup hůř než gaussovské rozostření a nikdo to nepozná. **1 den, nula nových závislostí.** K pravému shaderu se dá vrátit, až budeš shadery chtít i na něco jiného.

### 3.4 Prostorový zvuk

*Postup:* `GameSounds` už generuje vzorky procedurálně. Přidej `SpatialEmitter` — budova daného typu, poloměr slyšitelnosti, hlasitost podle vzdálenosti od středu kamery, panorama podle X. Strop na počet znějících zdrojů (např. 8 nejbližších), jinak se sto pil sečte v kaši.

*Data:* pole `sound` do `buildings.json` + `ambience.json` na éry.

**Odhad:** 2 dny. **Riziko:** nízké, ale chce sluchátka a trpělivost.

---

## 4. UI a komfort

### 4.1 Bilanční dashboard a inspektor úzkých hrdel

*Postup — tři kroky, každý použitelný sám o sobě:*

1. **Účtování toků.** `ProductionSystem` sečte za tik `vyrobeno[r]` a `spotřebováno[r]` do dvou předalokovaných polí. Do savu to nepatří, počítá se znovu.
2. **Tooltip u suroviny:** výroba / spotřeba / čistý tok + rozpad podle typu budovy. Grafy historie už máš, jen se na ně proklikne.
3. **Inspektor (klávesa B):** budovy už vědí, proč stojí (chybí vstup / plný sklad / chybí lidi) — je to overlay nad `BuildingRenderer`, ne nová logika.

*Testy:* součet toků musí sedět se skutečnou změnou zásob za tik. To je ten test, který chytí, že někde surovina mizí.

**Odhad:** 2 dny za všechny tři. **Riziko:** nízké. **Nejvyšší přínos v celém dokumentu.**

### 4.2 Undo/Redo a hromadné vylepšení

**Undo je v simulaci zrádné.** Postavení domu zároveň dotáhne silnice, odhalí mlhu, přepočítá sousedské bonusy, zapíše do statistik a může spustit milník. „Vrátit poslední akci" tedy není jedna operace.

*Doporučený rozsah:* zásobník posledních 20 **hráčových** akcí (stavba, bourání, silnice, zóna). Každá má inverzi a **kontrolu, že se pod ní svět nezměnil** — když tam mezitím auto-stavba něco postavila, undo se odmítne s hláškou. Poctivé, srozumitelné, a hlavně to nerozbije stav.
**2 dny.** Plný command-log s reverzibilní simulací je jiná liga (týdny) a nedoporučuji ho.

*Hromadné vylepšení distriktu* je proti tomu snadné: `TryUpgradeBuilding` už existuje, jde jen o výběr podle distriktu, spočtení ceny a jedno potvrzení. **0,5 dne.**

### 4.3 Hledání a filtry

**149 technologií a 94 budov, a ve `TechScreen` není hledání.** U hvězdicového stromu to není komfort, to je nutnost.

*Postup:* `TextBox` z Myry, filtr přes lokalizovaný název + id + id odemykaných budov a surovin. Ve stromu neshodné uzly ztlumit (ne skrýt — jinak zmizí souvislosti), ve stavebním menu vyfiltrovat.

**Odhad:** 0,5 dne. **Riziko:** žádné. **Udělej to jako první.**

### 4.4 Přehled impéria

**Stav:** `SettlementsScreen` existuje.

*Postup:* tabulka (populace, štěstí, hlavní výroba, guvernér) + klik = kamera. **Pozor na jeden důsledek:** `GovernorPlan` je dnes **jeden pro celou říši**. „Město A těžba, město B zemědělství" znamená přesunout plán do stavu sídla, tedy i do savu a do UI guvernéra.

**Odhad:** 1 den tabulka, +1,5 dne plán na sídlo. **Riziko:** střední (mění se save).

---

## 5. Herní režimy

### 5.1 Scénáře

Nejlevnější z trojice, protože přebíjení pravidel už existuje: `content.WithGameplay(...)` umí vyrobit variantu obsahu se změněnými čísly.

*Postup:* `data/scenarios.json` — název, seed, přebití gameplay hodnot, startovní zásoby, **cíl a prohra**. Cíle napiš přes systém úkolů, který máš; nedělej druhý.

*Zvláštnosti podle scénáře:* „mráz" a „voda jen do 10 polí od zdroje" jsou pravidla navíc → behavior-ID hook, ne `if` ve scénáři.

**Odhad:** 2 dny rámec + 1 den na scénář. **Riziko:** nízké.

### 5.2 Frontier Defense — jako režim, ne jako jiná hra

Původně jsem ho psal ven z CivDle. Chceš ho uvnitř, tak ho plánuju uvnitř — s jednou podmínkou, na které stojí, jestli to hru poškodí: **je to volitelný režim, ne nové pravidlo pro všechny.** Zapíná se při zakládání světa (totéž místo jako sandbox, 5.3) a kdo ho nezapne, o boji se nedozví. Relaxační jádro tím zůstane relaxační.

Co je potřeba postavit:

1. **Vlny z dat** — `data/frontier.json`: v kterém tiku, z které strany, co přijde a jak silné to je. Deterministický rozvrh, žádná náhoda bez seedu — jinak se rozejde save i test.
2. **Útočníci jako plochá data** — `struct[]` v Core (pozice, cíl, zdraví, typ). Jsou to stovky entit v tikové smyčce, tedy přesně ta vrstva, kde CLAUDE.md říká „ploché pole, ne strom objektů".
3. **Cesta k městu bez A\*** — jedno **spádové pole** na hrubé mřížce 8×8 k těžišti města, přepočet jen při změně zástavby. Je to tatáž mřížka jako u energetiky (2.2a); postav ji jednou a použij dvakrát.
4. **Obranné budovy jsou normální budovy** — blok `defense` v `buildings.json` (dosah, poškození za tik, provoz). Stavějí se, vylepšují a stojí údržbu jako všechno ostatní. Kód navíc je jenom míření.
5. **Zásah město nezničí** — budova se poškodí a na N tiků vypadne z výroby, pak se opraví. Trvalá ztráta postupu je v idle hře trest za to, že šel hráč spát.
6. **Do savu** jde stav vln a poškození, ne jednotliví útočníci uprostřed pole — ti se dopočítají z rozvrhu.

*Testy:* tentýž seed a tentýž počet tiků dá tutéž vlnu i tytéž ztráty; věž střílí na nejbližšího a při shodě podle indexu (ne podle pořadí ve slovníku, to by rozbilo determinismus); **vypnutý režim = nulový dopad na délku tiku** — změřit, ne tvrdit.

**Odhad:** 6 dnů. **Riziko:** vysoké — jediná položka celého plánu, která přidává novou entitu do tikové smyčky. Proto je **poslední**: napřed to, co prodává demo.

> **Hotovo.** Jedna věc vyšla jinak, a líp: **spádové pole nebylo potřeba**.
> Útočník jde přímo k těžišti města, a když mu v cestě stojí budova, pustí se
> do ní. Není to zjednodušení, je to ta mechanika — hradba z budov opravdu
> zdrží, protože se musí prokousat. Ušetřilo to celý bod 3 i mřížku, kterou
> měla obrana sdílet s energetikou.
>
> Zásah budovu nezničí, jen ji na chvíli vyřadí z výroby (a poškození se
> nesčítá donekonečna — strop je jedna oprava navíc). Inspektor úzkých hrdel
> dostal šestou barvu, takže se poškozená budova pozná i v přehledu.
>
> Podmínku „vypnutý režim = nulový dopad" hlídají dva testy: nejen že se nikdo
> neobjeví, ale ani jedna věž se za tři vlny nenabije. Kdyby systém tikal
> naprázdno, platil by za něj i ten, kdo si režim nezapnul.

### 5.3 Sandbox

Skoro hotové: `CheatMode` (neomezené suroviny, guvernér na maximum) existuje.

*Postup:* přepínač při zakládání světa → příznak v savu → **žádné achievementy a žádné žebříčky** v takové hře. Ten příznak je celá pointa: bez něj si někdo omylem zkazí postup.

**Odhad:** 0,5 dne. **Riziko:** nízké.

---

## 6. Inovace

### 6.1 Významné osobnosti

*Postup:* `data/figures.json` (id, obor, efekt, doba života). Vznik na milnících — ty už existují. Osobnost je stav v savu, efekt přes `RecomputeBonuses`. Po dožití vzniká socha = normální budova s pasivním bonusem, takže žádná nová mechanika.

*Testy:* efekt zmizí s dožitím a socha ho nahradí (ne že se sečtou).

**Odhad:** 2,5 dne. **Riziko:** nízké, přínos vysoký — dává městu příběh.

### 6.2 Říční proudy a plavení dřeva

*Postup:* (1) řeky mají směr už při generování — uložit ho na dlaždici (2 bity), ne dopočítávat. (2) Kláda je entita jako vozidlo, plave po směru. (3) Česle u pily je budova, která je vybírá.

*Riziko:* co s kládou, která doplave na konec řeky nebo do jezera — potřebuje pravidlo, jinak se hromadí donekonečna a s ní i paměť.

**Odhad:** 3 dny. **Riziko:** střední. Krásné, ale až po důležitějším.

### 6.3 Ilustrovaná kronika

**Stav:** `HistorySystem` existuje a události sbírá — tohle je hlavně prezentace.

*Postup:* šablony vět **do dat** (`data/chronicle.json`), ne do kódu, a se skloňováním pro každý jazyk. Vykreslení do `RenderTarget2D` a uložení jako PNG — přesně to, co dělá fotorežim, dá se to použít.

**Odhad:** 2,5 dne. **Riziko:** nízké. Skvělé před Vzestupem: hráč má co ukázat.

### 6.4 Karavany přátel

**Jde to a je to levné** — když zůstane u jmen a obrázků.

*Bez serveru:* `ISteamFriends.GetFriendCount`, `GetFriendByIndex`, `GetFriendPersonaName`, `GetMediumFriendAvatar`. Avatar přijde jako pole RGBA → `Texture2D`. Karavana se jménem a obličejem kamaráda je odpoledne práce.

*Co bez serveru nejde:* „veze to, co jeho město produkuje nejvíc" a „ruiny hráče, který tam provedl Vzestup" — to jsou data o cizí rozehrané hře.

*Střední cesta bez serveru:* žebříček ve Steamu umí ke každému záznamu uložit malé pole `int` (`details`) a stáhnout **jen přátele**. Propašuj tam éru, řádovou populaci a nejvíc produkovanou surovinu — karavana pak veze něco, co s jeho hrou opravdu souvisí.

*Podmínka:* bez běžícího Steamu se to musí tiše přeskočit, ne spadnout.

**Odhad:** 1 den (jména a avatary) + 1 den (žebříčkový trik). Předpokládá §7.0.

### 6.5 Podmoří

**Je to levnější, než jsem psal, a mýlil jsem se v tom hlavním.** Není to druhá mapa. Vodní biomy jsou v datech i s hloubkou (`coral_reef`, `shallow_water`, `ocean`, `deep_ocean`, `pack_ice`), generátor je umí — a `CanPlace` na vodu **vůbec nesahá**: kontroluje jen `allowedBiomes`. Budova, která má v datech povolené moře, se na moře dá postavit už dnes.

Co doopravdy chybí:

1. **Obsah** — `buildings.json`: řasová farma, sádky, těžní věž nad průduchem, obytný dóm. K tomu suroviny do `resources.json` a názvy do pěti jazyků. Tohle je většina práce a je to práce v datech, ne v kódu.
2. **Pravidlo dosahu.** Bez něj si hráč postaví dóm uprostřed oceánu na druhé polokouli. Podmořská stavba musí být **v dosahu přístavu** nebo jiné podmořské stavby — tedy síť, která se šíří po vodě stejným způsobem jako energetika (2.2a). Jedna mřížka, dva systémy.
3. **Svoz od přístavu, ne od centra.** `HaulSystem` měří vzdálenost k těžišti města; přes moře to nedává smysl. Podmořská budova počítá svoz od nejbližšího přístavu v síti.
4. **Pohled pod hladinu** — přepínač, který ztlumí hladinu, vykreslí dno podle hloubky (odstín z hodnoty, kterou už generátor má) a rozsvítí postavené struktury. Bez toho jsou to domečky plovoucí na modré.
5. **Odemčení technologií** v pozdní éře, ne od začátku.

*Testy:* stavba mimo dosah přístavu se odmítne s vlastní hláškou; síť se šíří jen po vodě a nepřeskočí pevninu; svoz se počítá od přístavu; síť přežije uložení a načtení.

**Odhad:** 4 dny (obsah 1, síť 1,5, pohled 1, odemčení a hlášky 0,5). **Riziko:** střední — nové je jen šíření sítě, zbytek jsou data.

> **Hotovo.** Bod 3 (svoz) nakonec nestál nic: `HaulSystem` bere za sběrné
> místo každou budovu se skladem a přístav sklad má, takže podmořská stavba
> u přístavu má dobrý svoz sama od sebe. Naopak přibyla věc, kterou plán
> nečekal — trest za nenapojení na silnici. Na dno silnice nevede a nikdy
> nepovede, takže by celá vrstva natrvalo jela na šedesát procent; podmořské
> budovy jsou z toho pravidla ven.
>
> Pohled pod hladinu vyšel jinak, než plán čekal, a líp: místo ztmavování
> hladiny se kreslí **dosah sítě** — nádech a obrys nad vodou, kam přístav
> dosáhne. Sám se rozsvítí, když má hráč v ruce budovu na dno, a klávesou **M**
> se dá zapnout natrvalo. Dno podle hloubky se dá dodělat, ale tohle je ta
> informace, kterou hráč doopravdy potřebuje.

### 6.6 Orbita

Není to jiná mapa, je to **jiný pohled na tutéž** — a hlavně jiný druh stavby: družice se nestaví na dlaždici, **vypouští se**.

1. **Kosmodrom** je normální budova: drahá, pozdní, velký půdorys. Bez něj se nedá vypustit nic.
2. **`data/orbit.json`** — družice: id, cena, doba výroby, globální efekt, sprite, výška dráhy, rychlost. Efekty **výhradně přes `RecomputeBonuses`**, ať se skládají se vším ostatním: solární zrcadlo výrobu, meteorologická mírní počasí, komunikační zrychluje výzkum, průzkumná odhaluje mlhu.
3. **Stav v savu je pole `int`** — kolik kterých družic je nahoře a co se zrovna staví. Nic víc: poloha na dráze je funkce tiku, ta se nepočítá a neukládá.
4. **Orbitální obrazovka** — planeta jako kotouč (barvy z minimapy, tu už máš), družice po elipsách, klik na družici ukáže její efekt.
5. **Odemčení** až vesmírnou érou.

*Testy:* efekt družice se projeví v násobiči a po demontáži zmizí; poloha na dráze je funkcí tiku (tentýž tik = tentýž obrázek); počet družic přežije save/load.

> **Hotovo.** Pět družic v `data/orbit.json`, kosmodrom už v datech byl.
> Efekty jdou tímtéž slovníkem jako Vzestup a Velké dílo, takže se skládají
> se vším ostatním a nevznikla druhá soustava násobičů. Do savu jde pole
> `int` a jeden rozestavěný start, nic víc.
>
> Dvě věci vyšly jinak, než plán čekal. **Jeden start naráz** — ne kvůli
> výkonu, ale aby vypuštění zůstalo událost; deset družic ve frontě je
> nákupní seznam. A **cena každé další roste geometricky**, takže i strop
> `maxCount` má před sebou brzdu: šestá družice stojí desetkrát víc než první
> a hráč se sám rozhodne, kdy přestat.

### 6.7 Zvonohra a festivaly

Festivaly existují (boost + efekt). Chybí ta vizuální slavnost: stánky, lampiony, tanec.

Zvonohra je lepší nápad, než vypadá: `GameSounds` **generuje tóny procedurálně**, takže osm tónů a jednoduchý notový editor nepotřebuje ani jeden zvukový soubor. Melodie se uloží do savu jako osm čísel.

**Odhad:** zvonohra 1,5 dne, vizuál festivalu 1 den. **Riziko:** nízké. Milá, sdílitelná drobnost.

---

## 7. Steam

### 7.0 Jedna integrace, ne pět — App ID 5045220

Workshop, karavany přátel, žebříčky, achievementy i haptika Decku sedí na jedné knihovně. Udělej to **jednou a pořádně**:

1. `Steamworks.NET` do `CivDle.csproj` (jediná nová závislost, kterou tenhle plán přidává — s jasným zdůvodněním, jak žádá CLAUDE.md).
2. `steam_appid.txt` s `5045220` vedle binárky **jen pro vývoj** — do distribuce nepatří.
3. `SteamPlatformServices` jako druhá implementace vedle `LocalPlatformServices`. Rozhraní už existuje, právě kvůli tomuhle.
4. **Když Steam neběží, hra běží dál** na lokální implementaci. Ověř to testem, ne pohledem.
5. Demoverze má na Steamu **vlastní App ID** — Workshop, žebříčky ani achievementy do ní nepatří (což už `Edition.IsDemo` řeší).

**Odhad:** 2 dny. Odemyká 7.1, 7.2 i 6.4.

### 7.1 Workshop

*Postup:* `SteamUGC.CreateItem` → `StartItemUpdate` → nastavit složku, náhled, popis a značky → `SubmitItemUpdate`. Odběry se stahují samy do složky, kterou vrátí `GetItemInstallInfo` — tu jen přidej k dnešnímu prohledávání `mods/`.

*Na co narazíš:* nahrávání je asynchronní s postupem (chce ukazatel, ne zamrzlé okno), první publikace vyžaduje odsouhlasení pravidel v prohlížeči a náhledový obrázek má limit 1 MB.

**Odhad:** 2,5 dne po §7.0. **Riziko:** střední — testovat se to dá až proti živému Steamu.

### 7.2 Steam Deck

Gamepad a `GamePadMap` existují. Zbývá: rozložení pro 1280×800 (Myra škáluje, ale písmo je potřeba zvětšit), **radiální menu** na levý trigger (kruh položek, výběr páčkou — jedna obrazovka, žádná nová logika) a haptika přes `ISteamInput`.

**Odhad:** 2 dny. **Riziko:** nízké. Bez fyzického Decku to ale neodladíš do konce.

### 7.3 Sdílení šablon přes schránku

Šablony existují. Kód = `Deflate` + Base64 nad JSONem šablony, s krátkou hlavičkou a **verzí formátu** (aby budoucí hra poznala starý kód a řekla to slušně).

*Jediný zádrhel:* MonoGame nemá schránku. DesktopGL má pod sebou SDL (`SDL_SetClipboardText`), na Windows je `System.Windows.Forms`. Nejmenší zlo je tenká platformní vrstvička, protože hra už jednu má.

*Testy:* kolečko šablona → text → šablona musí dát tutéž šablonu; poškozený kód musí skončit hláškou, ne pádem.

**Odhad:** 1,5 dne. **Riziko:** nízké.

---

## 8. Doporučené pořadí

Setřídil jsem to podle toho, **co udělá z hráče dema kupce**, ne podle velikosti nápadu.

### Před demem — 6 dnů

> **Stav k dnešku: hotovo celé.** Všech sedm položek stojí a je otestovaných.
>
> Cestou se našly tři chyby mimo plán:
> * `--capture` nedojede sadu snímků do konce (patří k položce „index budov pro
>   render"). Původně to tu stálo jako „tiše umírá"; při měření se ukázalo, že
>   spíš **trvá neúnosně dlouho** — deset snímků, každý si napřed vypěstuje
>   čtrnáctiminutové město, a v kontejneru stojí jeden tik nad takovým městem
>   46 ms. Tři snímky z deseti za pětadvacet minut. Než tomu říkat pád, patří
>   sem měření: kolik z toho je růst města a kolik samotné kreslení.
> * hledání zpočátku prozrazovalo neodhalené technologie,
> * **osm budov ve hře nešlo postavit vůbec** — přístav, rybářství a šest
>   megastruktur měly `buildable: false` bez toho, aby na ně cokoli vylepšovalo.
>   Megastruktury přitom „odemykal" stupeň měřítka, což nikdy nemohlo zabrat.
>   Opraveno a pohlídané třemi testy dosažitelnosti.

| # | Co | Dny | Proč právě teď |
|---|---|---|---|
| 1 | Hledání ve stromu a stavebním menu (4.3) | 0,5 | 149 technologií bez hledání je bariéra hned v první hodině |
| 2 | Bilanční tooltip + inspektor hrdel (4.1) | 2 | Hráč přestane tápat, proč mu stojí město |
| 3 | Sníh, listí, ohýbaný kouř (3.2) | 1,5 | Nejlevnější „wow" na screenshoty |
| 4 | Hromadné vylepšení distriktu (4.2b) | 0,5 | Odstraní nejotravnější klikání |
| 5 | Sandbox jako režim (5.3) | 0,5 | Skoro hotové, dobře se o tom mluví |
| 6 | Zlatá fauna z dat (2.1a) | 0,5 | Rozhýbe střední hru za půl dne |
| 7 | Tilt-shift bez shaderu (3.3) | 1 | Trailer a Steam stránka |

### Krátce po demu — 9 dnů

| # | Co | Dny |
|---|---|---|
| 8 | ~~Měření alokací + opravy podle čísel (1.4)~~ **hotovo** | 2 |
| 9 | ~~Index budov pro render (1.1)~~ **hotovo** | 1,5 |
| 10 | ~~Prostorová energetika (2.2a)~~ **hotovo** | 2,5 |
| 11 | Mikro-animace obyvatel (3.1) | 2,5 |
| 12 | Fázové megastruktury (2.3) | 1 |

### Steam — 2 dny

| # | Co | Dny |
|---|---|---|
| 13 | Steamworks (7.0) | 2 |

Stojí samostatně schválně: je to **hradlo**, ne položka. Dokud neběží, nedá se dělat 7.1, 7.2 ani 6.4 — a nesmí se to zdržet kvůli obsahu, protože vydání na něm visí.

### Nové vrstvy — 7 dnů

| # | Co | Dny | Proč v tomhle pořadí |
|---|---|---|---|
| 14 | ~~Podmoří (6.5)~~ **hotovo** | 4 | Většina je obsah v datech; nové je jen šíření sítě po vodě |
| 15 | ~~Orbita (6.6)~~ **hotovo** | 3 | Nesahá do simulace města, takže se nemá o co rozbít |

Podmoří je napřed proto, že jeho síť je tatáž mřížka jako u energetiky (2.2a) — když už se staví, ať slouží dvakrát. Orbita je za ním, protože je to koncová meta a chce mít pod sebou hotový strom technologií.

### Komunita a Deck — 6,5 dne

| # | Co | Dny |
|---|---|---|
| 16 | Workshop (7.1) | 2,5 |
| 17 | Steam Deck (7.2) | 2 |
| 18 | Karavany přátel (6.4) | 2 |

### Poslední — 6 dnů

| # | Co | Dny |
|---|---|---|
| 19 | ~~Frontier Defense jako volitelný režim (5.2)~~ **hotovo** | 6 |

Poslední z jediného důvodu: je to jediná položka, která přidává novou entitu do tikové smyčky. Když se něco pokazí, pokazí to výkon i determinismus všeho ostatního — a to se hledá líp v hotové hře než v rozestavěné.

### Velké věci — až bude prostor

Anomálie a expedice (2.1b), doktríny (2.4), kronika (6.3), osobnosti (6.1), scénáře (5.1), plavení dřeva (6.2), hustotní mapa (1.3), zvonohra (6.7), sdílení šablon (7.3), plán guvernéra na sídlo (4.4).

### Nedělat teď

* **Železnice (2.2c)** — vlastní dopravní vrstva, vlastní graf, vlastní entity. Není o co se opřít.
* **Vlákna na pozadí (1.2)** — dokud měření neukáže, že to je opravdu potřeba, je to jen riziko pro determinismus.

---

## Co si z toho odnést

Celý dokument je zhruba **37 dnů do Frontier Defense včetně** a dalších ~25 na velké věci. To je něco jiného než ta původní matice — ne proto, že by nápady byly špatné, ale protože odhady v ní nepočítaly s tím, co už stojí (což některé věci zlevňuje) ani s tím, co chybí kolem (což jiné zdražuje).

Nic z toho seznamu není škrtnuté. Tři věci, které jsem původně psal ven z hry — podmoří, orbita a Frontier Defense — jsou uvnitř a mají svoje kroky, testy i odhady. U dvou z nich se ukázalo, že je hra unese líp, než jsem čekal: podmoří proto, že vodní biomy i hloubka už v datech jsou a `CanPlace` na vodu nesahá, orbita proto, že to není mapa, ale pohled a pár modifikátorů.

První den práce z toho seznamu — hledání ve stromu — bude znát víc než celý §6.
