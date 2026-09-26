# Nové světy — druhá kapitola CivDle (bod D)

**Stav:** návrh schválený 26. 9. 2026. Souvisí s [`endgame.md`](endgame.md)
(body A, B, C) — Hvězdná brána z bodu C je vstupem do galaxie.

**Schválená rozhodnutí:**

1. **Hlavní město zůstává a běží dál** — souhrnně, když se hráč dívá jinam.
   Kolonizace nic neresetuje.
2. **Vzestup platí pro každý svět zvlášť, Odkaz je společný** pro celou
   galaxii.
3. **Šest světů v tomto pořadí:** Duna (poušť) → Mráz (led) → Souostroví
   (oceán) → Výheň (sopka) → Nebesa (plynný obr) → Xeno (cizí svět).
4. **Každý svět má 20–22 unikátních budov**, ne jen hrstku — svět má být
   druhá hra, ne reskin.

Dokument je rozdělený tak, aby se podle něj dalo stavět po svislých řezech:
nejdřív kostra (kap. 2), pak šablona obsahu (kap. 3), světy jeden po druhém
(kap. 4), vizuál (kap. 5), technika (kap. 7) a plán (kap. 8).

---

## 1. Proč a co je „svět"

### 1.1 Problém, který to řeší

Na konci první kapitoly má hráč všechno: čísla bez cíle, plné sklady
a obrovské město, které už nemá kam růst ani co ukázat
(viz [`endgame.md`](endgame.md), diagnóza). Další násobiče nepomůžou.
Pomůže **nový začátek se smyslem**: nejzábavnější část CivDle je rozjezd
— první chatrče, první řetězec, první čtvrť — a nové světy ho vrací,
tentokrát s jiným pravidlem a s vazbou na to, co už hráč vybudoval.

### 1.2 Pilíře

1. **Každý svět je nové pravidlo, ne nová textura.** Poušť nemá dřevo
   a potřebuje vodu, led potřebuje teplo, oceán nemá souš, sopka vybuchuje,
   plynný obr nemá zem vůbec, cizí svět žije. Stejné stavění — jiná
   skládačka.
2. **Mechanika je vidět.** Pravidlo světa se musí dát pochopit pohledem na
   mapu: zelený pás kolem vody, roztátý sníh kolem tepla, žhnoucí dráha
   lávy, klesající plošina. Tooltip je až druhý.
3. **Měkký tlak.** Nic se neničí natrvalo. Budova, na kterou dopadne bouře,
   láva nebo mráz, **vypadne** z výroby a sama se vrátí. Idle hra nesmí
   trestat za to, že hráč šel spát.
4. **Světy se potřebují.** Nejlepší budovy každého světa chtějí dovoz odjinud.
   Galaxie je jedna ekonomika, ne šest oddělených her.
5. **Hlavní město má smysl.** Stane se průmyslovým srdcem galaxie: vyrábí
   stroje a roboty pro kolonie, přijímá luxus a jeho plné sklady konečně mají
   kam téct (kolonizační lodě).
6. **Všechno v datech.** Svět je záznam v `worlds.json` + obsah označený
   značkou světa. Kód je jen „jak": sítě, přírodní jevy, render.

### 1.3 Anatomie jednoho světa

Každý svět má pevnou kostru, aby byly srovnatelné a dalo se je stavět podle
šablony (kap. 3):

| Část | Rozsah |
|---|---|
| Hlavní pravidlo | 1 mechanika, která mění skládačku (síť nebo přírodní jev) |
| Vedlejší mechanika | 1–2 (počasí, rytmus dne, terén) |
| Nové suroviny | 6 |
| Unikátní budovy | 20–22 v pevných rolích (kap. 3.2) |
| Větev výzkumu | 10–11 technologií |
| Vzestup světa | 6–8 vylepšení pro tenhle svět |
| Události | 5 |
| Počasí | 3–4 druhy (část existuje) |
| Fauna | 3 druhy |
| Hvězdy | 3 + 1 mistrovská |
| Vývoz / dovoz | 1–2 artikly ven, 1–2 dovozy pro nejlepší budovy |
| Vzhled | profil atmosféry, paleta, architektura, noční obloha |
| Zvuk | vlastní ambient a hudební motiv |

---

## 2. Kostra galaxie

### 2.1 Vstup do galaxie

* Hvězdná brána (bod C) je poslední megastruktura hlavního města. Její
  aktivace spustí závěrečnou sekvenci první kapitoly a **otevře galaxii**.
* Hlavní město dostane jméno **Domovina** a stane se prvním světem galaxie.
  Nic se neresetuje.
* Na mapě galaxie se objeví Domovina a obrys prvního dostupného světa (Duna).

### 2.2 Kolonizace

* **Kolonizační loď** je stupňový projekt v kosmodromu Domoviny (budova
  `spaceport` existuje) — stejný princip jako velké dílo: několik stupňů, cena
  roste. Je to **obří odběr** přebytků, které dnes propadají: stroje,
  elektronika, roboti, jídlo, stavební materiál. Každá další kolonie stojí
  víc (`worlds.json` → `colonyCost` × růst).
* Po dokončení: animace startu (kap. 5.5), výběr místa přistání na nové
  planetě (tři nabídnutá místa — jako výběr startu v úvodu hry, ten existuje)
  a začátek.
* **Kolonie začíná malá, ale ne od nuly:**
  * **Přistávací modul** — startovní budova: bydlení pro 30 lidí, malý sklad,
    výroba základního stavebního materiálu světa (ať první minuty neuvíznou).
  * **Náklad lodi** — startovní suroviny podle světa (`startingKit`).
  * **Společné znalosti:** technologie Domoviny, které dávají smysl i tady
    (sklady, služby, věda, správa), jsou známé. **Větev světa** se zkoumá
    od začátku.
  * **Měřítko od vesnice.** Kolonie má vlastní žebříček měřítek a vlastní
    Vzestup (2.6).
* Kolonizace **nepřeruší** Domovinu — ta běží dál souhrnně (2.4).

### 2.3 Role Domoviny

* **Průmyslové srdce:** vyváží stroje, elektroniku, počítače, roboty
  a nanomateriál — kolonie je potřebují na pokročilé budovy, dokud si
  vlastní výrobu nepostaví (a některé ji nemají vůbec: plynný obr nemá rudy).
* **Přijímá luxus a exotiku:** koření, perly, krystaly — spokojenost a nové
  budovy Domoviny (Galaktická burza, Muzeum světů, Galaktický div — 6.4).
* Dál roste (bod B), plní Velké cíle (bod A).

### 2.4 Přepínání světů a souhrnná simulace

Naplno se simuluje **jen svět, na který se hráč dívá**. Ostatní běží
**souhrnně** — jako dnešní dohánění offline, jen trvale.

* **Při odchodu ze světa** se změří jeho toky: čistý přírůstek každé suroviny
  (evidence toků už umí `SteadyTotal`), růst populace, výroba vývozu. Uloží
  se do záznamu světa spolu s úplným snímkem simulace.
* **Mezitím** se na záznamu jen počítá: zásoby += tok × čas (mezi nulou
  a stropem skladu), populace roste k bydlení, vývoz odtéká obchodními
  trasami. Cena je O(počet surovin) na svět — zanedbatelná.
* **Při návratu** se snímek načte, připíšou se souhrnné přírůstky a proběhne
  krátké přesné dotikání (60–120 tiků), aby se rozjely výrobny. Cíl: přepnutí
  **do 2 sekund** i u velkého města.
* **Dlouhá nepřítomnost** (hráč byl na jiném světě hodiny): při návratu se
  použije `OfflineCatchUp` se svými zárukami (strop práce, kroky, ukazatel)
  — guvernér za tu dobu opravdu stavěl.
* **Galaktické hodiny** jsou jedny: tiky aktivního světa posouvají čas celé
  galaxie. Offline se dohání aktivní svět jako dnes a ostatní souhrnně.

### 2.5 Obchod

* **Obchodní trasa** = svět A posílá surovinu R světu B. Zakládá se na mapě
  galaxie tažením od planety k planetě.
* **Kapacita** trasy = menší z kapacit přístavů na obou koncích (každý svět má
  vlastní přístavní budovu: karavanseraj, dok vzducholodí, komunikační květ…
  a Domovina kosmodrom). Kapacita roste s výzkumem a vylepšením přístavu.
* **Doba cesty** podle vzdálenosti na mapě — zboží dorazí se zpožděním;
  lodě jsou vidět (kap. 5.4).
* Trasa **nic nevytváří**: co odejde z A, dorazí do B (test na zachování).
  Když A dojde surovina, trasa čeká.
* Dovoz se v cílovém světě objeví v evidenci toků jako „dovoz" (hráč vidí,
  odkud co je).
* Guvernér trasy nezakládá (je to strategické rozhodnutí hráče), ale
  **hlásí**, že budova čeká na dovoz, a navrhne trasu jedním klikem.

### 2.6 Progrese

**Hvězdy.** Každý svět má tři hvězdy a jednu mistrovskou:

* ★ populace 5 000 (svět se chytil),
* ★★ zvládnutí pravidla světa (konkrétní výzva, kap. 4),
* ★★★ div světa,
* ✦ mistrovská výzva (volitelná, těžká).

**Odemykání světů** podle celkového počtu hvězd (včetně mistrovských):

| Svět | Odemyká se |
|---|---|
| Duna | Hvězdná brána (bod C) |
| Mráz | 2 ★ |
| Souostroví | 4 ★ |
| Výheň | 6 ★ |
| Nebesa | 9 ★ |
| Xeno | 12 ★ |

**Vzestup pro každý svět zvlášť.** Každý svět má vlastní žebříček měřítek
(stejné stupně jako Domovina, strop se dá v datech světa snížit) a vlastní
body Vzestupu. Kromě společných vylepšení (výroba, sklady, růst) má každý
svět **6–8 vlastních** (efektivita vody, izolace, lehčí plošiny…). Vzestup
kolonie resetuje jen tu kolonii.

**Odkaz pro celou galaxii.** Body Odkazu se sčítají ze všech světů, jeho
vylepšení platí všude (výroba, rychlost stavby, zachování staveb při
Vzestupu…). Nová vylepšení Odkazu pro galaxii: levnější kolonizační lodě,
kapacita tras, rychlejší přepínání (kratší dotikání).

**Galaktická sbírka** (profil hráče): hvězdy, divy, fauna viděná na každém
světě, rekordy — obrazovka „Muzeum světů".

### 2.7 Konec druhé kapitoly

**Galaktický div „Souhvězdí"** na Domovině potřebuje artikly ze všech šesti
světů (biokrystaly z Xena jako poslední). Po dostavbě proběhne epilog celé
galaxie: timelapse každého světa, kronika, statistiky, titulky — stejná
sekvence jako bod C, rozšířená na všechny světy. Galaxie pak běží dál.

