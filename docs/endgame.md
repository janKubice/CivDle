# Pozdní hra: co dělat, když je všechno vymaxované

**Stav:** návrh schválený 26. 9. 2026 (body A, B, C, D). Bod D — nové světy —
je rozpracovaný zvlášť v [`svety-design.md`](svety-design.md); tady je jen
jeho místo v celku.

Dokument popisuje **proč** pozdní hra ztrácí smysl a **co** s tím udělat. Každý
bod má rozsah, dotčené systémy, data, testy a kritérium „hotovo". Pořadí
a odhady jsou na konci.

---

## 1. Diagnóza

Výchozí stav: hra na planetárním měřítku, éra Budoucnost, všechno odemčené
a koupené (snímek z hraní). Z něj a z dat vychází čtyři příčiny:

1. **Není za čím jít.** Všechny sklady jsou plné (43,3M/43,3M, 149M/149M,
   120M/120M), výroba teče do ztracena. Vylepšení Vzestupu, Odkazu i velkého
   díla jsou procenta k číslům, která už nic neznamenají. Kontrakty končí na
   stupni 40 (`contracts.json`: `maxScale`), scénáře (`scenarios.json`, tři)
   nedávají žádnou odměnu.
2. **Panel cílů mluví nesmysly.** Na planetárním měřítku radí „Break Stone
   0/10" a „Start a grove". Úvodní úkoly (`quests.json`) za hráče udělala
   automatika jinak, než úkol počítá (ruční sběr, ruční sázení), takže se
   nikdy nesplní a visí navždy. Hra tak doslova neřekne nic, co by stálo za
   to dělat.
3. **Velikost není vidět.** Z výšky je město béžová šachovnice mezi jezery —
   bez centra, panoramatu a čtvrtí s vlastní tváří. Obrovská plocha nedělá
   radost, protože nevypadá jako úspěch.
4. **Růst narazil jinde, než se zdá.** Populace 1,52M / 1,52M je strop
   **bydlení**; strop měřítka `planetary` je 10 miliard (`ascension-tiers.json`)
   — prostoru je 6 500× víc. Jenže do plochy už město nemůže (jezera) a do
   výšky ho nic nevede.

## 2. Principy

* **Nový druh činnosti, ne další násobič.** Násobiče už hráč má; chybí mu
  důvod a cíl.
* **Každá novinka musí být vidět** na mapě — velikost, která se jen čte
  z čísla, radost nedělá.
* **Měkký tlak.** Nic se neničí natrvalo, nic hráče netrestá za to, že šel
  spát (duch celé hry).
* **Data-driven** (CLAUDE.md): obsah do JSON, logika za behavior-ID do kódu,
  validace při načtení.
* **Svislé řezy.** Každý bod je samostatně hratelný a má smysl i bez dalších.

---

## 3. Bod A — cíle pro pozdní hru

Nejlevnější bod s okamžitým dopadem: opraví nesmyslný panel a dá hře, co
hlásit.

### A1. Přerostlé úkoly se zavřou samy

**Problém:** úkol „nasbírej ručně 10 kamene" nikdy neskončí, když kámen těží
automatika. Panel ho ukazuje jako „další krok" i na planetárním měřítku.

**Návrh:**

* V `quests.json` nové volitelné pole **`retireWhen`** — podmínka ve stejném
  tvaru jako `condition` (metrika, cíl). Když platí, úkol se **uzavře bez
  odměny** a z panelu zmizí. Pravidlo má každý úkol výslovně v datech (ruční
  sběr a sázení po prvním Vzestupu, slučování a modlitba po druhém, setkání
  s městy a první pomník po třetím) — žádné skryté výchozí pravidlo.
* **Uzavření se neukládá** (změna proti prvnímu návrhu): je to funkce stavu
  hry. Kdo Odkazem začne znovu od vesnice, tomu se úvodní úkoly vrátí,
  protože zase dávají smysl — kdyby se stav ukládal, zůstaly by zavřené.
  Save se tím nemění.
* Pole **`activeWhen`** (od kdy je úkol vidět) a **`group`** (`start` / `late`)
  slouží Velkým cílům z A2.
* Obrazovka úkolů ukáže uzavřené úkoly šedě v sekci „Přerostlé"; kronika je
  nezmiňuje — nejsou úspěch ani prohra.

