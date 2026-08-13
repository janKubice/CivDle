# Nápady: tower defense z generátoru map

Zásobník nápadů na **druhou hru** postavenou na tom, co v CivDle už funguje —
generovaný svět, biomy, teraformace, silnice, budovy z JSON. Není to plán ani
závazek; je to materiál k probírání. Věci, u kterých si nejsem jistý, jsou
označené jako **otevřená otázka**.

---

## 1. O čem to je

Bráníš město na **procedurálně generované mapě**. Mezi vlnami město stavíš
a vylepšuješ, během vln ho hájíš věžemi. Dvě věci to mají odlišovat od stovky
jiných TD:

1. **Mapa je hádanka, ne kulisa.** Kopce, les, voda a kaňony mění, co věže umí.
   A terén se dá přetvářet — kanál, násyp, vypálený průsek jsou tahy, ne
   dekorace.
2. **Věže se skládají, nekupují.** Věž je podvozek + jádro + runy. Sílu nedělá
   „koupil jsem draze", ale **jak spolu věže reagují** a kudy létají střely.

Třetí věc je spíš důsledek: protože se mezi vlnami staví město, hráč pořád
rozhoduje mezi „další věž" a „další ekonomika". To je ta smyčka, kvůli které se
hraje dál.

---

## 2. Proč zrovna tohle

Většina práce na CivDle je v **generování a čtení světa**, ne v idle smyčce.
Tower defense z toho vytěží skoro všechno, ale postaví na tom úplně jiné tempo:
místo hodinového růstu tříminutová vlna, kde na terénu doopravdy záleží.

Co jde vzít prakticky beze změny:

| Z CivDle | K čemu v TD |
|---|---|
| Generátor světa, 24 biomů | Mapa jako hádanka; každý běh jiný |
| Teraformace (kanály, zvedání, zalesnění) | **Hlavní strategická akce mezi vlnami** |
| Silnice a jejich napojování | **Cesta nepřátel** — hráč ji staví sám |
| Budovy z JSON (registr, ceny, recepty) | Ekonomika města, věže jako další typ |
| Fog of war | Průzkum mapy před vlnou; „co přijde z téhle strany?" |
| Tolerantní savy | Uložení rozehraného běhu |
| LOD, culling, částice | Stovky nepřátel bez propadu snímků |
| Myra UI, lokalizace do 5 jazyků | Rozhraní od nuly nepíšeš |

Co je nové a co bude stát nejvíc práce: **pathfinding s hodně jednotkami**,
zásahy a projektily, stavové efekty a jejich reakce, vlny a balanc.

---

## 3. Komba: jádro celé věci

Inspirace Magicraftem není „taky ať to dělá barevné exploze", ale tohle:
**hráč skládá účinek ze součástek a hledá kombinace, na které autor nepomyslel.**
Přepis do TD navrhuju ve dvou vrstvách.

### 3.1 Vrstva A: stavy a reakce (co se děje na nepříteli)

Věže nedělají „poškození". Nanášejí **stavy**:

| Stav | Sám o sobě | Nosič |
|---|---|---|
| **Oheň** | poškození v čase | plamenomet, katapult se smolou |
| **Mráz** | zpomalení, po nasčítání zmrazí | ledová věž, mlhovina |
| **Blesk** | přeskočí na 2 nejbližší | tesla |
| **Mokro** | nic — jen zesiluje | vodní tryska, déšť, brod |
| **Dehet** | zpomalí a hoří dvakrát líp | dehtová past |
| **Kov** | „označí" cíl, přitahuje střely | magnetická věž |

A teď to podstatné — **reakce**:

- **Mokro + Blesk** → výboj projde *všemi* mokrými nepřáteli v okolí. Proti
  sevřenému houfu brutální.
- **Mokro + Mráz** → okamžité zmrazení. Zmrzlý nepřítel se stane **překážkou**
  a ostatní ho musí obejít. Přetváříš cestu uprostřed vlny.