---

## 3. Šablona obsahu světa

### 3.1 Co zůstává a co se mění

* **Společné budovy** (sklady, škola, knihovna, správa, služby…) jsou
  dostupné na všech světech, kde dávají smysl. Svět je může **zakázat** po
  kategoriích nebo jednotlivě (`bannedBuildings`, `bannedCategories`) —
  poušť nemá dřevorubce ani pilu, plynný obr nemá nic, co stojí na zemi.
* **Společné suroviny** (jídlo, nástroje, věda, víra, stroje…) platí všude;
  svět je může doplnit vlastními zdroji (datle dávají jídlo, rybáři taky).
* **Společné systémy** (spokojenost, služby s dosahem, znečištění, čtvrti,
  sídla, guvernér, kontrakty) běží i na koloniích.
* **Nové:** hlavní pravidlo, 6 surovin, 20–22 budov, větev výzkumu,
  vylepšení Vzestupu, události, počasí, fauna, vzhled, zvuk.

### 3.2 Role unikátních budov

Aby 20+ budov nebyla hromada, každá má **roli** a každý svět má všechny role
obsazené:

| Role | Počet | K čemu |
|---|---|---|
| Bydlení | 3–4 | levné → husté → luxusní (luxusní chce dovoz) |
| Pravidlo světa | 4–5 | zdroje a přenos sítě / ochrana před jevem / předpověď |
| Těžba | 3–4 | suroviny z terénu světa |
| Zpracování | 3–4 | řetězce k vývozu a stavebním materiálům |
| Energie | 1–2 | energie typická pro svět |
| Služby | 2 | spokojenost, typické pro svět |
| Obchod | 1 | přístav světa (kapacita tras) |
| Div | 1 | ★★★, mění pravidlo světa ve prospěch hráče |

### 3.3 Hrubý balanc

* **Rozjezd:** první ★ (5 000 obyvatel) za 60–90 minut hraní bez cheatů
  a s guvernérem — jako druhá vesnice, ne jako první.
* **Pravidlo tlačí brzy:** první setkání s mechanikou (první bouře, první
  mráz, první příliv) do 10 minut, s varováním předem.
* **Dovoz je bonus, ne blokáda:** bez dovozu jde dojít k ★★★; dovoz
  odemyká nejlepší stupně (luxusní bydlení, velké elektrárny).
* **Guvernér zvládne ★ sám** (hands-off test, kap. 7.13) — pravidlo světa
  musí umět řešit i on.

---

## 4. Světy

Pro každý svět: identita, pravidlo, suroviny, budovy, řetězce, výzkum,
události a počasí, fauna, hvězdy, obchod, vzhled, zvuk a co je nového
v kódu. ID v závorkách jsou návrhy pro data.

---

### 4.1 Duna — pouštní svět (`dune`)

**Identita.** Rozpálený svět dun, skalních stolů a solných plání s řídkými
oázami. Voda je všechno. Město roste podél vodních tras jako náhrdelník oáz
— z výšky je vidět zelená žilnatina v okrové pláni.

**Hlavní pravidlo: voda.** Budovy mají `waterDemand` a fungují jen v dosahu
**vodní sítě** (stejný princip jako dnešní elektrická síť: zdroj zaplní
buňky 8×8 kolem sebe, dokud stačí výkon; bez drátů).

* **Zdroje:** studna (jen na podzemní vodě — generátor světa vytvoří mapu
  zvodní), lapač rosy (kdekoli, málo, jen v noci), oáza (přírodní, velká,
  nepřenosná), odsolovací věž (u moře, pozdě).
* **Přenos:** kanát (podzemní kanál, dlouhý dosah jedním směrem), cisterna
  (zásobník — vyrovná noc a bouře, prodlouží dosah).
* **Nedostatek:** výroba × pokrytí vodou (jako proud); domy bez vody rostou
  pomaleji a mají nižší spokojenost. Farmy bez vody nevyrábí vůbec.
* **Vidět:** půda v dosahu vody zezelená (tráva, keře), mimo zůstává písek.
  Zdroje mají modrý kruh v překryvu sítě.

**Vedlejší mechanika: písečné bouře.** Bouře (počasí `sandstorm` existuje)
přejde přes mapu jako pás po směru větru. Budova v pásu bez **větrolamu**
v dosahu se **zasype** — vypadne z výroby na několik minut (stejný
mechanismus jako poškození budovy v režimu obrany), pak ji lidé vyhrabou.
Bouře má varování (obloha zhnědne, meteorologie ukáže směr). Duny se pomalu
posouvají (kosmetika).

**Suroviny**

| Surovina | Odkud | K čemu |
|---|---|---|
| Písek (`sand`) | pískovna na dunách | sklo |
| Hlína (`clay`) | hliník u oáz a vádí | nepálené cihly |
| Nepálená cihla (`adobe`) | sušárna cihel | hlavní stavební materiál |
| Sklo (`glass`) | sklárna | zrcadla, cisterny, kopule, **vývoz** |
| Sůl (`salt`) | solné pánve | konzervace (sklad jídla), koření, obchod |
| Koření (`spice`) | kořenná zahrada | luxus → spokojenost, **vývoz** |

Jídlo dávají datlové háje a oázové zahrady (společná surovina `food`).

**Budovy (22)**

| Budova | Role | Co dělá | Vzhled |
|---|---|---|---|
| Nepálená chýše (`adobe_hut`) | bydlení | levné, málo vody | kostka z hlíny, plátěná stříška |
| Dvorní dům (`courtyard_house`) 2×2 | bydlení | vyšší kapacita, stín dvora = spokojenost | uzavřený dvůr s palmou |
| Věž s lapačem větru (`windcatcher_tower`) 2×2 | bydlení | hustá; chladí okolí (+spokojenost) | vysoká věž s mřížovým lapačem |
| Palác u oázy (`oasis_palace`) 3×3 | bydlení (luxus) | nejvyšší kapacita; chce koření a perly | kopule, arkády, bazén |
| Studna (`well`) | pravidlo | zdroj vody jen na zvodni | kamenný kruh s rumpálem |
| Lapač rosy (`dew_trap`) | pravidlo | malý zdroj kdekoli, jen v noci | síťky na kůlech, ráno se lesknou |
| Cisterna (`cistern`) | pravidlo | zásobník + prodloužení dosahu | nízká kopule s průduchy |
| Kanát (`qanat`) | pravidlo | dlouhý přenos jedním směrem | řada šachet v zemi |
| Větrolam (`sand_fence`) | pravidlo | chrání okruh před zasypáním | plot z rákosu a hlíny |
| Hliník (`clay_pit`) | těžba | hlína | mokrá jáma, hromady |
| Pískovna (`sand_quarry`) | těžba | písek | stupňovitá jáma v duně |
| Solné pánve (`salt_pans`) | těžba | sůl na solné pláni | bílé čtverce, zrcadlí nebe |
| Datlový háj (`date_grove`) | těžba | jídlo, chce vodu | řady palem |
| Sušárna cihel (`adobe_yard`) | zpracování | hlína + voda → cihly (v horku rychleji) | řady cihel na slunci |
| Sklárna (`glassworks`) | zpracování | písek + palivo → sklo | pec se žhnoucím ústím |
| Kořenná zahrada (`spice_garden`) | zpracování | voda + sůl → koření | barevné terasy |
| Sluneční zrcadla (`solar_mirrors`) | energie | proud, nejvíc v poledne, slabne v bouři | pole zrcadel s věží |
| Karavanseraj (`caravanserai`) | obchod | přístav světa: kapacita tras + služba | dvůr s velbloudy |
| Hammám (`hammam`) | služba | spokojenost, spotřebuje vodu | kopulky s párou |
| Hvězdárna pouště (`desert_observatory`) | služba | věda + spokojenost v noci | bílá kopule, dalekohled |
| **Skleněná oáza** (`glass_oasis`) 5×5 | div | obří skleněná kopule: velký zdroj vody, okolí se pomalu mění v trávu | třpytivá kopule, uvnitř zeleň |

Odsolovací věž (`desalinator`, pozdní zdroj vody u moře, potřebuje dovoz
krystalů) je 22. budova; objeví se až s dovozem z Mrazu.

**Řetězce.** hlína + voda → cihly → domy · písek + palivo → sklo → zrcadla,
cisterny, vývoz · sůl + voda → koření → luxus, vývoz · voda → datle → jídlo.

**Výzkum (11).** vodní hospodářství, kanáty, sušení cihel, sklářství,
solivarnictví, karavany, sluneční zrcadla, větrolamy, kořenářství,
odsolování, Skleněná oáza.

**Vzestup Duny (7).** úsporné zavlažování (−poptávka vody), hlubší studny
(+dosah), pevné větrolamy, rychlé sušení, sklářští mistři, koření světu
(+vývoz), noční rosa (+lapače).

**Události.** Vyschlá oáza (dočasně menší zdroj) · Karavana z hor (nabídka
výměny) · Fata morgana (kosmetika, fotka) · Bouře století (velký pás,
dlouhé varování) · Pouštní rozkvět (vzácný déšť: pár minut zeleně a růstu).

**Počasí.** jasno a výheň (tetelení existuje), písečná bouře (existuje),
vzácný déšť, chladná hvězdná noc.

**Fauna.** velbloudi (táhnou karavany mezi karavanseraji), fenek, supi
kroužící nad městem.

**Hvězdy.** ★ 5 000 obyvatel · ★★ celé město v dosahu vody a 10 bouří bez
zasypaného domu · ★★★ Skleněná oáza · ✦ 25 000 obyvatel bez odsolování.

**Obchod.** Vývoz: **sklo, koření**. Dovoz: broušené krystaly z Mrazu
(odsolovací věž), perly ze Souostroví (palác u oázy).

**Vzhled.**
* *Světlo:* vysoké ostré slunce, v poledne bílé, ráno a večer zlaté; krátké
  tvrdé stíny v poledne, dlouhé za soumraku.
* *Obloha:* bledá modř přecházející u obzoru do písku; v bouři hnědá.
* *Voda:* tyrkysové oázy s bílým solným lemem.
* *Částice:* písek nesený větrem, tetelení nad dunami.
* *Noc:* chladná modř, nejhvězdnější obloha v galaxii s Mléčnou dráhou.
* *Architektura:* ploché střechy, kopule, oblouky, dvory, lapače větru;
  okrové a pískové omítky, indigové dveře a látky.
* *Paleta:* okr, terakota, písková, indigo, zelená jen tam, kde je voda.
* *Mechanika vidět:* zelená žilnatina kolem vodní sítě; zasypaná budova
  má kopeček písku a nekouří.

