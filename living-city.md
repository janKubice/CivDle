# Living City — Růst města a automatizace

*Pracovní dokument · verze 0.1*

Jak město staví hráč, jak se staví samo, jak roste přirozeně a jak se automatizace odemyká v čase. Nejtěžší a nejdůležitější systém hry. Navazuje na **Budovy** (spojení, hustota, distrikty), **Living Map** (terén), **Game Feel** (uspokojení z růstu), **Data-Driven** (nové typy) a **Tech Stack** (výkon). Skoro vše je **[návrh]**.

---

## 0. Vůdčí myšlenka: ze stavitele guvernérem

Celý systém stojí na jednom oblouku:

> **Na začátku hráč staví cihly. Na konci vládne principy.**

Early game = ruční, zapojené, uspokojivé (stavíš, klikáš, cítíš to). Late game = hráč nastavuje **pravidla** a civilizace se řídí sama; on jen sleduje, jak roste, a upravuje směr. Tenhle přechod **builder → plánovač → guvernér** je přirozená idle progrese a řeší napětí „staví se to samo" vs. „chci si stavět sám": obojí platí, jen v jiné fázi. Automatizace se **odemyká**, není od začátku — takže hráč má vždy důvod se zapojit, a pak si zvolit klid.

---

## 1. Co se staví samo a co ručně (výchozí stav)

| Prvek | Od začátku | Pozdější odemčení |
|---|---|---|
| **Silnice** | staví se **samy** k budovám; hráč může kreslit ručně | manuál jako „vodítko", které auto respektuje |
| **Domy** | staví se **samy** podle potřeby populace | hráč ladí hustotu/tier přes pravidla |
| **Těžba, průmysl, speciál** | **ručně** — hráč rozhoduje kde a co | auto-stavba dle cílů a zón |
| **Služby** | ručně | auto dle potřeb distriktu |
| **Celé kolonie** | ručně založené | auto-zakládání dle politiky expanze (governor) |

Logika: **infrastruktura a bydlení jsou nuda na ruční klikání**, tak se dělají samy hned. **Produkce je zajímavé rozhodnutí**, tak zůstává ruční — dokud si hráč nezaslouží klid.

---

## 2. Samostavitelné silnice

- **Jak:** když vznikne budova (ručně i automaticky), systém najde nejlevnější cestu od nejbližší existující silnice / sítě a natáhne ji. Respektuje terén — obchází hory, mosty přes řeku stojí víc (viz Budovy, spojení).
- **Přirozený vzhled:** silnice se **vlní podle terénu**, ne rigidní mřížka — pokud hráč zrovna nezvolil plánovaný distrikt s mřížkou (viz sekce 8).
- **Auto-upgrade:** silnice se podle vytížení povyšuje (pěšina → dlažba → dálnice; viz Budovy 4.3). Ucpaný úsek si řekne o vylepšení sám.
- **Ruční má přednost:** cesty nakreslené hráčem systém bere jako pevná vodítka a napojuje se na ně. Hráč tak může „naznačit záměr" a nechat zbytek na automatu.

---

## 3. Samostavitelné domy

- **Poptávka tlačí růst:** rostoucí populace vytváří „tlak na bydlení". Když překročí kapacitu, systém najde vhodné volné místo (u silnice, u služeb, u existující zástavby) a postaví dům.
- **Přirozené shlukování:** domy se lepí k cestám a službám jako skutečná sídla — vzniká zástavba podél silnic (ribbon development) a postupné zaplňování mezer, ne rovnoměrný koberec.
- **Tier podle místa:** blízko centra / u služeb roste časem vyšší tier (činžáky, paneláky, mrakodrapy); na okrajích nižší (domky). Tohle žene **land value** (viz sekce 7) — hustota se skládá sama a vypadá organicky.
- **Materiál limituje:** auto postaví jen to, na co jsou suroviny — takže tvoje výrobní řetězce pořád rozhodují (soft pressure zůstává).

---

## 4. Odemykání automatizace *(páteř progrese)*

Postupný přechod od ruční stavby k vládnutí principy. Každý stupeň = nová „hračka" a nová úroveň klidu.

