# MVP & Roadmapa — Co postavit první

*Pracovní dokument · verze 0.1*

Chybějící dokument pro handoff: osm design doků říká *co hra obsahuje*, tenhle říká *v jakém pořadí to vzniká*. Bez něj neví tým (ani Claude), kde začít. Cíl MVP = **promakané demo = vyleštěná první éra** (viz Progression & Prestige, sekce 8).

---

## 0. Zásada: vertikální řez, ne všechno napůl

> Postav **jeden tenký, kompletní a zábavný** průřez hrou — ne deset systémů rozdělaných z poloviny.

Každá fáze níže je **hratelný přírůstek**: než se jde dál, musí být předchozí věc funkční a příjemná. Tím pádem máš v každém okamžiku něco, co jde spustit a otestovat — a demo vznikne přirozeně jako součet fází, ne jako velký třesk na konci.

---

## 1. Co je v demu (MVP) a co ne

| V demu ✅ | Odloženo na plnou verzi / update ❌ |
|---|---|
| Sim/render split + data-driven loader | vyšší éry T2–T6, kovy nad první, futuristika |
| 1 startovní biom (louka) + náznak druhého (les) | ostatní biomy (hory, poušť, pobřeží, tundra, bažina, džungle) |
| Jádro: klik → surovina → stavba → domy samy → dělníci | megastruktury |
| Krátký řetězec (dřevo→prkna, kámen, jídlo), éra T0–T1 | bydlení R4–R7 (paneláky, mrakodrapy, arkologie) |
| Soft pressure (jídlo jako spotřeba) | plné stupně automatizace (zóny, politiky, guvernér) |
| Juice: kácení, popy, čísla, pád stromu, zvuky | synergie distriktů, blueprinty, roční období, počasí-eventy |
| Auto-silnice + auto-domy (běží samo) | vlaky/lodě/letadla (demo = jen silnice) |
| Základní den/noc + trocha fauny/dekorací | combo meter, milníkové spektákly nad rámec prvního |
| První „wow" oddálení (ochutnávka měřítka) | další prestige stupně (demo ukáže jen první Vzestup) |
| Save/load | Steam integrace nad rámec nutného minima |
| Uzávěr: první výzva k Vzestupu jako háček | — |

> **Princip řezu:** demo prodává **pocit** (jak se to hraje) a **příslib** (že je za tím obří tah), ne rozsah. Jedna éra do lesku > pět ér napůl.

---

## 2. Build order (fáze)

| Fáze | Cíl | Hotovo, když… |
|---|---|---|
| **0 — Skeleton** | MonoGame projekt, sim/render smyčka, data-driven loader, vykreslení dlaždicové mapy a jednoho spritu budovy | z JSON se načte 1 surovina + 1 budova a objeví se na mapě |
| **1 — Klik + juice** | klik na strom → dřevo → postav budovu; první uspokojivá interakce | klik má třísky, zvuk, „+2" popup; strom se kácí a padá; budova „žuchne" na místo |
| **2 — Lidé + domy** | populace, přiřazování do prací, auto-domy dle poptávky, jídlo jako spotřeba | dělníci pracují, domy se staví samy, došlé jídlo zpomalí růst (soft pressure) |
| **3 — Řetězec** | 2–3 tierový řetězec + sklady; „hlídací" smyčka | vyschlý vstup viditelně zastaví navazující výrobu, sklad ji vyrovná |
| **4 — Auto-silnice + růst** | samostavitelné silnice, organický růst města, štítky distriktů/sídel | nová budova se sama napojí cestou; shluk se pozná jako distrikt/osada s jménem |
| **5 — Živá mapa + měřítko** | den/noc, trocha fauny a dekorací, oddálený agregátní pohled | v noci se město rozsvítí; oddálením vidíš hustotu a „wow" velikosti |
| **6 — Vzestup + leštění** | první Vzestup jako háček, milníková oslava, save/load, polish pass | demo má kompletní oblouk končící výzvou k Vzestupu; jde vydat |

Fáze 0–6 = **hotové demo**. Po něm se teprve otevírá plná verze (další éry, biomy, automatizace, prestige stupně).

---

## 3. Co dělat po demu (hrubý směr)

Pořadí po vydání dema, řízené zpětnou vazbou hráčů:

1. **Šířka obsahu** — další éry (T2–T4), první kovy, další biomy (hory, poušť) a jejich identita.
2. **Hloubka automatizace** — zóny → politiky růstu → guvernér (stupně z Living City).
3. **Měřítko a prestige** — vyšší Vzestupy, miliony → miliardy, megastruktury.
4. **Živost** — víc fauny, počasí-eventy, komba, blueprinty, časosběr historie.
5. **Steam meta** — Workshop (data-driven to umožňuje), cloud saves, achievementy.

> Přesné pořadí se řídí tím, co hráči po demu nejvíc chtějí — ne tímhle seznamem předem.

---

## 4. Co je k handoffu ještě fajn mít (ne blokující)

- **Master schéma obsahu** — konsolidace všech JSON typů z Data-Driven doku do jednoho referenčního souboru (usnadní práci, není blokující — schémata existují roztroušeně).
- **Art směr / asset spec** — jednotný styl, seznam potřebných spritů, zdroj (packy/zakázka/vlastní). **Připomínka: art je skrytá hora**, vyplatí se ho pojmenovat dřív než další design.
- **Coding standards / instrukce pro Clauda** — viz samostatný soubor `CLAUDE.md` (dodán zvlášť).

---

## 5. Rizika roadmapy

- **Scope creep zpět do fází.** Pokušení „přidat ještě jeden biom do dema" je smrtelné. Drž IN/OUT tabulku ze sekce 1 jako zákon.
- **Neleštit fázi 0–3 předčasně.** Dokud nejsou všechny fáze v kostře, neutápěj čas v detailech; polish přijde ve fázi 6.
- **Nezaseknout se na balancu.** Čísla ekonomiky a prestiže se ladí *průběžně s testery*, ne „až bude čas" (viz Progression, sekce 6).

---

## 6. Definice hotového dema

Demo je hotové, když:

- jde spustit kompletní oblouk **vesnice → městečko** bez zásahu (běží samo),
- **i s** volitelným ručním managementem,
- první minuta chytne juicem, oddálení dá „wow" velikosti,
- končí výzvou k Vzestupu jako háčkem na plnou verzi,
- a všechno je řízeno z JSON (žádný obsah natvrdo v kódu).

---

*Návrh k iteraci. IN/OUT tabulka je nejdůležitější — chraň ji před scope creepem.*