**Zvuk.** vítr v písku, zvonky karavan, kapání ve studni, cvrčci v noci;
motiv: loutna a bubínek.

**Nové v kódu.** síť `water` (obecná síť, kap. 7.2) · přírodní jev
`weather_burial` (sdílí Mráz a Souostroví) · vrstva zvodní v generátoru ·
biom `salt_flat` · překryv vodní sítě · zelenání půdy podle pokrytí vodou.

**Odhad:** 6–8 dní (první svět — ověřuje kostru).

---

### 4.2 Mráz — ledový svět (`frost`)

**Identita.** Ledovce, tundra, zamrzlé moře a horké prameny. Město je ostrov
tepla v bílé pustině — z výšky roztátá, teple osvětlená skvrna ve sněhu.

**Hlavní pravidlo: teplo.** Budovy mají `heatDemand` a fungují v dosahu
**tepelné sítě** (obecná síť, typ `heat`).

* **Zdroje:** rašelinová pec (malá), dům s krbem (hřeje sám sebe a sousedy),
  parní generátor (velký, pálí rašelinu nebo uhlí, dává i proud),
  geotermální vrt (jen na horkém prameni, velký a zadarmo).
* **Přenos:** tepelná věž (relé), tepelné články (přenosné teplo pro
  vzdálené budovy — spotřebují se).
* **Mráz:** budova bez tepla **zamrzne** — výroba stojí, domy pojmou méně
  lidí, klesá spokojenost. Nic se nerozbije: po návratu tepla za pár
  sekund rozmrzne.
* **Vidět:** sníh v dosahu tepla roztaje (hnědá mokrá zem, cestičky), mimo
  zůstává bílý. Zamrzlá budova má jinovatku, nesvítí, nekouří.

**Vedlejší mechaniky.**
* **Polární noc:** rok má čtyři období; v zimě je noc dlouhá (délka dne
  z dat světa). Poptávka po teple ×1,5, skleníky potřebují proud na světlo.
  Konec polární noci je svátek (událost).
* **Vánice** (existuje): stejný jev jako písečná bouře — zasypává sněhem;
  chrání **sněžná pluhovna**.

**Suroviny**

| Surovina | Odkud | K čemu |
|---|---|---|
| Led (`ice`) | ledolom na ledovci | stavební materiál (izoluje) |
| Rašelina (`peat`) | rašeliniště v tundře | palivo pro pece a generátory |
| Lišejník (`lichen`) | sběr v tundře | krmivo skleníků → jídlo |
| Mrazový krystal (`frost_crystal`) | krystalový důl v ledových jeskyních | broušení, tepelné články |
| Broušený krystal (`cut_crystal`) | brusírna | optika, membrány, **vývoz** |
| Tepelný článek (`heat_cell`) | výrobna článků | přenosné teplo, **vývoz** (Nebesa) |

**Budovy (20)**

| Budova | Role | Co dělá | Vzhled |
|---|---|---|---|
| Ledový dům (`ice_house`) | bydlení | levný, málo tepla (izolace ledem) | kopulka z ledových kvádrů |
| Dům s krbem (`hearth_house`) | bydlení | hřeje sebe a nejbližší sousedy, pálí rašelinu | kamenný dům, kouřící komín |
| Obytná kopule (`habitat_dome`) 2×2 | bydlení | hustá, tepelně úsporná | prosklená kopule, teplé světlo |
| Termální věžák (`thermal_tower`) 2×2 | bydlení (luxus) | nejhustší; u geotermálního zdroje; chce perly | věž s parou kolem |
| Rašelinová pec (`peat_stove`) | pravidlo | malý zdroj tepla | zídka se žhnoucím ohništěm |
| Parní generátor (`steam_generator`) 2×2 | pravidlo | velký zdroj tepla i proudu | kotel, sloup páry |
| Geotermální vrt (`geothermal_bore`) | pravidlo | velký zdroj na horkém prameni | vrtná věž nad kouřícím jezírkem |
| Tepelná věž (`heat_relay`) | pravidlo | prodlouží dosah tepla | měděná věž, rozžhavené žebrování |
| Sněžná pluhovna (`snowplow_depot`) | pravidlo | chrání okruh před vánicí | garáž, pluhy, stopy ve sněhu |
| Ledolom (`ice_quarry`) | těžba | led z ledovce | modré kvádry, lana |
| Rašeliniště (`peat_cutter`) | těžba | rašelina | tmavé pruhy v tundře |
| Sběr lišejníku (`lichen_gatherer`) | těžba | lišejník | nízká chata, koše |
| Krystalový důl (`crystal_mine`) | těžba | mrazové krystaly | vchod do ledové jeskyně, modrý svit |
| Skleník (`greenhouse`) 2×2 | zpracování | lišejník + teplo + světlo → jídlo | v noci svítí zeleně |
| Brusírna krystalů (`crystal_cutter`) | zpracování | krystal → broušený krystal | dílna s třpytem |
| Výrobna tepelných článků (`heat_cell_works`) | zpracování | krystal + rašelina → články | dílna s oranžovými válci |
| Horké lázně (`hot_springs_spa`) | služba | spokojenost, v polární noci víc | kouřící bazénky ve sněhu |
| Polární observatoř (`aurora_observatory`) | služba | věda; při polární záři spokojenost | kopule s dalekohledem |
| Zimní přístav (`winter_harbor`) | obchod | přístav světa; led z moře drží ledoborec | molo s ledoborcem |
| **Srdce zimy** (`winter_heart`) 5×5 | div | obří geotermální jádro: teplo pro celé centrum, centrum nikdy nezamrzne | sloup teplého světla, polární záře zesílí |

**Řetězce.** rašelina → teplo (pece, generátory) · led → domy · lišejník +
teplo + proud → jídlo · krystal → broušený krystal (vývoz) · krystal +
rašelina → tepelné články (vzdálené budovy, vývoz).

**Výzkum (10).** izolace ledem, rašelinové pece, parní teplo, geotermální
vrty, skleníky, krystalografie, broušení, tepelné články, pluhy, Srdce zimy.

**Vzestup Mrazu (7).** lepší izolace (−poptávka tepla), delší dosah věží,
hlubší vrty, úsporné pece, zimní otužilost (menší propad spokojenosti
v noci), rychlé rozmrzání, jasnější záře.

**Události.** Polární záře (noc se spokojeností navíc) · Zamrzlá řeka
(dočasně se dá chodit po ledu — cesty napříč) · Ledová kra (kosmetika) ·
Návrat slunce (svátek po polární noci) · Lavina (dočasně zasype svah).

**Počasí.** sněžení a vánice (existují), mrazivá mlha, diamantový prach
(třpytivé částice v mrazu), jasná polární noc.

**Fauna.** stáda sobů, polární lišky, sněžné sovy; tuleni na kře u pobřeží.

**Hvězdy.** ★ 5 000 · ★★ celá polární noc bez jediné zamrzlé budovy ·
★★★ Srdce zimy · ✦ 20 000 obyvatel jen s geotermálním teplem.

**Obchod.** Vývoz: **broušené krystaly, tepelné články**. Dovoz: sklo
z Duny (velké skleníky), perly (termální věžák).

**Vzhled.**
* *Světlo:* nízké slunce celý den, dlouhé modré stíny, zlatá hodina trvá
  hodiny; v polární noci soumrak a měsíc.
* *Obloha:* bledě tyrkysová, v noci **polární záře** (vlnité zelenofialové
  pásy přes celou oblohu — nová vrstva oblohy).
* *Země:* sníh, modré ledovce, černé skály; roztáté kruhy kolem tepla.
* *Částice:* sníh, diamantový prach, pára z komínů a pramenů.
* *Architektura:* kopule, strmé střechy se sněhem, silné kamenné zdi,
  komíny, malá teple svítící okna.
* *Paleta:* bílá, ledově modrá, šedá, teplá oranžová jen u tepla, zelená
  u skleníků.
* *Mechanika vidět:* hranice sněhu = hranice tepelné sítě; zamrzlá budova
  má jinovatku a zhasne.

**Zvuk.** vítr, praskání ledu, praskání ohně, ticho sněžení, vzdálené vytí;
motiv: zvonkohra a hluboký sbor.

**Nové v kódu.** síť `heat` · stav „zamrzlá" (násobič podle pokrytí, jako
proud) · délka dne podle období z dat světa · `weather_burial` znovu
(vánice) · tání sněhu podle pokrytí sítě (render) · polární záře.

**Odhad:** 5–6 dní.

---

### 4.3 Souostroví — oceánský svět (`archipelago`)

**Identita.** Tropické moře s atoly, lagunami a korálovými útesy. Souše je
málo, každá dlaždice je vzácná. Město je rozeseté po ostrovech, spojené
trajekty, mosty a mělčinami, které se odhalují s odlivem.

**Hlavní pravidlo: příliv a odliv.** Cyklus (~10 herních minut) střídá
příliv a odliv na **přílivových mělčinách** (nový biom `tidal_flat`).

* Za **přílivu** jsou mělčiny pod vodou: budovy na nich musí stát **na
  kůlech** (příznak `stilted`), jinak vypadnou. Cesty přes mělčiny jsou
  zaplavené.
* Za **odlivu** se mělčiny odkryjí: sběr mušlí, pěší spojení mezi ostrovy,
  přístup k útesům.
* Technicky: dočasný přepis biomu na přílivových dlaždicích (systém přepisů
  biomu a `TerrainRevision` existují), hrubá mřížka, nízká frekvence,
  deterministicky ze seedu.
* **Vidět:** čára přílivu se posouvá, mokrý písek je tmavší, loďky se
  při odlivu kloní na bok.