**Dotčeno:** `QuestSystem` (vyhodnocuje jen běžící úkoly),
`Simulation.IsQuestRetired` / `IsQuestActive`, `QuestDef` + loader (validace
metriky a skupiny), panel cílů a obrazovka úkolů.

**Testy:** úkol s `retireWhen` se uzavře a nevyplatí odměnu; úkol se vrátí,
když je město zase malé; skrytý Velký cíl se nesplní, dokud není vidět;
loader odmítne neznámou metriku v `retireWhen`/`activeWhen` a neznámou
skupinu.

### A2. Velké cíle pozdní hry

Místo úvodních úkolů dostane panel **Velké cíle** — dlouhé, viditelné,
s odměnou, která se dá ukázat: **pomníkem ve městě**.

| Cíl (`quests.json`, `group: late`) | Podmínka | Vidět od | Pomník |
|---|---|---|---|
| Miliarda | populace ≥ 1 000 000 000 | 4. Vzestup | Sloup miliardy |
| Tři metropole | 3 sídla s hodností `metropolis` | 2. Vzestup | Brána bulvárů |
| Nová zem | 2 000 přetvořených dlaždic | 2. Vzestup | Socha zúrodnitelů |
| Sedm divů techniky | 7 různých dostavěných megastruktur | 3. Vzestup | Síň divů |
| Hloubka díla | stupeň velkého díla 25 | první stupeň díla | Hvězdná studna |
| Nebe nad městem | 5 družic na oběžné dráze | první kosmodrom | Nebeská lucerna |
| Město bez kouře | čistota vzduchu nad městem ≥ 95 % | 100 milionů obyvatel | Zahradní věž (čistí vzduch) |
| Věčný trh | 50 kontraktů v jednom měřítku | 5 kontraktů | Kupecký sloup |
| Klid zbraní | 100 vln obrany | první vlna | Hradba vytrvalých |
| Otevřít bránu | Hvězdná brána (C1) | — | závěrečná sekvence (bod C) |