- **Dehet + Oheň** → výbuch, zapálí i zem pod nohama na pár vteřin.
- **Mráz + fyzická rána** → *roztříštění*, násobné poškození. Odměna za dvě
  věže, které spolu mluví.
- **Oheň + Mráz** → pára: zakryje výhled *tvým* věžím. Vědomě špatná kombinace,
  aby nešlo mít všechno naráz.
- **Kov + Blesk** → zásah nikdy nemine a přeskočí dál.

**Proč zrovna takhle:** reakce dělá z rozestavění věží hádanku. Nestavíš „nejlepší
věž", stavíš *dvojice*. A nechat jednu kombinaci **škodit** (pára) je důležité —
bez trestu není skládání volba.

### 3.2 Vrstva B: průlet aurou (co se děje ve vzduchu)

Tohle je ta věc, kterou jinde nevidím a která by mohla hru odlišit:

> **Střela, která na cestě k cíli proletí aurou jiné věže, si její vlastnost
> odnese s sebou.**

Balistická věž sama o sobě dělá tupou ránu. Postav jí do dráhy ledovou mlhovinu
a začne střílet ledem. Postav dvě aury za sebou a nese obojí.

Důsledky, kvůli kterým to stojí za to:

- **Geometrie je gameplay.** Neřešíš „kam se vejde věž", ale „kudy poletí
  střely". Postavení věže o dvě políčka vedle je jiná stavba.
- **Terén tím dostane smysl.** Les blokuje výhled, kopec ho zvedá nad les —
  takže výška najednou není +10 % dostřel, ale povolení mít vůbec palebnou dráhu.
- **Aury jsou levné, jádra drahá.** Ekonomicky to tlačí k rozvržení, ne k jedné
  přerostlé věži.

**Otevřená otázka:** jestli je průlet čitelný. Hráč musí *vidět*, že střela
prolétla aurou — jinak je to skrytá matematika. Návrh: aura je viditelný pás,
střela v ní změní barvu a při zásahu vypíše, co všechno nese.

### 3.3 Runy: skládání jedné věže

Věž = **podvozek** (dostřel, kadence, počet slotů) + **jádro** (element) +
**runy**. Runy jsou modifikátory ve stylu Magicraftu:

- *Rozdvojení* — dva slabší projektily
- *Průraz* — projde prvním cílem dál
- *Zpožděná roznětka* — vybuchne až po vteřině (kombinuje s pomalými nepřáteli)
- *Při zabití* — spustí efekt další runy
- *Přetížení* — +100 % poškození, věž se po 5 ranách na chvíli vypne
- *Odraz* — střela se odráží od skal (a kaňon se stává zbraní)

**Runy jde přemísťovat mezi věžemi** kdykoli mezi vlnami, zdarma. Bez toho by
hráč dostal špatnou runu a měl běh zkažený; s tím je pozdní hra o **stavbě
stroje**, ne o loterii.

---

## 4. Terén jako zbraň

Každý biom má dělat něco mechanického. Ne bonus k číslu — pravidlo.

| Terén | Co dělá |
|---|---|
| **Vysočina, hory** | Věž vidí přes les. Stavba stojí dvojnásob. |
| **Les** | Blokuje výhled oběma směrům. **Hoří** — a oheň se šíří. |
| **Mokřad, brod** | Nepřátelé zpomalí a jsou **mokří** (zdarma reakce). Stavět nejde. |
| **Kaňon** | Přirozené hrdlo. Střely se odrážejí od stěn. |
| **Vulkanická půda** | Aura ohně zdarma. Průběžně poškozuje **tvoje** budovy. |
| **Led, sníh** | Nepřátelé **kloužou rychleji**. Mráz drží dvakrát dýl. |
| **Step, poušť** | Nic zvláštního = čisté dlouhé dráhy pro balistiku. |

A hlavně: **teraformace je tah**. Mezi vlnami za suroviny:

- **Vykopat kanál** → nová vodní překážka; nepřátelé buď obcházejí, nebo brodí
  (a jsou mokří).