**Vedlejší mechaniky.**
* **Tropické bouře a příboj** — zasahují pobřežní budovy (`weather_burial`
  s cílem „pobřeží"); chrání **vlnolam**, maják předpovídá.
* **Stavění na vodě** — plovoucí budovy na mělké vodě; **poldr** mění
  mělčinu na souš (`AutoTerraformSystem` existuje) — cesta k velkému městu.
* **Útesy dorůstají** — korálový lom útes vytěžuje, útes pomalu dorůstá
  (stejně jako dnešní dorůstání lesa); přetěžený útes na čas zbělá.

**Suroviny**

| Surovina | Odkud | K čemu |
|---|---|---|
| Korálový kámen (`coral_stone`) | korálový lom | stavební materiál |
| Bambus (`bamboo`) | bambusový háj | kůly, lehké stavby |
| Ryby (`fish`) | rybářské molo | → sušárna → jídlo |
| Mušle (`shells`) | sběr za odlivu | výzdoba, luxus, obchod |
| Perly (`pearls`) | perlová farma v laguně | luxusní bydlení všude, **vývoz** |
| Lano (`rope`) | provaznictví (bambus + kelp) | lodě, plovoucí domy, **vývoz** (Nebesa) |

Kelp a manganové konkrece (podmoří) už ve hře jsou a tady se hodí.

**Budovy (21)**

| Budova | Role | Co dělá | Vzhled |
|---|---|---|---|
| Kůlová chata (`stilt_hut`) | bydlení | na mělčinách i mělké vodě | chýše na kůlech, lávka |
| Kruhový dům (`round_house`) | bydlení | na souši, levný | kulatá doškovaná střecha |
| Plovoucí dům (`floating_house`) 2×2 | bydlení | na mělké vodě, chce lano | vor s domkem, ukotvený |
| Lagunová vila (`lagoon_villa`) 2×2 | bydlení (luxus) | hustá, chce perly a koření | vila nad lagunou, lampiony |
| Maják (`lighthouse`) | pravidlo | předpověď bouří + služba | bílá věž s otáčivým světlem |
| Vlnolam (`breakwater`) | pravidlo | chrání pobřeží před příbojem | kamenná hráz s pěnou |
| Poldr (`polder`) 2×2 | pravidlo | mění mělčinu v souš, dlaždici za kolo | větrné čerpadlo, kanálky |
| Přístaviště trajektu (`ferry_dock`) | pravidlo | spojí ostrovy — trajekt nahradí silnici přes vodu | molo, loďka pendluje |
| Loděnice (`shipyard`) 2×2 | pravidlo | trajekty a delší trasy mezi ostrovy | skluz s trupem lodi |
| Bambusový háj (`bamboo_grove`) | těžba | bambus | hustý zelený háj |
| Korálový lom (`coral_quarry`) | těžba | korálový kámen z útesu | plošina nad útesem |
| Rybářské molo (`fishing_pier`) | těžba | ryby | molo, sítě, loďky |
| Sběr mušlí (`shell_collector`) | těžba | mušle, jen za odlivu | košíky na mělčině |
| Plovoucí farma řas (`kelp_raft`) | těžba | kelp | vory s řasou |
| Sušárna ryb (`fish_drying_rack`) | zpracování | ryby → jídlo | stojany s rybami |
| Provaznictví (`ropewalk`) | zpracování | bambus + kelp → lano | dlouhá kryté dráha |
| Perlová farma (`pearl_farm`) | zpracování | perly v laguně | bóje a sítě v tyrkysové vodě |
| Přílivová elektrárna (`tidal_plant`) | energie | proud, nejvíc při změně přílivu; II. stupeň chce vzácné zeminy | turbíny v úžině |
| Plovoucí trh (`floating_market`) | obchod | přístav světa + služba | loďky se zbožím, plachty |
| Chrám mušlí (`shell_shrine`) | služba | víra + spokojenost | bílá stavba z korálů a mušlí |
| **Chrám přílivu** (`tide_temple`) 5×5 | div | v okruhu ustane příliv: mělčiny se stanou trvalou souší | chrám na útesu, kruhy klidné vody |

Podmořská kopule (`sea_dome`) z podmoří je dostupná i tady.

**Řetězce.** bambus + kelp → lano → plovoucí domy, lodě, vývoz · ryby →
jídlo · útes → korálový kámen → stavby · laguna → perly → luxus, vývoz.

**Výzkum (10).** kůlové stavby, rybolov, provaznictví, loďařství, trajekty,
perlařství, vlnolamy, poldry, přílivová energie, Chrám přílivu.

**Vzestup Souostroví (7).** vyšší kůly, rychlejší poldry, bohatší laguny,
pevné vlnolamy, rychlé trajekty, dorůstání útesů, klidná moře (slabší
příboj).

**Události.** Velký odliv (hodně mušlí, přístup k vraku) · Bělení korálů
(útes dočasně nedorůstá) · Návrat želv (svátek) · Vrak u pobřeží (náhodné
suroviny) · Cyklon sezóny (silná bouře, dlouhé varování).

**Počasí.** tropické jasno, déšť, bouřka (existují), cyklon (nový — silný
příboj).

**Fauna.** delfíni, mořské želvy, rejnoci **viditelní pod hladinou**, racci,
krabi na mělčinách za odlivu.

**Hvězdy.** ★ 5 000 · ★★ 200 dlaždic souše získaných z moře · ★★★ Chrám
přílivu · ✦ 5 cyklonů bez vyřazené budovy.

**Obchod.** Vývoz: **perly, lano**. Dovoz: vzácné zeminy z Výhně (přílivová
elektrárna II), sklo z Duny (lagunová vila).

**Vzhled.**
* *Světlo:* jasné tropické slunce, sytá modrá obloha s kupovitými mraky.
* *Voda:* gradient tyrkys → azur → hlubinná modř podle hloubky, útesy
  prosvítají, bílá pěna u pobřeží, **pohyblivá čára přílivu**.
* *Noc:* **světélkující plankton** ve vlnách podél pobřeží, lampiony na
  molech, hvězdy v klidné laguně.
* *Pohyb:* trajekty a rybářské loďky, palmy ve větru.
* *Architektura:* kůly, kulaté doškové střechy, bambus, lana, plachty,
  bílé korálové zdi.
* *Paleta:* tyrkys, korálová, bílá písková, sytě zelená, teakové dřevo.

**Zvuk.** příboj, racci, vítr v palmách, vrzání mol, bubny o svátku;
motiv: ukulele a marimba.

**Nové v kódu.** přírodní jev `tides` (přepis biomu v cyklu, příznak
`stilted`) · trajekty (spojení silniční sítě přes vodu) · příboj
(`weather_burial` s cílem pobřeží) · dorůstání útesů · biom `tidal_flat` ·
čára přílivu a plankton ve `WaterRenderer`.

**Odhad:** 6–7 dní.

---

### 4.4 Výheň — sopečný svět (`forge`)

**Identita.** Mladý svět sopek, čedičových plošin a horkých pramenů.
Nejbohatší rudy v galaxii, ale země tu žije. Město se naučí žít vedle ohně
— stavět hráze, svádět lávu a sklízet úrodu z popela.

**Hlavní pravidlo: erupce.** Hlavní průduchy (1–3 podle mapy) mají
**rozvrh** erupcí — deterministický ze seedu, s varováním minuty předem.

* Láva teče z průduchu **po spádu terénu** (výška terénu existuje:
  `ITerrain.ElevationAt`) po hrubé mřížce. Dráhy jsou předem spočitatelné
  a **seismická stanice je ukáže** v překryvu.
* Budova v cestě lávy **vypadne** na několik minut (nic se nezničí).
* **Lávová hráz** tok zastaví, **lávový kanál** ho svede po své dráze —
  puzzle „přesměruj řeku ohně".
* Ztuhlá láva = **nová čedičová zem**; po vychladnutí **úrodná sopečná
  půda** (bonus farem).
* **Vidět:** předpovězené dráhy jako oranžové čárkované pruhy, láva žhne
  a v noci osvětluje okolí, chladnoucí kůra tmavne.

**Vedlejší mechaniky.**
* **Popel** po erupci: sluneční a zemědělský výkon dočasně klesne, pak je
  půda úrodnější.
* **Fumaroly** — sirné výdechy jako zdroj síry (zvláštní dlaždice
  z generátoru).

**Suroviny**

| Surovina | Odkud | K čemu |
|---|---|---|
| Čedič (`basalt`) | čedičový lom, ztuhlá láva | stavební materiál |
| Síra (`sulfur`) | sirný důl u fumarol | žárobeton, chemie |
| Obsidián (`obsidian`) | obsidiánová dílna u lávy | nástroje (+těžba) |
| Ruda vzácných zemin (`rare_earth_ore`) | důl v sopečných žilách | rafinerie |
| Vzácné zeminy (`rare_earths`) | rafinerie | elektronika, turbíny, **vývoz** |
| Žárobeton (`pyrocrete`) | míchárna (čedič + síra) | hráze, kanály, odolné domy |

**Budovy (21)**

| Budova | Role | Co dělá | Vzhled |
|---|---|---|---|
| Čedičová chata (`basalt_hut`) | bydlení | levná | šestiboké čedičové sloupy |
| Kopulový dům z žárobetonu (`pyrocrete_dome`) | bydlení | odolný vůči popelu | šedá kopule, měděné větrání |
| Terasový dům (`terrace_house`) 2×2 | bydlení | na svazích, hustý | stupňovité terasy |
| Věž nad lávou (`lava_view_tower`) 2×2 | bydlení (luxus) | nejhustší, chce perly a sklo | štíhlá věž, okna žhnou |
| Seismická stanice (`seismograph`) | pravidlo | předpověď erupcí, ukáže dráhy lávy | anténa, ručička kmitá |
| Lávová hráz (`lava_wall`) | pravidlo | zastaví tok (segment) | masivní šedá zeď |
| Lávový kanál (`lava_channel`) | pravidlo | svede tok po své dráze (segment) | žlab s okraji |
| Chladicí věž (`quench_tower`) | pravidlo | v okruhu láva tuhne rychleji → dřív nová zem | věž s mlžnými tryskami, pára |
| Čedičový lom (`basalt_quarry`) | těžba | čedič | sloupy lámané do bloků |
| Sirný důl (`sulfur_mine`) | těžba | síra u fumaroly | žluté krusty, dým |
| Důl na vzácné zeminy (`rare_earth_mine`) | těžba | ruda | šachta s barevnými haldami |
| Obsidiánová dílna (`obsidian_workshop`) | zpracování | obsidián → nástroje (+těžba na celém světě) | dílna u okraje lávy |
| Rafinerie vzácných zemin (`rare_earth_refinery`) 2×2 | zpracování | ruda → vzácné zeminy | nádrže, trubky, fialové odlesky |
| Míchárna žárobetonu (`pyrocrete_mixer`) | zpracování | čedič + síra → žárobeton | rotující buben |
| Popelová farma (`ash_farm`) 2×2 | zpracování | jídlo; na úrodné půdě po erupci výnos ×2 | tmavá pole se zelenými řádky |
| Geotermální elektrárna (`geothermal_plant`) | energie | stálý proud | chladicí věže s párou |
| Magmatický reaktor (`magma_tap`) 3×3 | energie | obří proud u lávového jezera; při erupci stojí; II. stupeň chce uhlíková vlákna | sonda nad žhnoucím jezerem |
| Kovárna výhně (`forge_smithy`) | služba | +výroba kovových řetězců v okruhu | kovárna s výhní |
| Sopečné lázně (`volcanic_baths`) | služba | spokojenost | terasy s horkou vodou |
| Burza rud (`ore_exchange`) | obchod | přístav světa | kamenná budova s lanovkou k orbitě |
| **Kovadlina světa** (`world_anvil`) 5×5 | div | zkrotí hlavní průduch: jeho erupce tečou do kanálu a dávají energii a rudu místo výpadků | obří kovadlina nad kráterem, žhnoucí žíly |

**Řetězce.** čedič + síra → žárobeton → hráze, kanály, domy · ruda →
vzácné zeminy → vývoz · láva → nová zem → popelové farmy → jídlo ·
obsidián → nástroje.

**Výzkum (11).** vulkanologie, seismografie, žárobeton, hráze, lávové
kanály, sirné hutnictví, obsidián, vzácné zeminy, geotermální energie,
magmatická energie, Kovadlina světa.

**Vzestup Výhně (7).** přesnější předpověď (delší varování), pevnější hráze,
rychlejší tuhnutí, úrodnější popel, bohatší žíly, odolné stavby (kratší
výpadky), klidná země (méně erupcí).

**Události.** Probuzení vedlejšího průduchu · Nový ostrov z lávy (u pobřeží
vznikne souš) · Sirný déšť (krátce) · Vzácná žíla (bohaté ložisko) · Déšť
na lávě (obří oblaka páry — fotka).

**Počasí.** popelový spad (šedé vločky), suchá bouřka v popelovém mraku,
tetelení, déšť nad lávou (pára).

**Fauna.** salamandry vyhřívající se u lávy, ohniví ptáci, kozorožci na
svazích.

**Hvězdy.** ★ 5 000 · ★★ 5 erupcí za sebou bez jediné vyřazené budovy ·
★★★ Kovadlina světa · ✦ 300 dlaždic nové země z lávy.

**Obchod.** Vývoz: **vzácné zeminy**. Dovoz: uhlíková vlákna z Nebes
(magmatický reaktor II), perly a sklo (věž nad lávou).

**Vzhled.**
* *Světlo:* načervenalé, zakalené kouřem; v noci svítí země odspodu.
* *Obloha:* rezavá až kouřová, v popelovém mraku blesky.
* *Láva:* **svítící** tekutina s pomalu praskající kůrou, osvětluje okolí
  (zdroj světla v nočním renderu).
* *Částice:* popel jako šedý sníh, pára tam, kde láva potká vodu, jiskry.
* *Architektura:* masivní, hranatá, šestiboké čedičové sloupy, měděné
  střechy, valy.
* *Paleta:* antracit, rez, žhavá oranžová, sírová žlutá, zelené skvrny
  úrodné půdy.

**Zvuk.** dunění země, syčení páry, praskání chladnoucí lávy, hromy;
motiv: bicí a hluboké žestě.

**Nové v kódu.** přírodní jev `eruptions` (rozvrh, tok po spádu na hrubé
mřížce, hráze a kanály, tuhnutí, přepis biomu) · vrstva popela (hrubá
mřížka, dočasné násobiče) · překryv předpovězených drah · láva jako zdroj
světla · dlaždice fumarol v generátoru.

**Odhad:** 7–8 dní (nejsložitější simulace po Nebesích).

---

### 4.5 Nebesa — plynný obr (`gas_giant`)

**Identita.** Žádná země. Město visí v pásech mraků plynného obra na
plošinách nesených vztlakem; pod ním stovky kilometrů oblačného oceánu, nad
ním prstence a měsíce. Je to nejodlišnější svět galaxie — a technicky ten
nejodvážnější.

**Hlavní pravidlo: plošiny a vztlak.**

* Celá mapa je **obloha** (terén `cloud_sea`, není kam stavět). Stavět se
  dá jen na **nosné plošině** — kreslí se po dlaždicích jako silnice
  (vrstva plošin v `TileMap`).
* Každá dlaždice plošiny a každá budova na ní **váží**. Vztlak dávají
  **vztlakové budovy** (vaky, vodíkové nosiče, antigravitační kotvy) — je to
  **síť `lift`**: zdroj nese buňky kolem sebe, dokud stačí nosnost.
* **Přetížená plošina klesá:** budovy na ní vypadnou z provozu, dokud
  nepřibude vztlak nebo neubude zátěž. Nic nespadne — plošina jen visí níž.
* **Vidět:** klesající plošina tmavne, noří se do mraků a její **stín na
  mracích pod ní** se zkracuje (výška je čitelná ze stínu).

**Vedlejší mechaniky.**
* **Bouřkové pásy** — atmosférické pásy obra (jako pruhy Jupitera) se
  pomalu posouvají přes mapu. Pás bouře vyřadí budovy bez **bouřkového
  štítu** — ale **hromosvody** v pásu sklízí obří energii. Bouře je hrozba
  i hlavní zdroj proudu.
* **Vítr** — stálý silný vítr určitého směru: turbíny, vzducholodě,
  sběrače prachu.
* **Vzducholodě** spojují vzdálené plošiny (jako trajekty v Souostroví).

**Suroviny**

| Surovina | Odkud | K čemu |
|---|---|---|
| Vodík (`hydrogen`) | sběrač vodíku | vztlak, palivo, aerogel |
| Hélium-3 (`helium3`) | separátor z proudu vodíku | fúze v celé galaxii, **vývoz** |
| Uhlíkový prach (`carbon_dust`) | sběrač prachu ve větru | vlákna, aerogel |
| Uhlíkové vlákno (`carbon_fiber`) | tkalcovna | plošiny, lana, **vývoz** |
| Aerogel (`aerogel`) | aerogelová pec | lehké plošiny (menší váha) |
| Kovový vodík (`metallic_hydrogen`) | hlubinná sonda | antigravitační kotvy, div |

**Budovy (22)**

| Budova | Role | Co dělá | Vzhled |
|---|---|---|---|
| Nosná plošina (`platform`) | základ | stavební plocha (kreslí se po dlaždicích) | kovová mříž s zábradlím, lana dolů |
| Vztlakový vak (`lift_balloon`) | pravidlo | malý vztlak | pruhovaný balon uvázaný na lanech |
| Vodíkový nosič (`hydrogen_lifter`) 2×2 | pravidlo | velký vztlak, spotřebuje vodík | trojice protáhlých vaků |
| Antigravitační kotva (`antigrav_anchor`) 2×2 | pravidlo | obří vztlak, chce kovový vodík | prstenec levitující nad plošinou |
| Bouřkový štít (`storm_shield`) | pravidlo | chrání okruh v bouřkovém pásu; II. stupeň chce krystaly | stožár s modrou kupolí pole |
| Meteorologická věž (`weather_tower`) | pravidlo | předpověď pásů: kdy a kudy | věž s rotujícími anemometry |
| Kapsle (`sky_pod`) | bydlení | levné, lehké | kulatá kapsle s kruhovými okny |
| Závěsný dům (`hanging_house`) | bydlení | visí pod okrajem plošiny — bydlí se bez zabrání plochy nahoře | domek zavěšený pod hranou |
| Nebeská věž (`cloud_spire`) 2×2 | bydlení | hustá, těžká (chce víc vztlaku) | štíhlá věž s terasami |
| Oblačný palác (`cloud_palace`) 3×3 | bydlení (luxus) | nejhustší, chce perly a sklo | skleněné kopule, vlajky |
| Sběrač vodíku (`hydrogen_scoop`) | těžba | vodík hadicí z mraků | jeřáb s hadicí do hlubiny |
| Sběrač prachu (`dust_collector`) | těžba | uhlíkový prach ve větru | síťové plachty proti větru |
| Hlubinná sonda (`deep_probe`) 2×2 | těžba | kovový vodík z hloubky; v bouři stojí | vrátek s lanem mizejícím dolů |
| Separátor hélia-3 (`helium_separator`) 2×2 | zpracování | vodík → hélium-3 | stříbrné nádrže, modrý svit |
| Tkalcovna vláken (`fiber_loom`) | zpracování | prach → uhlíkové vlákno | stroj s cívkami černého vlákna |
| Aerogelová pec (`aerogel_kiln`) | zpracování | prach + vodík → aerogel | pec s mléčně modrými bloky |
| Aeroponická farma (`aeroponic_farm`) 2×2 | zpracování | jídlo z mlžení a světla | zavěšené zelené závěsy |
| Hromosvod (`lightning_harvester`) | energie | obří proud, když jím projde bouře | vysoký stožár, v bouři jiskří |
| Větrná turbína (`wind_turbine`) | energie | stálý proud podle větru | vrtule na výložníku |
| Dok vzducholodí (`airship_dock`) 2×2 | obchod | přístav světa; spojí vzdálené plošiny | věž s kotvící vzducholodí |
| Vyhlídková galerie (`sky_gallery`) | služba | spokojenost (výhled do bouře) | prosklená lávka nad propastí |
| **Oko bouře** (`storm_eye`) 5×5 | div | napojí město na Velkou bouři: trvalý obří proud a v okruhu utiší pásy | vír pod městem, uvnitř blesky |

Ze společných budov jsou dostupné jen ty, které nepotřebují zem a suroviny,
jež tu nejsou (sklady, věda, správa, služby) — a i ty stojí na plošinách.

**Řetězce.** prach → vlákno → plošiny · vodík → vztlak · vodík → hélium-3 →
vývoz · prach + vodík → aerogel → lehčí plošiny · hlubina → kovový vodík →
antigravitace, div · bouře → hromosvody → proud.

**Výzkum (11).** nosné plošiny, vztlak, vodíkové nosiče, separace hélia,
uhlíková vlákna, větrná energie, meteorologie, bouřkové štíty, bouřková
energie, aerogel, antigravitace (+ Oko bouře).

**Vzestup Nebes (8).** lehčí plošiny, silnější vaky, širší štíty, přesná
předpověď, bohatší hlubina, rychlé vzducholodě, bouřkové žně (+hromosvody),
výhled do propasti (+spokojenost).

**Události.** Průlet **nebeských velryb** (obří tvorové proplují pod
městem — spokojenost, fotka) · Blíží se Velká bouře (silný pás, dlouhé
varování) · Sestupný proud (část plošin dočasně ztratí vztlak, pokud se
nedoplní) · Zatmění měsícem · Duhová koróna (kosmetika).

**Počasí.** stálý vítr, bouřkové pásy s blesky, vodíkový déšť (kosmetika),
jasné horní nebe.

**Fauna.** **nebeské velryby** (obří pomalí plovouci tvorové — podpisový
tvor světa), svítící medúzovití plovci v noci, hejna mrakoplavců.

**Hvězdy.** ★ 5 000 · ★★ 10 bouřkových pásů bez jediné vyřazené budovy ·
★★★ Oko bouře · ✦ 1 000 dlaždic plošin.

**Obchod.** Vývoz: **hélium-3** (palivo pro fúzní reaktory na všech světech
— nová společná budova, odemkne ji první dovoz), **uhlíková vlákna**.
Dovoz: lano ze Souostroví (levnější první plošiny), broušené krystaly
z Mrazu (štíty II), tepelné články (hlubinné sondy), sklo a perly (palác).

**Vzhled.**
* *Bez země:* spodní vrstva je **oceán mraků v pásech** (krémová, okrová,
  rezavá, bílá) — 2–3 vrstvy s **paralaxou**, každá jinou rychlostí.
* *Výška:* plošiny vrhají **stín na mraky pod sebou**; posun stínu dává
  pocit výšky.
* *Obloha:* obří měsíce a prstenec přes oblohu; na obzoru nebo pod městem
  **Velká bouře** — vír se světélkujícími blesky uvnitř.
* *Světlo:* teplé, rozptýlené, jantarové; v bouřkovém pásu tmavne.
* *Noc:* blesky rozsvěcí mraky zevnitř, světla města se odrážejí v mracích,
  měsíce jasné.
* *Pohyb:* balony se pohupují, vzducholodě plují, vlajky ve větru.
* *Architektura:* lehká, tahová — kopule, stožáry, lana, plachty, balony;
  bílá, měď a sklo.
* *Paleta:* krémová, broskvová, rezavá, jantarová, noční indigo, bleskově
  fialová.

**Zvuk.** hukot větru, vzdálené hromy, vrzání lan, hluboký zpěv nebeských
velryb; motiv: táhlé smyčce a harfa.

**Nové v kódu.** terén `cloud_sea` bez stavitelné země · vrstva plošin
(`TileMap`) a jejich váha · síť `lift` a stav „klesá" · přírodní jev
`storm_bands` (pohyblivé pásy, deterministicky) · `CloudSeaRenderer`
(paralaxa) · stíny plošin na mracích · spojení vzducholoděmi · guvernérský
cíl „plošina a vztlak dřív než stavba".

**Odhad:** 9–11 dní (největší svět technicky).

---

### 4.6 Xeno — cizí svět (`xeno`)

**Identita.** Živoucí svět pod dvěma slunci, pokrytý bioluminiscenční
flórou, která se chová jako jeden organismus. Tady se nestaví — tady se
pěstuje. Finále galaxie.

**Hlavní pravidlo: flóra se šíří.**

* Z **hnízd** (zvláštní dlaždice z generátoru) roste flóra do sousedních
  volných dlaždic na hrubé mřížce, v pravidelných „tepech" (deterministicky).
* Flóra **přerůstá cesty** a pomalu **obaluje okrajové budovy** (vyřadí je,
  dokud se neodstřihnou). Nic se nezničí.
* Flóra je zároveň **zdroj**: sklizeň dává spory, vlákno a nektar.
* Hráč vyvažuje **prořezávání a pěstování**: prořezávač šíření zastaví
  a sklízí, sporová bariéra ho zablokuje v linii, **Strom života** flóru
  v okruhu zkrotí — tam se šíří „přátelsky" a budovám dává bonus.
* **Vidět:** okraj flóry se plazí (úponky), pulzuje; obalená budova má
  úponky přes střechu a zhasne.

**Vedlejší mechaniky.**
* **Budovy se pěstují** — stavba je růst: potřebuje výživu v okolí a čas;
  klíčírna ho zrychlí.
* **Symbióza** — část budov potřebuje flóru v sousedství, aby fungovala
  (a za to dává víc).
* **Dvě slunce** — dva překrývající se cykly dne: světlé, soumračné a tmavé
  fáze; flóra v různých fázích produkuje různě.

**Suroviny**

| Surovina | Odkud | K čemu |
|---|---|---|
| Spory (`spores`) | sběrna spor, prořezávání | pěstování budov, výživa |
| Živé vlákno (`living_fiber`) | pěstírna vlákna | stavební materiál |
| Nektar (`nectar`) | nektarová zahrada | jídlo |
| Luminiscin (`luminsap`) | luminiscenční pole | energie |
| Genom (`genome`) | genetická laboratoř | věda světa |
| Biokrystal (`biocrystal`) | líheň biokrystalů | **vývoz** — Galaktický div a finální stavby všude |

**Budovy (21)**

| Budova | Role | Co dělá | Vzhled |
|---|---|---|---|
| Houbový příbytek (`fungal_dwelling`) | bydlení | levný, vypěstovaný | houba s okénky, svítí |
| Živý úl (`living_hive`) 2×2 | bydlení | hustý; chce flóru v sousedství | pulzující buňkovitý úl |
| Korunní dům (`canopy_house`) 2×2 | bydlení | v koruně obřích rostlin, vysoký | domky v obřím květu |
| Symbiotická věž (`symbiont_spire`) 3×3 | bydlení (luxus) | kapacita roste s věkem; chce perly | spirálová věž porostlá světlem |
| Prořezávač (`pruner`) | pravidlo | zastaví šíření v okruhu a sklízí | krunýřový stroj s čepelemi |
| Sporová bariéra (`spore_barrier`) | pravidlo | blokuje šíření v linii (segment) | řada svítících pylových sloupků |
| Strom života (`life_tree`) 2×2 | pravidlo | v okruhu zkrotí flóru — bonus místo přerůstání | strom se zlatým světlem |
| Kořenová síť (`root_network`) | pravidlo | spojení budov jako cesta, kterou flóra nepřeroste | svítící kořeny v zemi |
| Klíčírna (`seed_vault`) | pravidlo | zrychlí růst staveb v okruhu | semeník s pulzujícími lusky |
| Sběrna spor (`spore_harvester`) | těžba | spory z flóry | nálevky nasávající oblak spor |
| Pěstírna vlákna (`fiber_nursery`) | těžba | živé vlákno | záhony s vlákny jako vlasy |
| Nektarová zahrada (`nectar_garden`) | těžba | jídlo | obří květy s kapkami |
| Luminiscenční pole (`glow_field`) 2×2 | energie | luminiscin → proud, nejvíc ve tmě | pole svítících baněk |
| Líheň biokrystalů (`biocrystal_hatchery`) 2×2 | zpracování | biokrystaly (vývoz) | krystalové geody, duhový svit |
| Genetická laboratoř (`gene_lab`) | zpracování | genom → věda; II. stupeň chce hélium-3 | organická kopule s DNA spirálou světla |
| Symbiotická dílna (`symbiont_workshop`) | zpracování | vlákno + spory → stavební tkáň | dílna jako mušle |
| Xenobiologický ústav (`xenobiology_institute`) 2×2 | služba | věda + spokojenost | mušlová kopule |
| Zahrada klidu (`serenity_garden`) | služba | spokojenost, v noci víc | svítící zahrada s jezírkem |
| Léčivý háj (`healing_grove`) | služba | služba zdraví, spokojenost | kruh stromů s mlhou |
| Signální květ (`signal_bloom`) 2×2 | obchod | přístav světa — rostlina „zpívá" k orbitě | obří květ mířící k nebi |
| **Matka stromů** (`world_tree`) 5×5 | div | flóra celého světa zkrotne: šíření už neohrožuje, celá mapa v noci září | obří strom, koruna přes půl obrazovky |

**Řetězce.** flóra → spory → výživa staveb · vlákno + spory → stavební
tkáň → budovy · nektar → jídlo · luminiscin → proud · genom → věda ·
líheň → biokrystaly → vývoz.

**Výzkum (10).** xenobotanika, symbióza, pěstované stavby, prořezávání,
sporové bariéry, kořenové sítě, luminiscence, genetika, biokrystaly, Strom
života (+ Matka stromů).

**Vzestup Xena (7).** pomalejší šíření, bohatší sklizeň, rychlý růst staveb,
širší Stromy života, zářivější pole, hlubší kořeny, souznění (symbiotický
bonus).

**Události.** Rojení spor (skok v šíření) · Velké kvetení (bonus surovin) ·
Zatmění dvou sluncí (tma, všechno svítí) · Probuzení hnízda (nové hnízdo) ·
Píseň lesa (flóra zpívá — svátek).

**Počasí.** unášené spory (částice), teplý déšť, bioluminiscenční mlha,
dvojitý východ slunce.

**Fauna.** svítící „motýlí medúzy", šestinozí pasoucí se tvorové, pernatí
stromoví plazi.

**Hvězdy.** ★ 5 000 · ★★ 10 tepů šíření bez obalené budovy a přitom ≥ 50 %
mapy zelené · ★★★ Matka stromů · ✦ 20 000 obyvatel bez prořezávačů (jen
Stromy života).

**Obchod.** Vývoz: **biokrystaly**. Dovoz: hélium-3 (genetická laboratoř
II), perly (symbiotická věž), broušené krystaly (xenobiologický ústav).

**Vzhled.**
* *Světlo:* **dvě slunce — dvojité stíny** (teplý oranžový a studený modrý,
  každý jiným směrem).
* *Obloha:* obří prstencová planeta přes půl oblohy, dva měsíce.
* *Flóra:* fialová, tyrkysová, purpurová; jemně „dýchá" (pomalá animace
  jasu), plazivý okraj s úponky.
