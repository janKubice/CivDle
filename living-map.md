# Living Map — Živý a pestrý svět

*Pracovní dokument · verze 0.1*

Jak udělat mapu živou a ne-repetitivní: fauna, počasí, den/noc, biomové zvláštnosti (kaktusy, gejzíry, kaňony) a ekonomická identita biomů. Navazuje na **Content Design** (biomy), **Game Feel** (den/noc jako wow), **Budovy** a **Tech Stack** (výkon). Rozšíření datové struktury je v dokumentu **Data-Driven Content** (sekce „živá mapa"). Skoro vše je **[návrh]**.

---

## 0. Filozofie: mapa žije a nenudí

Tři principy:

1. **Vrstvy života.** Statické pozadí je mrtvé. Živé pozadí = pohyb (fauna), proměna v čase (den/noc, počasí) a rozmanitost (dekorace, landmarky). Každá vrstva stojí málo, dohromady dělají svět, který dýchá.
2. **Každý biom má identitu.** Nejen jiné suroviny (to už máme), ale jiný *pocit* — jinou faunu, jiné počasí, jiné dekorace, jinou paletu. Poušť a džungle nemají působit jako přebarvená louka.
3. **Anti-repetice je záměr, ne náhoda.** Nekonečná procedurální mapa se snadno zvrhne v „pořád to samé". Rozmanitost se musí navrhnout (viz sekce 6).

---

## 1. Denní a noční cyklus

- **Vizuál:** plynulá změna osvětlení a barevné palety — teplé ráno, jasné poledne, zlatavý večer, modrá noc.
- **Noc = wow moment:** město se rozzáří okny a pouličními světly. Oddálený pohled na milionovou civilizaci plnou světel v noci je přesně ten efekt, cos chtěl od začátku.
- **Život podle času [návrh]:** ptáci ve dne, světlušky a sovy v noci, některá zvířata jen za soumraku. Levné, ale mapa díky tomu žije jinak ráno a jinak večer.
- **Gameplay:** doporučuju držet **hlavně vizuální** (sedí na relax). Volitelně drobnost: pár tvorů/surovin vázaných na denní dobu, ať má cyklus i špetku smyslu.

---

## 2. Počasí

Počasí je **vázané na biom** — každý má svoje, což posiluje jeho identitu.

| Biom | Počasí (ambientní) |
|---|---|
| Nížina / louka | jasno, déšť, mlha, duha po dešti |
| Les / džungle | častý déšť, hustá mlha, prosvítání slunce korunami |
| Hory | sníh, vánice, nízká oblačnost |
| Poušť | žár (vlnění vzduchu), písečné bouře |
| Pobřeží | vítr, bouře nad mořem |
| Tundra | sníh, **polární záře v noci** [návrh — velký wow] |
| Bažina | mlha, mrholení, bludičky za noci |

**Dvě úrovně počasí:**
- **Ambientní** (déšť, mlha, žár) = čistě atmosféra, mění náladu scény.
- **Extrémní** (bouře, vánice, písečná bouře) = napojení na **eventy** z Content Designu — dočasně sníží flow (rybolov/farmy), nikdy nezničí. Tím se počasí a katastrofy propojí do jednoho systému místo dvou.

**Roční období [návrh, scope-náročné]:** jaro/léto/podzim/zima měnící barvy listí a délku dne. Nádherné, ale je to systém navíc — spíš kandidát na update než na launch.

---

## 3. Ambientní fauna

Zvířata a ptáci, co dělají svět živým. Většina **čistě dekorace**, část **lovitelná** (napojení na job „lovec" a surovinu maso/kůže z Content Designu).

| Biom | Fauna |
|---|---|
| Louka | zajíci, jeleni (prchají před panáčky), motýli, hejna ptáků |
| Les | srnky, lišky, veverky, sovy (noc), zpěvní ptáci |
| Džungle | opice, pestří ptáci, hejna hmyzu, hadi |
| Hory | kozorožci, orli kroužící nad vrcholky, vlci |
| Poušť | ještěrky, škorpioni, supi, velbloudi u oáz |
| Pobřeží | racci, kraby, skákající ryby, delfíni u pobřeží |
| Tundra | sobi, polární lišky, tuleni |
| Bažina | žáby, volavky, světlušky (noc) |

**Chování [návrh]:** pár jednoduchých vzorů stačí — *hejno* (ptáci letí přes obrazovku), *toulání* (zvěř se pomalu pohybuje), *útěk* (rozprchne se, když se přiblíží panáček/vozidlo). LOD: fauna se vykresluje jen u kamery; z dálky mizí (nikdo ji tam neuvidí).

> **Design pointa:** část fauny je zároveň **surovinový uzel** — jelen v lese = maso a kůže pro lovce. Tím zvíře není jen kulisa, ale i důvod expandovat do lesa. Většina ať ale zůstane čistě pro atmosféru.

---

## 4. Biomové dekorace a landmarky

Statické (nebo lehce animované) prvky, které dávají biomu tvář. Dělí se na **běžné dekorace** (husté, rozmístěné náhodně) a **landmarky** (vzácné, výrazné body zájmu, co lámou monotónnost).

| Biom | Běžné dekorace | Landmarky (vzácné) |
|---|---|---|
| Louka | květiny, balvany, rybníčky, osamělé stromy | prastarý strom, kamenný kruh |
| Les / džungle | kapradí, liány, houby, padlé kmeny | vodopád, zarostlé ruiny, obří strom |
| Hory | útesy, sněhové čepice, viditelné rudné žíly | jeskyně, horský průsmyk, vodopád |
| Poušť | **kaktusy**, tumbleweed, kosti, duny | **gejzíry**, **kaňony**, mesy, skalní oblouky, ropné prosaky |
| Pobřeží | mušle, přílivové tůně, naplavené dřevo | útesová zátoka, vrak lodi, maják-skála |
| Tundra | ledové útvary, zmrzlá jezera | ledovcová trhlina, aurora nad obzorem |
| Bažina | mangrovy, lekníny, uschlé stromy | bublající jezírko, potopené ruiny |
| Vulkanická zóna [návrh] | láva, obsidián, popel | aktivní gejzíry, lávové jezero |

> **Landmarky jsou tvůj nástroj proti nudě.** Kaňon, gejzírové pole nebo obří strom uprostřed pouště přeruší „pořád to samé" a dá hráči orientační bod a důvod se tam podívat. Rozmísťuj je řídce — vzácnost dělá dojem.

---

## 5. Ekonomická identita biomů

Tvůj nápad — džungle skoro nekonečné dřevo, ropa v pouštích — je skvělý: biomy nejsou jen jiná grafika, mají **jinou ekonomiku**. To dává expanzi smysl a každému biomu roli.

| Biom | Ekonomická identita |
|---|---|
| **Džungle [tvůj nápad]** | téměř nevyčerpatelné dřevo (rychle dorůstá / extrémní hustota), ale těžká stavba a riziko nemocí |
| **Poušť [tvůj nápad]** | ropa, minerály, později solární energie; skoro žádné jídlo/voda |
| Louka | jídlo, snadná stavba — bezpečná základna |
| Les | vyvážené dřevo + lov, ale míň polí |
| Hory | rudy a kovy, kámen; skoro žádná půda |
| Pobřeží | ryby a obchod přes přístav |
| Tundra | plyn / vzácné materiály; pomalý růst, drahé topení |
| Bažina | hlína a voda; pomalé, riziko nemocí |

> **Návaznost:** tohle prohlubuje smyčku „expanduj za surovinou" z Content Designu. Chceš stavět z dřeva bez limitů? Prober se do džungle — a vyřeš její nevýhody. Každý biom je jiná nabídka a jiná výzva.

---

## 6. Jak zabránit repetici *(přímá odpověď na „ne moc repetitivní")*

Nekonečná procedurální mapa se sama od sebe stane nudnou. Nástroje proti tomu:

- **Varianty spritů.** Ne jeden strom, ale 4–6 variant; totéž kameny, kaktusy, keře. Náhodně se střídají.
- **Náhodná transformace při umístění.** Jemná odchylka měřítka, překlopení a lehký tint u každé instance — stejný sprite pak nikdy nevypadá úplně stejně.
- **Landmarky jako body zájmu.** Řídce rozeseté výrazné prvky (sekce 4) lámou monotónnost a dávají orientaci.
- **Sub-biomy.** Poušť není jednolitá — má oblasti dun, skalnaté části, oázy. Vnitřní pestrost uvnitř biomu.
- **Měkké přechody.** Biomy se prolínají, ne ostrá hranice — les postupně řídne do louky.
- **Ručně-navržené set-piece „sety".** Procedura rozmisťuje, ale sem tam vloží ručně složenou scénku (gejzírové pole, ruiny) — kombinace nekonečnosti a autorského dojmu.
- **Proměna v čase.** Den/noc + počasí + fauna znamenají, že i totožný kus mapy vypadá jinak ráno, za deště a v noci. Čas sám bojuje proti nudě.

> Repetici neporazíš jedním trikem, ale **skládáním** těchhle vrstev. Každá přidá kousek pestrosti.

---

## 7. Výkon *(caveat)*

Konzistentně s Tech Stackem — život je levný, jen musí být cílený:

- **LOD pro faunu i dekorace.** Zvířata a drobné props jen u kamery; z dálky mizí nebo přejdou na agregát.
- **Pooling pro počasí.** Kapky deště / vločky / písek = recyklované částice, ne alokace za běhu; strop na počet.
- **Den/noc jako levný overlay.** Jedna barevná/světelná vrstva přes scénu, ne přepočítávání každého spritu.
- **Instancing dekorací.** Tisíce stromů/kaktusů přes batching (viz atlas v Tech Stacku), ne jednotlivě.

---

## 8. Jak to zapadá do zbytku

```
DEN/NOC + POČASÍ  ──mění──►  náladu scény  (a noc = wow z Game Feel)
POČASÍ (extrémní)  ──je──►  EVENT z Content Designu  (flow ↓, nikdy zničení)
FAUNA  ──část je──►  surovinový uzel (lov → maso/kůže → jídlo)
DEKORACE + LANDMARKY  ──dávají──►  biomu identitu + anti-repetici
EKONOMICKÁ IDENTITA biomu  ──pohání──►  smyčku expanze za surovinou
```

---

## 9. Otevřené otázky k doladění

- **Má den/noc ovlivnit gameplay, nebo být čistě vizuál?** Doporučuju vizuál + špetka (noční tvorové).
- **Kolik fauny je ještě únosné na výkon?** Ladit strop podle testů na velké mapě.
- **Roční období — launch, nebo update?** Krásné, ale scope-náročné; spíš update.
- **Kolik variant spritů na prvek je rozumné minimum?** 4–6 bývá sweet spot mezi pestrostí a prací pro grafika.
- **Vulkanická zóna — samostatný biom, nebo vzácná zvláštní oblast?** Ovlivní, jak často ji hráč potká.

---

*Návrh k iteraci. Vše označené **[návrh]** je moje doplnění — vyber a škrtej.*