| Stupeň | Hráč dělá | Automat dělá |
|---|---|---|
| **1 — Stavitel** | staví vše kromě domů a silnic | domy, silnice |
| **2 — Cíle výroby** | nastaví cíl („drž 5 pil") | staví/udržuje daný typ, jak dovolí suroviny |
| **3 — Zóny** | maluje zóny (obytná/průmysl/farmy) | zaplní zónu vhodnými budovami |
| **4 — Politiky růstu** | nastaví pravidla („expanduj k lesům", „drž 20 % zeleně", „prioritizuj hustotu") | civilizace se sama rozrůstá dle pravidel |
| **5 — Guvernér** | řídí impérium principy | zakládá a rozvíjí celé kolonie automaticky |

> **Idle paradox pod kontrolou:** kdyby se vše stavělo samo hned, hráč nemá co dělat. Proto se automatizace **odemyká** — než ji získáš, ruční stavba tě baví; pak si volíš klid. Vždy je důvod hrát aktivně i důvod nechat to běžet.

---

## 5. Distrikty *(tvůj nápad)*

Když se shlukne dost budov stejného typu, oblast se **automaticky rozpozná jako distrikt**.

- **Detekce:** systém průběžně hledá shluky (5+ továren = Průmyslová čtvrť, blok obytných = Rezidenční čtvrť, trhy = Tržní distrikt, pole = Zemědělský pás).
- **Identita:** distrikt dostane **vizuální tvář** (jemné zabarvení země, cedule, prapory) a **jméno** (auto-generované, hráč může přejmenovat) → svět dostává orientaci a hráč vztah k místům.
- **Synergie [návrh]:** shluknutí dává **bonus** (5+ továren = efektivita), ale i **stinnou stránku** (znečištění → nespokojenost → potřeba parku/služby). Tím je shlukování skutečné rozhodnutí, ne jen kosmetika — a propojí to se službami z Budov.

---

## 6. Detekce osad a měst *(tvůj nápad)*

Shluk budov + populace nad prahem, propojený sítí, se **rozpozná jako sídlo** a roste v hierarchii:

```
osada  →  vesnice  →  městečko  →  město  →  velkoměsto  →  metropole
```

- Každý stupeň se odemkne dosažením populace / velikosti a dostane **jméno** (auto-generované), **centrum** (radnice) a **štítek na mapě**.
- Vyšší stupeň **odemyká možnosti** — metropole unese budovy, které vesnice ne (napojení na tiery z Budov).
- **To je ta civilizační fantazie:** oddálíš mapu a vidíš pojmenovaná města roztroušená po nekonečné krajině, propojená silnicemi a železnicí. Přesně tvůj původní obraz „samostatná civilizace rozprostřená po obří mapě".

---

## 7. Jak to vypadá přirozeně *(klíč k „hezky a přirozeně")*

Přirozený vzhled **není náhoda — je to důsledek omezení.** Náhoda vypadá chaoticky; omezená emergence vypadá organicky. Nástroje:

- **Respekt k terénu.** Města se lepí k pobřeží, vinou se údolími, obcházejí hory. Vypadá to „ručně navržené", protože to sleduje krajinu.
- **Land value (hodnota místa).** Podkladové skalární pole: u služeb, parků a vody = žádané (roste hustota, vyšší tier); u průmyslu = levné (nižší zástavba). Mrakodrapy vyrostou tam, kde je hodnota vysoká — jako ve skutečnosti.
- **Ribbon + infill.** Zástavba nejdřív podél cest, pak zaplňování mezer → přirozený vývoj.
- **Město ukazuje svou historii.** Staré jádro = smíšené tiery, novější okraje = jednotnější. Věk zástavby dělá město uvěřitelným.
- **Organické křivky + občasná mřížka.** Většina roste organicky, plánované distrikty mají mřížku — kontrast působí realisticky.
- **Drobné nedokonalosti.** Lehké natočení domu, zvlnění cesty (přes variaci z Data-Driven) — dokonalá pravidelnost vypadá mrtvě.

---

## 8. Moje další nápady *(nechal jsi prostor — tady je)*

Věci, které by systém pozvedly a sedí na tvůj koncept:

1. **Blueprinty / šablony.** Hráč jednou navrhne čtvrť (rozložení domů, cest, služeb), uloží jako šablonu, a auto-stavba ji „razítkuje". Spojí ruční kreativitu s automatizací — hráč tvoří *styl*, ne každý dům.
2. **Ghost-náhled záměru.** Auto-stavba ukáže „duchy" toho, co hodlá postavit; hráč může schválit / vetovat. Automatizace pak nikdy nepůsobí, že bojuje proti hráči.
3. **Osobnosti růstu.** Presety, jak auto-růst vypadá: „Organická", „Plánovač mřížky", „Zelená utopie", „Průmyslový gigant". Mění estetiku i chování → znovuhratelnost.
4. **Mřížka vs. organika per zóna.** Někdo miluje řád, někdo bujení. Nech hráče zvolit charakter pro každou zónu.
5. **Časosběr / historie.** Přehraj si růst civilizace od začátku jako zrychlené video. Obrovský emoční payoff pro idle hru, kam se vracíš sledovat pokrok.
6. **Satelitní sídla.** Když město vyčerpá místní prostor, auto-systém založí opodál satelit propojený železnicí → vysvětluje organické rozlézání po nekonečné mapě.
7. **„Founder" momenty.** Vznik nového města = malá oslava + pojmenování (napojení na Game Feel juice).
8. **Vizualizace dopravy.** Síť pulzuje tokem; ucpané tepny se samy povyšují. Uspokojivé i funkční.
9. **Jemné pobídky místo trestů.** Přehuštěný distrikt si „řekne" o park nebo službu (notifikace / auto-návrh). Soft pressure, žádný trest — konzistentní s anti-frustrací.
10. **Landmarky a zdroje kotví růst.** Města přirozeně rostou k pobřeží, k surovinám, kolem landmarků (jako v historii) — posiluje přirozenost i identitu míst.
11. **Megastruktura přetváří distrikt.** Div světa nebo přehrada přeskládá okolí kolem sebe → dramatické, čitelné momenty (napojení na megastruktury).
12. **Prosperita → estetika.** Bohatý, spokojený distrikt časem „zkrásní" (upravené fasády, zeleň, světla); zanedbaný zešedne. Vizuál vypráví stav ekonomiky beze slov.

---

## 9. Implementace (jak na to bez zabití výkonu)

- **Auto-stavba je systém na simulační vrstvě** (data-oriented, viz Tech Stack): každých pár tiků vyhodnotí *poptávku + land value + politiku* a vydá „stavební příkazy". Neběží každý snímek.
- **Silnice = A\*** na dlaždicové mřížce, cachované a inkrementální (přepočítává jen dotčenou oblast).
- **Land value = řídké skalární pole** (hrubší mřížka), aktualizované občas, difuzí od služeb a nešvarů. Levné.
- **Detekce distriktů / sídel = periodický clustering** (flood-fill na hrubé mřížce), ne každý tik.
- **Vše datově řízené** (viz Data-Driven) — nové typy definic:

```json
// district-type
{ "id": "industrial", "triggerCount": 5, "buildingTypes": ["factory","smelter"],
  "bonus": { "efficiency": 0.15 }, "penalty": { "happiness": -0.1 }, "tint": "industrial_gray" }

// settlement-tier
{ "id": "city", "minPopulation": 50000, "unlocks": ["skyscraper","university"], "labelStyle": "city" }

// growth-policy
{ "id": "expand_to_forests", "priority": "wood", "densityTarget": 0.7,
  "greenspaceMin": 0.2, "layout": "organic" }

// blueprint
{ "id": "suburb_a", "size": [8,8], "cells": [ /* domy, cesty, park */ ] }
```

Jádro kódu se nemění — přidání politiky nebo typu distriktu je jen další JSON (přesně proč jsme šli data-driven).

---

## 10. Výkon

- Systémy růstu běží **na nízké frekvenci** (každých N tiků) a na **hrubých mřížkách** — „civilizace se řídí sama" je levné, protože jsou to čísla a občasné průchody, ne práce za snímek.
- Pracuje se jen v **aktivních / načtených chuncích** (viz Tech Stack); vzdálená města jsou agregovaný stav.
- Stavební příkazy se dávkují (pár za tik), ať nevznikne špička, když poptávka vyskočí.

---

## 11. Rizika *(nejtěžší systém = největší opatrnost)*

- **Tohle je nejtěžší část hry.** Procedurální růst, co vypadá dobře, je vážně obtížný. Počítej s iterací a nečekej, že první verze bude krásná.
- **Začni jednodušeji než vize.** MVP = auto-silnice + auto-domy + zbytek ručně. Stupně automatizace (zóny, politiky, guvernér) přidávej v updatech. Nestav celý governor systém dřív, než je jádro zábavné.
- **Agentura hráče musí být čitelná.** Automatizace nikdy nesmí působit, že přebíjí hráče. Ruční stavba vždy vyhrává, auto zaplní zbytek. Ukazuj záměr (ghosty), umožni veto.
- **Přirozenost = omezení, ne náhoda.** Terén a land value dělají organický vzhled; čistý random dělá nepořádek.
- **Pozor na idle paradox.** Gatuj automatizaci za progresi, ať má hráč pořád co dělat, a klid ať je odměna, ne výchozí stav.

---

## 12. Jak to zapadá do celku

```
POPTÁVKA + LAND VALUE + POLITIKA  ──►  AUTO-STAVBA (domy, silnice, později vše)
AUTO-STAVBA  ──respektuje──►  TERÉN (Living Map) + RUČNÍ vstup hráče
SHLUKY budov  ──►  DISTRIKTY (identita + synergie)
DISTRIKTY + populace  ──►  SÍDLA (osada→metropole, civilizační fantazie)
ODEMYKÁNÍ automatizace  ──►  builder → plánovač → guvernér (idle progrese)
```

Celá hra vrcholí tím, že sedíš, sleduješ pojmenovaná města růst po nekonečné mapě podle pravidel, která jsi zadal — a kdykoli chceš, sáhneš dovnitř a postavíš něco sám.

---

## 13. Otevřené otázky k doladění

- **Jak brzy odemknout první auto-stavbu produkce?** Moc brzy → hráč se nudí; moc pozdě → ruční klikání unaví. Ladit s tempem.
- **Mají distrikty dávat mechanické bonusy, nebo být hlavně vizuál?** Bonusy = hloubka, ale i balanc navíc.
- **Kolik kontroly nad auto-růstem chce cílový hráč?** Relaxační publikum možná chce míň páček, ne víc. Testovat.
- **Blueprinty do launche, nebo update?** Skvělá funkce, ale kus práce; kandidát na po-vydání.
- **Časosběr historie — stojí za implementaci?** Emočně silné, ale technicky netriviální (ukládat historii růstu).

---

*Návrh k iteraci. Tohle je nejtěžší systém — ber sekci 11 vážně a stav ho po vrstvách.*