* *Noc:* **všechno svítí** — flóra, budovy, fauna. Nejkrásnější noc
  v galaxii.
* *Částice:* spory, pyl, světlušky.
* *Architektura:* žádné rovné čáry — baňky, mušle, spirály, membrány.
* *Paleta:* violet, tyrkys, magenta, neonově zelené akcenty, perleť.

**Zvuk.** harmonické tóny flóry, bzukot, kapání, cizí zpěvy; motiv:
syntetické pady a zvonky.

**Nové v kódu.** přírodní jev `flora_spread` (buněčný růst na hrubé mřížce,
obalování budov, prořezávač, bariéra, Strom života) · pěstovaná stavba
(stavba spotřebuje výživu) · dvě slunce (`SceneLight` se dvěma směry,
`SoftShadow` dvakrát) · vrstva nebe s prstencem · symbióza (podmínka
sousedství).

**Odhad:** 8–9 dní.

---

## 5. Vizuál napříč světy

### 5.1 Profil atmosféry

Každý svět pozná hráč **podle světla dřív než podle budov**. Profil je v datech
(`atmospheres.json`) a čte ho render; vrstvy už existují (`SceneLight`,
`DayNightCycle`, `WaterRenderer`, `CloudLayerRenderer`, `WeatherRenderer`,
`LightsRenderer`, `GodRayRenderer`) — jde hlavně o parametrizaci. Domovina
dostane profil, který přesně odpovídá dnešnímu vzhledu (regresní snímek).