- **Navršit kopec** → palebné stanoviště nad lesem.
- **Zasadit les** → clona, za kterou schováš křehké budovy.
- **Vypálit průsek** → otevřeš si palebnou dráhu, ale i nepříteli cestu.
- **Zavalit soutěsku** → přesměruješ celou vlnu jinam.

Tohle je nejsilnější věc, kterou CivDle nabízí zadarmo: teraformace už je
napsaná a otestovaná. V idle hře je to kosmetika navíc. Tady je to **hlavní
strategické sloveso**.

---

## 5. Cesta: silnice, které stavíš ty

CivDle už umí silnice, jejich napojování a hledání cesty po síti. Návrh:

> **Nepřátelé chodí po silnicích. Silnice stavíš ty.**

Není to tedy klasické „maze TD", kde hráč staví zdi. Je to diegetické: město
potřebuje silnice kvůli ekonomice (svoz surovin), a zároveň jsou to koridory,
kudy přijde útok. Každá nová cesta je **zisk i díra v obraně**.

Pravidla, aby to nebylo zneužitelné:

- Bez souvislé cesty k městu si nepřítel **prorazí vlastní** (pomalu, ničí, co
  mu stojí v cestě). Zabraňuje to „zazdím se a nic".
- Delší objížďka = víc času pod palbou, ale i **delší svoz** = slabší ekonomika.
  To je ta volba.
- Někteří nepřátelé silnice ignorují (viz níž).

**Otevřená otázka:** jestli je tohle čitelné pro hráče, který hru vidí poprvé.
Možná to chce v prvních vlnách cestu předkreslit a volnost pustit až později.

---

## 6. Město a jeho rozvoj

Bráníš **městské jádro**. Jeho životy = tvoje životy.

Mezi vlnami stavíš, a to na téže mapě, kde mají stát věže — **o místo se
soupeří**. To je jádro ekonomického rozhodování.

### Stupně rozvoje

Město má **Rozvoj** (1 → 10+). Každý stupeň:

- zvedne **strop budov** (kolik jich smíš mít — ne kolik si můžeš dovolit),
- odemkne **novou třídu budov** a s ní nová jádra a runy,
- zvedne základní příjem.

Strop místo pouhé ceny je důležitý: cena se dá přerůst, strop nutí **vybírat**.

### Suroviny

Držel bych tři, ne devět:

- **Peníze** — za každou zabitou jednotku. Utrácí se za věže a runy. *Rychlé.*
- **Věda** — z budov, ne z boje. Otevírá strom jader/run. *Pomalé.*
- **Materiál** — z terénu (les, lom). Platí teraformaci a stavbu. *Vázané na mapu.*

Tři suroviny = tři různá tempa a tři různé důvody, proč postavit budovu místo
věže. Devět surovin by z toho udělalo tabulku.

### Budovy (náměty)

| Budova | Co dělá |
|---|---|
| Tržnice | +peníze za zabití v okolí |
| Knihovna | +věda za vlnu |
| Pila / lom | +materiál, ale zabírá les/skálu, kterou možná chceš jako terén |
| Kovárna | +1 slot na runy všem věžím v dosahu |
| Hradby | zpomalí, dají se prorazit, dají se opravit |
| Maják | odhalí mlhu a ukáže **složení příští vlny** |
| Špitál | vrátí životy jádru mezi vlnami (draho) |

---

## 7. Nepřátelé jako protiargument

Každý typ má **rozbíjet jednu strategii**, ne jen mít víc životů:

- **Létající** — ignoruje silnice i překážky, letí vzdušnou čarou. Trestá
  labyrinty.
- **Podzemní** — vynoří se za první linií. Trestá „všechno na jedno hrdlo".
- **Obrněný** — imunní vůči poškození v čase. Trestá čistě ohnivé sestavy.
- **Rušič** — okolním jednotkám maže stavy. **Trestá komba** — hráč ho musí
  zabít první, jinak mu celá reakční mašinérie stojí.
- **Dělič** — po smrti dva menší. Trestá spoléhání na jednu velkou ránu.
- **Bořič** (boss) — přetváří terén: zasype kanál, srovná kopec. Bere ti tahy,
  které jsi udělal mezi vlnami.