* **Nové metriky** (`MetricKind`): sídla s aspoň danou hodností
  (`settlements` + `rank`), různé dostavěné megastruktury (`megastructures`),
  stupeň velkého díla (`grandwork`), družice (`satellites`), kontrakty
  (`contracts`), čistota vzduchu 0–100 (`airquality` — obrácené znečištění,
  protože podmínky jsou vždy „≥ práh") a vlny obrany (`waves`).
* **Odměna = pomník.** Budova má v datech `unlockedBy: "quest:<id>"`; dokud
  cíl nepadne, nedá se postavit (`Simulation.IsRewardUnlocked`). Loader
  ověří, že odkazovaný cíl existuje. Obrazovka úkolů u cíle napíše, co
  odemkne, a po splnění se stavební nabídka hned obnoví.
* **Vzhled pomníků je z dat** (`look`: tvar, barvy, prvky) a kreslí ho
  `LookPainter` — stejný systém potáhne budovy bodů B, C a D (viz 3.3 níž).
* Oproti prvnímu návrhu: „Všechny megastruktury" je **sedm divů techniky**
  (brána z bodu C přibude jako osmá a cíl by jinak přestal sedět); styly
  čtvrtí jako odměna přijdou s bodem B4; tituly v kronice vypadly —
  pomník je vidět, titul ne.

### A2b. Vzhled budov z dat

Sprity se kreslí v kódu a každá budova měla vlastní kresbu. Pro stovku nových
budov (B, C, D) by to bylo pomalé a nejednotné, proto:

* `BuildingLook` (jádro): **tvar** z pevného katalogu (30 tvarů — dům, věž,
  kopule, síň, dílna, nádrže, jáma, pole, háj, kůly, vor, balon, stožár,
  sloup, obelisk, oblouk, krystal, baňka, strom, zeď, kanál, bazén, plošina,
  vír, prstenec, zrcadla, molo, vrtná věž, kapsle), **barvy** (zeď, střecha,
  doplněk, světlo) a **prvky** (26 — okna, komín, anténa, vlajka, lucerny,
  světla, rostliny, sníh, potrubí, plachty, solární panely, krystaly, pruhy,
  prstence, střechy, pára, voda, písek, oblouky, úponky, rotor, blesk, lana).
* `LookPainter` (render) z toho kreslí se společným rukopisem: světlo zleva
  shora, okna, která v noci svítí, obrys a paleta jako ruční kresby.
* Loader odmítne neznámý tvar či prvek; test v UI ověří, že malíř umí každý
  tvar i prvek; test pokrytí spritů bere vzhled z dat jako vlastní model.
* Náhled pro autora: `LOOK_PREVIEW_DIR=… dotnet test --filter LookPreview`
  vykreslí všechny vzhledy do PNG bez grafické karty.

### A3. Výzvy s trvalou odměnou

Scénáře dnes existují (`scenarios.json`: tři, pravidla `NoAutoBuild`,
`NoAscension`), ale nic nedávají. Stanou se **Výzvami**: krátký běh s jiným
pravidlem a odměnou, která platí **pro hráče** — zapíše se do `PlayerProfile`
(stejně jako achievementy) a projeví se v každé další hře.

**Nová pravidla** (`ScenarioRule`, každé jako malé chování v kódu):

| Pravidlo | Co dělá |
|---|---|
| `NoRoads` | silnice se nestaví, svoz trpí — město musí být kompaktní |
| `FloodedWorld` | generátor zvedne hladinu moře; souše je málo |
| `SingleBiome` | celý svět jeden biom (poušť, tundra) |
| `DefenceFromStart` | obrana zapnutá od první minuty, vlny rychlejší |
| `NoResearch` | výzkum zakázaný; jen to, co je odemčené od začátku |
| `HighUpkeep` | údržba služeb dvojnásobná |
| `NightWorld` | věčná noc — světla a spokojenost rozhodují |
| `Timed` | cíl v časovém limitu (herní čas) |

**Dvanáct výzev** (návrh, doladí se hraním):

| Výzva | Pravidla | Cíl | Odměna do profilu |
|---|---|---|---|
| První tisícovka | `NoAscension` | 1 000 obyvatel | (dnešní scénář) landmark „Kámen zakladatelů" |
| Krutá zima | `NoAscension` | 300 obyvatel | styl čtvrti „Srubová" |
| Vlastníma rukama | `NoAutoBuild`, `NoAscension` | 120 budov | budova „Cech stavitelů" (+rychlost ruční stavby) |
| Město bez cest | `NoRoads` | 5 000 obyvatel | styl čtvrti „Uličky" |
| Potopa | `FloodedWorld` | 10 000 obyvatel | budova „Kůlový dům" do hlavní hry |
| Jen písek | `SingleBiome:desert` | 3 000 obyvatel | landmark „Pouštní brána" |
| Na hradbách | `DefenceFromStart` | přežít 20 vln | vzhled věží „Hradní" |
| Bez knih | `NoResearch` | 2 000 obyvatel | budova „Lidová škola" |
| Drahý provoz | `HighUpkeep` | spokojenost ≥ 80 % při 10 000 obyvatel | politika „Úsporná správa" |
| Věčná noc | `NightWorld` | 5 000 obyvatel | noční paleta města „Lucerny" |
| Sprint | `Timed` (60 min) | měřítko `city` | titul „Rychlík" v kronice |
| Mistr | vše výše | všech 11 výzev | landmark „Síň výzev" |

* `scenarios.json` dostane pole `reward` s druhy `unlockBuilding`,
  `landmark`, `districtStyle`, `policy`, `title`. Loader ověří, že odkaz
  existuje (fail-fast).
* **Budovy odemčené výzvou** jsou běžné budovy v `buildings.json` s příznakem
  `unlockedBy: "challenge:<id>"`; hra je nabídne, jen když je profil má.
* Výzvy se hrají jako samostatný save; hlavní hra se nemění.

### A4. Kontrakty bez stropu

* `maxScale` v `contracts.json` přestane být tvrdý strop: nad ním roste
  požadavek pomaleji (měkký strop, parametr v datech).
* Kontrakty na pozdním měřítku chtějí pozdní suroviny (elektronika, roboti,
  nanomateriál) a jako odměnu nabízí i body Odkazu a kosmetiku, ne jen
  suroviny, kterých je nadbytek.

### A5. Hotovo, když

* Na planetárním měřítku panel neukazuje žádný úvodní úkol a ukazuje Velké
  cíle s postupem.
* Všech 12 výzev jde spustit z menu, dohrát a odměna se objeví v nové hře.
* Kontrakty jdou za stupeň 40.
* Testy: A1 výše; každé nové pravidlo scénáře má test, že dělá, co slibuje
  (a že bez něj hra běží jako dřív); odměna výzvy se zapíše do profilu
  a projeví v nové simulaci; loader odmítne neexistující odměnu.

**Odhad:** 2–3 dny (A1 půl dne, A2 den, A3 den a půl, A4 pár hodin).

---

## 4. Bod B — růst do výšky a velikost, která je vidět

### B1. Proč město nemá kam růst

Strop měřítka (10 miliard) je daleko, strop bydlení (1,52M) je tady. Na
snímku je souš mezi jezery zastavěná. Guvernér zahušťuje (vylepšuje domy)
jen při politice `dense_housing` (`policies.json`); bez ní hledá místo na
nové domy — a když není, stojí.

### B2. Zahušťování jako výchozí cesta

* **Guvernér zahušťuje sám, když dojde místo.** Když potřeba bydlení nenajde
  místo pro nový dům, zkusí nejdřív povýšit existující (dnešní `TryDensify`),
  i bez politiky. Politika `dense_housing` zůstane — dělá z toho první volbu,
  ne až poslední.
* **Centrum města.** Každé sídlo má těžiště (`Settlement.CenterX/Y`). Budovy
  bydlení v okruhu centra dostanou vyšší stupně dřív a vyšší `visualHeight`
  (falešná výška, viz `plan-vylepseni.md` 8.5) — z centra vyroste panorama,
  okraje zůstanou nízké. Poloměr a stupně podle hodnosti sídla jsou v datech
  (`settlement-ranks.json`).
* **Nový stupeň bydlení pro planetární měřítko:** „Vertikální čtvrť"
  (3×3, arkologie nad arkologií) — `buildings.json`, odemyká ho měřítko
  `planetary`. Bez něj hustota na konci nemá kam.

### B3. Nová zem z vody

* **Nástroj „Zúrodnit mělčinu"** v `terraform.json`: mělká voda → souš
  (dnes je tam zavlažení a vysušení bažiny, voda jako cíl, ne jako zdroj).
* **Čerpací polder** — budova s `AutoTerraformSystem` (existuje): pomalu
  vysouší mělčinu v okruhu, dlaždici za kolo. Odemyká ji Velký cíl A2.
* **Megaprojekt „Vysušení jezera"** — stupňový projekt jako velké dílo:
  cena roste, každý stupeň sníží hladinu vybraného jezera o pás dlaždic.
  Viditelné (voda ustupuje, zůstává mokrá zem) a je to bezedný odběr
  přebytků, kterých je na konci plno.

### B4. Město z výšky

Dnes `DensityMap` peče hustotu jedním tónem — odtud béžová šachovnice.

* **Čtvrti barvou a materiálem:** průmyslová, obytná, občanská, logistická,
  energetická (`districts.json`, existují) dostanou na oddálení vlastní tón
  a texturu střech.
* **Výška stínem:** hustší buňky mají delší stín a světlejší hranu ze strany
  slunce — z mapy je reliéf panoramatu.
* **Centrum září:** v noci svítí centra sídel víc (`LightsRenderer`) — z výšky
  je vidět, kde město žije.
* **Hierarchie cest:** hlavní tahy mezi sídly se na oddálení kreslí širší
  (dnes jsou všechny silnice stejné) — mapa dostane kostru.
* **Landmarky jako ikony** na oddálení a **jména sídel velikostí podle hodnosti.**
* **Styly čtvrtí** (odměny z A2/A3) — hráč přiřadí čtvrti styl, ten změní
  paletu střech a doplňků. Kosmetika, ale dělá z plochy „moje město".

Všechno se peče do textur jako dnes (výkon: žádná práce za snímek navíc).

### B5. Velké dílo je vidět

Jáma `great_pit` dnes s každým stupněm jen zdraží. Nově roste i na mapě:
každých pár stupňů se zvětší, přibudou lešení, jeřáby, v noci světla, na
vysokých stupních „Hvězdná studna" (odměna A2).

### B6. Hotovo, když

* Město na planetárním měřítku dál roste, i když souš je zastavěná
  (zahušťováním a zúrodňováním), a populace se nezastaví na stropu bydlení.
* Z oddálení jsou poznat centra, čtvrti a hlavní tahy (porovnat snímky před
  a po).
* Testy: guvernér zahušťuje bez politiky, když nenajde místo; polder mění
  mělčinu na souš jen po dlaždici; vysušení jezera je deterministické
  a přežije save; pečení mapy hustoty dává stejný výsledek jako dnes pro
  město bez čtvrtí (regrese).

**Odhad:** 4–6 dní (B2 den a půl, B3 den a půl, B4 dva dny, B5 půl dne).

---

## 5. Bod C — konec kapitoly a Nová hra+

Dohrání má být **zážitek**, ne prázdno. C dává hře konec, a zároveň je to
most do bodu D.

### C1. Hvězdná brána

* Poslední megastruktura (`buildings.json`, kategorie `megastructure`),
  odemyká ji měřítko `planetary` a všechny ostatní megastruktury.
* Staví se **ve stupních** jako velké dílo — obrovský odběr přebytků, které
  jinak propadají. Každý stupeň mění vzhled (`BuildStage` — fáze spritu podle
  postupu stavby, existuje): základy, prstenec, výztuhy, aktivace.
* Guvernér ji nestaví (divy a megastruktury zůstávají hráči, viz role
  guvernéra).

### C2. Závěrečná sekvence

Po aktivaci brány:

1. Kamera se oddálí nad celé město, přijde noc, brána se rozsvítí.
2. **Timelapse** civilizace od prvního domu (`TimelapseScreen`, existuje).
3. **Kronika** jako ilustrovaný souhrn (`chronicle.json`, existuje): éry,
   sídla, osobnosti, katastrofy, rekordy.
4. **Statistiky:** herní čas, nejvyšší populace, postavené budovy, Vzestupy,
   odražené vlny, splněné výzvy.
5. **Titulky** se jmény sídel a osobností z téhle hry, hudba.

Sekvenci jde kdykoli pustit znovu z menu a uložit jako video (capture
pipeline existuje).

### C3. Co potom

* **Město zůstává.** Brána nic neresetuje; hra běží dál, Velké cíle i výzvy
  zůstávají.
* **Dokud D není hotové:** brána nabídne **Novou hru+** — nový svět se
  zvoleným pravidlem výzvy (A3) a vším, co je v profilu (odměny výzev, styly,
  rekordy). Hlavní město zůstane v savu.
* **S bodem D:** brána je **start kolonizace** — z hlavního města se stane
  první planeta galaxie. Konec první kapitoly je začátek druhé
  (viz [`svety-design.md`](svety-design.md)).

### C4. Hotovo, když

* Brána jde postavit, stupně jsou vidět, po aktivaci proběhne celá sekvence
  a jde ji pustit znovu z menu.
* Achievement „Brána otevřena" (Steam i profil).
* Testy: brána se neodemkne bez podmínek; stupně jsou deterministické
  a přežijí save; sekvence nemění stav hry (jen ho čte); Nová hra+ přenese
  profil a nesáhne na původní save.

**Odhad:** 2–3 dny.

---

## 6. Bod D — nové světy

Samostatný dokument: [`svety-design.md`](svety-design.md). Shrnutí:

* Po Hvězdné bráně (C1) se otevře **galaxie**. Hlavní město zůstává a běží
  souhrnně dál; hráč zakládá kolonie na **šesti světech** (poušť, led,
  souostroví, sopka, plynný obr, cizí svět).
* Každý svět má **vlastní pravidlo**, 6 nových surovin a **20–22
  unikátních budov**, vlastní atmosféru, architekturu, faunu a zvuk.
* Světy obchodují — nejlepší budovy každého potřebují dovoz odjinud.
* Vzestup platí pro každý svět zvlášť, Odkaz je společný pro celou galaxii.

---

## 7. Pořadí a odhady

| Pořadí | Bod | Odhad | Proč v tomhle pořadí |
|---|---|---|---|
| 1 | A — cíle, výzvy, kontrakty | 2–3 dny | opraví nesmyslný panel, dá hře cíle hned |
| 2 | C — Hvězdná brána, konec, NG+ | 2–3 dny | dohrání dostane tečku; připraví most do D |
| 3 | B — hustota, nová zem, město z výšky | 4–6 dní | radost z velikosti; dobré na trailer |
| 4 | D — nové světy | 6–9 týdnů po fázích | druhá kapitola hry (viz dokument D) |

A a C jdou před demo, B krátce po něm, D jako velká aktualizace po vydání
(„zdarma: nové světy" je silná zpráva pro Steam).