| Pole profilu | Příklad |
|---|---|
| barva a výška slunce během dne (klíčové body) | Duna: bílé poledne, zlatý soumrak |
| druhé slunce (směr, barva) | Xeno |
| délka dne podle období | Mráz: polární noc |
| přechod oblohy (zenit → obzor) | Nebesa: jantar → krémová |
| barva vody podle hloubky, pěna | Souostroví: tyrkys → azur |
| mlha (barva, hustota) | Mráz: mrazivá mlha |
| mraky (typ, hustota, rychlost) | Nebesa: pásy, paralaxa |
| částice (druh, hustota, směr) | Výheň: popel; Xeno: spory |
| noční obloha (hvězdy, záře, měsíce, prstenec) | Mráz: polární záře; Xeno: prstenec |
| emisivní terén (co svítí a čím) | Výheň: láva |
| zabarvení rozhraní (akcent HUD) | každý svět jiný akcent |

Loader ověří, že každý svět má profil a že odkazy (druhy částic, vrstvy
oblohy) existují.

### 5.2 Paleta, sprity a silueta

* Sprity se kreslí **v kódu** (`PixelCanvas`, `SpriteLibrary`) — to je
  výhoda: materiálové varianty jsou **záměna palety** (`GamePalette` na
  svět) a sdílené díly (kopule, stožár, komín, lano, oblouk) se skládají
  do nových budov.