Ten poslední mi přijde nejzajímavější: když je teraformace hlavní sloveso hráče,
je nejsilnější boss ten, který ho umí vzít zpátky.

---

## 8. Struktura běhu

Dvě cesty, každá jiná hra:

**A) Roguelite běh (30–45 minut).** Nová mapa, 20–25 vln, mezi vlnami nákup.
Po pádu si necháš trvalé odemčení (nová jádra, runy, podvozky). Sedí to
generátoru map — každý běh jiná hádanka — a je to nejlevnější na obsah.

**B) Kampaň nad jedním městem.** Město roste napříč mnoha vlnami, mapa se
rozšiřuje. Blíž CivDle, ale mnohem víc obsahu a balancu.

**Doporučuju A.** Generátor map je tvoje největší aktivum a roguelite z něj
vytěží nejvíc: pokaždé jiný terén = pokaždé jiný plán. Kampaň se dá přidat
později jako režim, opačně to nejde.

### Rytmus vlny

1. **Příprava** (bez limitu, dokud hráč nezmáčkne „připraven") — stavba,
   teraformace, přesun run.
2. **Vlna** (60–120 s) — hráč nesahá na stavbu, jen na aktivní schopnosti.
3. **Vyhodnocení** — co prošlo, kudy, co to zastavilo. Krátce, ale konkrétně.

Bod 3 je důležitější, než se zdá: bez zpětné vazby hráč neví, *proč* prohrál,
a nemá co zlepšit.

---

## 9. Co prototypovat první

Nedělat obsah. Ověřit, jestli je jádro zábavné — jednou mapou, čtyřmi věžemi:

1. **Jedna generovaná mapa**, jedna cesta, městské jádro. *(z CivDle skoro hotové)*
2. **Nepřátelé chodící po cestě**, jedna vlna, poškození jádra.
3. **Čtyři věže**: balistická, ledová aura, tesla, vodní tryska.
4. **Dvě reakce**: mokro+blesk, mokro+mráz.
5. **Průlet aurou** — a hlavně jeho **vizuál**.
6. **Jedna teraformace**: vykopat kanál.

Když s tímhle vzniká chuť „a co když dám auru sem", jádro drží a má smysl
stavět dál. Když ne, žádné množství obsahu to nespraví.

---

## 10. Rizika

- **Čitelnost komb.** Nejpravděpodobnější místo, kde to spadne. Hráč musí vidět,
  co se stalo a proč. Rozpočet na efekty a popisky ber jako součást mechaniky,
  ne jako leštění.
- **Výkon.** Stovky jednotek s hledáním cesty jsou něco jiného než agregovaná
  populace. Cesta se musí počítat jednou pro celou vlnu a sdílet, ne po
  jednotce.
- **Balanc komb.** Reakce mají tendenci se násobit a jedna kombinace pak vyhraje
  hru. Počítej s tvrdými stropy a s tím, že se to bude ladit dlouho.
- **Rozsah.** Tenhle dokument popisuje hru na roky. Prototyp z bodu 9 je na
  týdny. Ten rozdíl je celý vtip.
- **Dvě hry naráz.** CivDle není hotové. Tohle je zásobník na potom, ne
  odbočka na teď.

---

## 11. Drobnosti, které by to okořenily

- **Věž si pamatuje** — po X zabitích dostane vlastní jméno a drobný bonus.
  Levné na výrobu, silné na vazbu.
- **Počasí z CivDle**: déšť namočí všechno na mapě (blesk letí), mráz zamrzne
  vodu (kanál přestane fungovat jako překážka).
- **Noční vlny** — vidíš jen na dostřel svých věží. Maják dostane smysl.
- **Odměna za zbytečnost**: bonus za vlnu, kterou zastavila jedna jediná věž.
  Nutí to hráče zkoušet elegantní řešení místo hrubé síly.
- **Fotorežim a časosběr** už z CivDle jsou. Záznam vlny z ptačí perspektivy je
  materiál na sdílení zadarmo.