* **Silueta rozhoduje**, protože ta se čte i z oddálení: ploché střechy
  a lapače (Duna), kopule a komíny (Mráz), kůly a kulaté střechy
  (Souostroví), šestiboké sloupy a valy (Výheň), balony a stožáry (Nebesa),
  baňky a spirály (Xeno).
* Každá budova dostane **noční variantu** (svítící okna, emisivní části) —
  noc je na každém světě jiná a patří k „wow".
* Rozsah: ~125 nových spritů. Plán: 1–2 dny kreslení na svět, sdílená
  knihovna dílů na začátku (fáze 0).

### 5.3 Mechanika svítí

Každé pravidlo má **vizuální stopu přímo na mapě** a **překryv** v nabídce
Pohled (jako dnešní překryv proudu):

| Svět | Stopa na mapě | Překryv |
|---|---|---|
| Duna | zelená půda kolem vodní sítě; zasypané budovy s pískem | dosah vody, zvodně |
| Mráz | roztátý sníh kolem tepla; jinovatka na zamrzlých | dosah tepla |
| Souostroví | čára přílivu, mokrý písek | časovač přílivu, zaplavované dlaždice |
| Výheň | žhnoucí láva, tmavnoucí kůra | předpovězené dráhy lávy, čas do erupce |
| Nebesa | klesající plošiny, stíny na mracích | vztlak vs. zátěž, příští bouřkový pás |
| Xeno | plazivý okraj flóry, úponky na budovách | příští tep šíření, zkrocené okruhy |

### 5.4 Mapa galaxie

* Klidné **hvězdné pole s paralaxou** (2–3 vrstvy hvězd, mlhovina).
* **Planety jako pixelové koule — a planeta je doopravdy tvoje mapa:**
  terén světa (biomy z generátoru) se jednou upeče do malé textury
  (např. 256×128) a promítne na kouli s terminátorem. Na **noční straně
  svítí světla tvého města** (z mapy hustoty). Nebesa mají pásy a vír,
  Xeno prstenec, Mráz bílé čepičky.
* Planety se pomalu otáčí; najetí ukáže kartu světa (pravidlo, hvězdy,
  populace, vývoz).
* **Obchodní trasy** jsou jemné oblouky; po nich plují malé lodě se
  zbožím (barva podle suroviny).
* Neodemčené světy jsou siluety s počtem potřebných hvězd.

### 5.5 Přechody

* **Start kolonizační lodi:** v kosmodromu Domoviny se loď zvedne, kamera
  ji následuje vzhůru skrz vrstvu mraků (vrstva mraků existuje) a přejde do
  mapy galaxie.
* **Sestup na planetu:** kamera klesá k planetě, prolétne mraky (každý svět
  má jiné: písečný opar, sněhová mlha, tropické kupy, popel, oblačné pásy,
  spory) a zastaví se nad městem.
* Přepínání mezi již založenými světy používá kratší verzi (1–2 s), aby
  nezdržovalo — a dá se přeskočit.

### 5.6 Oddálení a fotky

* Oddálený pohled (mapa hustoty) používá paletu světa — z výšky je každý
  svět poznat na první pohled (okrová, bílá, tyrkysová, černo-oranžová,
  krémová, fialová).
* Fotoreportér a video (existují) fungují na každém světě; profil
  atmosféry se propisuje i do nich. Každý svět má 2–3 „pohlednicové"
  momenty pro trailer (polární záře nad Mrazem, erupce ve Výhni, velryby
  pod Nebesy, noc na Xenu).

---

## 6. Zvuk a hudba

* Každý svět má ambient (`ambience.json` existuje): vrstvy prostředí,
  počasí a přírodních jevů (bouře, láva, příliv, šíření flóry).
* **Hudební motiv světa** — nástroj a nálada (loutna, zvonkohra, marimba,
  žestě, smyčce, syntezátor), aby se svět poznal i poslechem.
* Přírodní jevy mají **zvukové varování** dřív než vizuální (vzdálené
  dunění před erupcí, hvízdání větru před bouří).
* Mapa galaxie: tichý vesmírný ambient, ťuknutí při najetí na planetu.

---

## 7. Architektura a technika

### 7.1 Data

**Nové soubory:**

* `worlds.json` — definice světů: `id`, jméno a popis (klíče lokalizace),
  `order`, odemčení (`starsRequired`), předvolba generátoru, pravidla
  (`behaviors` + parametry), zapnuté sítě, zakázané budovy a kategorie,
  `startingKit`, `colonyCost` a jeho růst, vývozy, odkaz na profil
  atmosféry, paletu, ambient, faunu, počasí, hvězdy (podmínky ve formátu
  úkolů), vylepšení Vzestupu světa.
* `atmospheres.json` — profily atmosféry (5.1), včetně profilu Domoviny.
* `networks.json` — typy sítí (`power`, `water`, `heat`, `lift`): velikost
  buňky, pokles s dosahem, co znamená nedostatek (násobič výroby, stav
  „zamrzlá", „klesá"), barva překryvu, stopa na mapě.

**Značka světa v existujících souborech:** `resources.json`,
`buildings.json`, `tech.json`, `events.json`, `biomes.json`, `fauna.json`,
`weather.json`, `landmarks.json`, `achievements.json` dostanou volitelné
pole `worlds` (seznam ID). **Chybí-li, obsah platí všude** (společný) —
starý obsah tak funguje beze změny; svět si nechtěné zakáže sám.

**Validace při načtení (fail-fast):**

* každý odkaz na svět, síť, profil, jev existuje;
* budova s `waterDemand`/`heatDemand`/… patří světu, který tu síť má;
* **svět je rozjetelný**: s přistávacím modulem a startovní výbavou jde
  postavit bydlení, jídlo a první stavební materiál (kontrola dosažitelnosti
  řetězců — chybný svět spadne při startu, ne po hodině hraní);
* vývozy a dovozy odkazují na suroviny, které někde vznikají;
* hvězdy používají známé metriky.

### 7.2 Obecné sítě

Dnešní `PowerGridSystem` (buňky 8×8, šíření od zdroje, dokud stačí výkon,
nízká frekvence, jen po změně zástavby) se zobecní na **`NetworkSystem`
s typem sítě z dat**. Elektřina zůstane jedním z typů.

* **Regrese:** elektřina na uloženém městě musí dát **stejný výsledek jako
  dnes** (test porovná oba výpočty — stejná metoda jako u shluků sídel).
* Každý typ má zdroje (`supplies`), spotřebiče (`demands`) a relé
  (`relays`) z dat budov.
* Nedostatek se promítne podle typu: násobič výroby (voda, proud),
  stav „zamrzlá" (teplo), stav „klesá" (vztlak).
* Guvernér: dnešní `PowerGoal` se zobecní na `NetworkGoal` — postaví zdroj
  nebo relé tam, kde síť chybí.

> **Hotovo (D0.1).** `NetworkSystem` nahradil `PowerGridSystem`. Elektřina
> zůstala, kde byla — dosah v `gameplay.json` (blok `power`), budovy ve
> starých polích `powerSupply`/`powerDemand` — takže Domovina ani mody nic
> nepoznají; regresní test porovná obecnou síť s původní implementací bit po
> bitu na městě s devadesáti spotřebiči. Ostatní sítě jsou v `networks.json`
> (`range`, `shortage`: `slowdown` = výroba × pokrytí, `cutoff` = pod
> `cutoffBelow` budova vypadne se stavem „síť nedosáhne" a sama se vrátí)
> a budovy je píšou do `networks: { "water": { "supply", "demand", "relay" } }`.
> Relé doplní zbývající dosah na svůj; buňka dosažená později lépe se rozšíří
> znovu. Stavy „zamrzlá/klesá" jsou zatím jeden stav `NetworkShortage` —
> vzhled podle světa přijde s Mrazem a Nebesy.

### 7.3 Přírodní jevy

Každé pravidlo světa je **chování za behavior-ID** (CLAUDE.md): třída
v simulaci, parametry v datech.

| Behavior | Svět | Co dělá | Frekvence |
|---|---|---|---|
| `weather_burial` | Duna, Mráz, Souostroví | pás bouře vyřadí nechráněné budovy (písek, sníh, příboj) | při bouři, 1× za s |
| `tides` | Souostroví | přepis biomu přílivových dlaždic v cyklu | 1× za s |
| `eruptions` | Výheň | rozvrh, tok lávy po spádu, hráze, kanály, tuhnutí | tok 2× za s během erupce |
| `storm_bands` | Nebesa | pohyblivé pásy, vyřazení, sklizeň hromosvody | 1× za s |
| `flora_spread` | Xeno | buněčné šíření, obalování, prořezávání, zkrocení | tep 1× za 30 s |
| `platforms` | Nebesa | vrstva plošin, váha, návaznost na síť `lift` | po změně zástavby |

Všechny jsou **deterministické** (seed světa + tik), běží na **hrubé
mřížce** a **nízké frekvenci**, bez alokací v tiku (CLAUDE.md). Vyřazení
budov používá existující mechanismus poškození (odpočet výpadku), takže
výroba, guvernér i render ho už umí.

### 7.4 Terén a generátor

* Každý svět má **předvolbu generátoru** (`worldgen.json` rozšíření): sada
  biomů a jejich váhy, hladina moře, posun teploty, zvláštní prvky (zvodně,
  horké prameny, fumaroly, průduchy, hnízda, přílivové mělčiny).
* **Nebesa** mají zvláštní terén bez souše (`cloud_sea`) — generátor vrací
  jen „oblohu" s pásy pro render a jevy.
* Dynamické změny terénu (příliv, láva, flóra) jdou přes **přepisy biomu**
  (`SetBiomeOverride`, `TerrainRevision` — existují). `CachedTerrain` drží
  jen vygenerovaný terén, přepisy jsou zvlášť, takže se nic nezneplatňuje.

### 7.5 Galaxie

Nová vrstva nad simulací (čisté OOP, `Core/Galaxy/`):

* `GalaxyState` — světy, trasy, galaktické hodiny, galaktický Odkaz,
  sbírka hvězd.
* `WorldRecord` — ID definice, seed, stav (zamčený, dostupný, kolonie),
  souhrn (zásoby, toky, populace, bydlení, sklady), poslední návštěva,
  hvězdy, snímek simulace.
* `TradeRouteSystem` — trasy, kapacity, doba cesty, zboží na cestě.
* `ColonizationProject` — stupňový projekt v kosmodromu.
* **V paměti je vždy jen jedna `Simulation`** (aktivní svět). Ostatní jsou
  záznamy se snímkem a souhrnem.

### 7.6 Souhrnná simulace

* Toky se měří průběžně (klouzavé okno ze `SteadyTotal` evidence toků),
  ne až při odchodu — odchod je okamžitý.
* Souhrnné počítání je čistá funkce (zásoby, toky, čas, stropy) — snadno
  testovatelná a deterministická.
* Guvernér neaktivních světů staví jen při návratu po dlouhé nepřítomnosti
  (přes `OfflineCatchUp` s jeho stropem práce). Při krátkém přepnutí ne —
  přepnutí musí být rychlé.
* Cíl: přepnutí ≤ 2 s u města s 25 000 budovami; souhrnný krok všech
  neaktivních světů ≤ 1 ms.

### 7.7 Uložení hry

* Save se stane **kontejnerem galaxie**: sekce `galaxy` (stav galaxie, trasy,
  Odkaz, sbírka) + sekce `world:<id>` s úplným snímkem každého světa ve
  **stávajícím sekčním formátu** (v14 umí přidat sekci bez rozbití
  kompatibility).
* **Migrace:** save bez sekce `galaxy` se načte jako galaxie s jediným
  světem (Domovina) — hra vypadá a chová se přesně jako dřív. Test na
  starých savech (existující testy kompatibility savů se rozšíří).
* Velikost: každý svět má snímek o velikosti dnešního savu; šest měst ×
  jednotky MB je v pořádku. Komprese sekcí jako dnes.

### 7.8 Rozhraní

* **Obrazovka galaxie** (5.4), **karta světa**, **dialog kolonizace**
  (výběr místa přistání), **panel obchodních tras**.
* **Přepínač světů** v HUD (malé ikony planet vedle minimapy).
* **Překryvy** sítí a jevů (5.3) v nabídce Pohled.
* **Předpověď jevů** v HUD: čas do erupce, bouře, přílivu, tepu šíření.
* **Muzeum světů** (sbírka, hvězdy, fauna, rekordy).
* Akcent HUD podle světa (profil atmosféry).

### 7.9 Render

Nové: `PlanetRenderer` (galaxie), `CloudSeaRenderer` (paralaxa Nebes),
`LavaRenderer` (emisivní láva a kůra), `AuroraRenderer`, `SkyObjectsRenderer`
(měsíce, prstence, dvě slunce), `FloraRenderer` (šíření, úponky, pulzování),
`NetworkGroundEffect` (zelená půda, roztátý sníh), čára přílivu a plankton ve
`WaterRenderer`, dvojité stíny v `SoftShadow`. Všechno se peče do textur nebo
kreslí z hrubých mřížek jako dnešní vrstvy — výkon podle `plan-vylepseni.md`
(LOD, culling, bez alokací za snímek).

### 7.10 Guvernér

Guvernér už plánuje podle cílů a rolí z dat (`GovernorGoals`, `GovernorRoles`).
Pro světy přibudou cíle:

* `NetworkGoal` (zobecněný `PowerGoal`) — voda, teplo, vztlak;
* `ProtectionGoal` — větrolamy, pluhy, vlnolamy, štíty, hráze tam, kde jev
  škodí (role `protects_from:<jev>`);
* `PlatformGoal` — na Nebesích napřed plošina a vztlak, pak stavba;
* `FloraGoal` — na Xenu prořezávače a Stromy života podle tlaku šíření;
* hlášení „budova čeká na dovoz" + návrh trasy (trasy zakládá hráč).

Hands-off test (kap. 7.13) je zákon: guvernér musí na každém světě dojít
k první hvězdě sám.

### 7.11 Výkon a determinismus

* Jedna živá simulace; ostatní světy O(surovin).
* Jevy na hrubých mřížkách, nízká frekvence, bez alokací v tiku.
* Mapa galaxie: textury planet se upečou jednou na svět (a po velké změně
  města).
* Všechno ze seedu a tiků — stejný save dá stejný výsledek na každém
  počítači (dohánění, přepínání i obchod).

### 7.12 Lokalizace

Na svět zhruba 110 klíčů (budovy s popisy, suroviny, výzkum, události,
hvězdy, jevy, rozhraní) → ~660 klíčů × 5 jazyků (cs, en, de, pl, es).
Počítá se s tím v odhadech; klíče se přidávají stejným nástrojem jako dnes.

### 7.13 Testy

Podle CLAUDE.md test spolu s kódem, simulace izolovaně:

* **Sítě:** elektřina přes `NetworkSystem` = dnešní výsledek (regrese na
  uložených městech); každý typ sítě — dosah, nedostatek, relé, stav.
* **Jevy:** deterministické (stejný seed = stejný průběh); ochrana chrání;
  vyřazení se vrátí; láva respektuje hráze a kanály a tuhne; příliv
  střídá fáze; pásy se posouvají; flóra se šíří, prořezávač a Strom života
  fungují; přetížená plošina klesá a po doplnění vztlaku se vrátí.
* **Loader:** neznámé odkazy, síť u špatného světa, **nerozjetelný svět**,
  chybějící profil — srozumitelná chyba při startu.
* **Galaxie:** kolonizace odečte cenu a založí svět; obchod nic nevytváří
  ani neztrácí; souhrnná simulace odhadne produkci do 5 % (jako
  `TheEstimateCreditsWhatTheCityReallyProduces`); přepnutí je v limitu;
  hvězdy se udělují; galaktický Odkaz platí na všech světech.
* **Save:** kontejner galaxie přežije uložení a načtení; starý save se
  načte jako galaxie s Domovinou a hraje se stejně.
* **Hands-off běh** (nástroj `Playtest` existuje) pro každý svět: guvernér
  dojde k ★ bez hráče.
* **Smoke:** kolonizace, přepnutí tam a zpět, jeden jev na každém světě,
  mapa galaxie, přechody.

---

## 8. Plán implementace

Po svislých řezech (CLAUDE.md): každá fáze končí hratelným celkem.

| Fáze | Obsah | Odhad |
|---|---|---|
| **0 — Základy** | `NetworkSystem` (+regrese), `worlds.json` + značky + validace, kontejner galaxie v savu + migrace, přepínání a souhrnná simulace, obrazovka galaxie (planety z mapy), kolonizační projekt, profily atmosféry (Domovina = dnešní vzhled), knihovna dílů spritů | 8–10 dní |
| **1 — Duna** | celý svět + obchodní trasa Domovina ↔ Duna; první hratelná verze druhé kapitoly | 6–8 dní |
| **2 — Mráz, Souostroví** | dva světy, obchod mezi koloniemi, polární záře, příliv | 11–13 dní |
| **3 — Výheň, Nebesa** | erupce, plošiny a vztlak, bouřkové pásy, paralaxa mraků | 16–19 dní |
| **4 — Xeno a finále** | šíření flóry, dvě slunce, Galaktický div a epilog, Muzeum světů | 10–12 dní |
| **5 — Balanc a vyladění** | hands-off běhy všech světů, ceny, časy, lokalizace, trailerové záběry | 4–6 dní |

**Celkem ~55–68 pracovních dní** (≈ 11–14 týdnů). Je to víc než původní
odhad 6–9 týdnů — přibyl plynný obr (nejnáročnější svět) a víc než dvojnásobek
unikátních budov.

**Vydávání:** každá fáze od 1 dál se dá vydat samostatně jako aktualizace
(„nový svět: Duna", „nový svět: Mráz a Souostroví"…). Pro Steam je pravidelný
příliv nových světů silnější než jedna velká aktualizace.

---

## 9. Rizika a jak je krotit

| Riziko | Co s tím |
|---|---|
| Rozsah (~125 budov, 6 jevů) | svislé řezy; každý svět hratelný sám; knihovna dílů spritů ve fázi 0 |
| Balanc mezi světy | hands-off běhy a měřené časy do hvězd; dovoz jen jako bonus, ne blokáda |
| Guvernér nezvládne nové pravidlo | zobecněné cíle (`NetworkGoal`, `ProtectionGoal`); hands-off test jako podmínka hotovosti |
| Složitost savu a migrace | kontejner nad stávajícím sekčním formátem; testy na starých savech |
| Výkon přepínání | jedna živá simulace, souhrny O(surovin), krátké dotikání; měřený limit |
| Vizuální jednota šesti světů | profil atmosféry a paleta v datech; stejná knihovna dílů; regresní snímek Domoviny |
| Objem lokalizace | klíče průběžně s každým světem, ne na konci |
| Hráč se ztratí v galaxii | karta světa s pravidlem, předpověď jevů v HUD, Velké cíle i na koloniích |

---

## 10. Otevřené otázky (neblokují začátek)

1. **Jména světů** — Duna, Mráz, Souostroví, Výheň, Nebesa, Xeno jsou
   pracovní; do angličtiny např. Dune, Rime, Archipelago, Forge, Aether, Xeno.
2. **Obrana na koloniích** — návrh: vypnutá; jen jako volitelná výzva.
3. **Cizí města (NPC) na koloniích** — návrh: žádná, kromě Xena, kde by mohla
   být „hnízdní města" domorodé flóry (později).
4. **Mapa galaxie** — pevná (stejná pro všechny) nebo ze seedu savu? Návrh:
   pevné pořadí světů, rozmístění a vzhled planet ze seedu.
5. **Galaktický Odkaz — podrobnosti** — z čeho přesně body plynou (součet
   Vzestupů všech světů?) a co „opuštění" znamená v galaxii; rozhodne se ve
   fázi 0 podle toho, jak se bude hrát Duna.
6. **Strop měřítka kolonií** — stejný jako Domovina, nebo nižší (kolonie
   jako specializované světy, Domovina jako jediná planetární)?
