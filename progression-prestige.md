# Progression & Prestige — Páteř hry

*Pracovní dokument · verze 0.1*

Jak hra drží hráče dlouhodobě: progrese, prestige (Vzestup) a měřítko. Řeší i identitu hry (relax vs. management) a rozsah dema. Navazuje na všech sedm předchozích dokumentů — je to lepidlo, co z nich dělá hru. Skoro vše je **[návrh]**, čísla obzvlášť (viz sekce 6).

---

## 0. Tři vůdčí rozhodnutí

1. **Relax je výchozí, management je volba.** Bez zásahu civilizace roste sama (auto-stavba z dokumentu Living City drží chod). Kdo chce, sáhne dovnitř a optimalizuje; kdo nechce, sleduje a jen občas rozhodne. Hlavní „aktivní" moment relaxačního hráče = **kdy provést Vzestup a do čeho investovat Odkaz** (meta-vrstva), ne mikromanagement dlaždic.
2. **Prestige = zvětšení měřítka, ne ztráta.** Vzestup neboří tvou civilizaci jako trest — povyšuje tě na větší plátno. Každý Vzestup = o řád větší čísla a nový obsah. Reset tak **slouží touze po velikosti**, místo aby proti ní bojoval.
3. **Do late-late-game vede několik Vzestupů.** Miliardové aglomerace a futuristika jsou schválně za pár prestiží — hra je „optimálně nastavená" tak, aby se k opravdové velikosti muselo dorůst po vrstvách. To dává dlouhý, jasný cíl.

---

## 1. Tři vrstvy progrese

```
① BĚH (růst civilizace)   → vesnice → město → metropole; hlavní smyčka, běží i sám
② VZESTUP (prestige)       → civilizace „dozraje" a její vědění zaseje větší nástupkyni
③ META-STUPNĚ (škála)      → každý Vzestup odemkne větší měřítko a obsah; late-late až po pár Vzestupech
```

Vrstva ① je relaxační jádro. Vrstva ② je rytmus, co ho obnovuje. Vrstva ③ je dlouhodobý tah za velikostí.

---

## 2. Co je Vzestup (a co zůstává)

Vzestup je zarámovaný jako **vnitřní příběh**, ne „game over": tvá civilizace dosáhla vrcholu své éry a její znalosti, technologie a odkaz **zakládají grandióznější nástupkyni na větším světě**. Staré civilizace se stávají tvým trvalým dědictvím (Síň odkazu), ne smazanou prací.

| Zůstává (trvalé) | Resetuje se |
|---|---|
| **Odkaz** (meta-měna) a za ni koupené trvalé bonusy | aktivní mapa / rozestavěné město |
| Odemčené technologie a éry (nezačínáš od nuly) | aktuální zásoby surovin |
| **Vyšší strop měřítka** (populace, velikost mapy) | čerstvé plátno k růstu |
| Odemčený obsah (biomy, mechaniky, automatizace) | — |
| Síň odkazu (tvé minulé civilizace „navždy") | — |

> **Emoční pointa:** „vědění přetrvává, svět se rodí větší". Pocit „tohle jsem vybudoval" nezmizí — přesune se do trvalého dědictví a do toho, že další civilizace startuje mocnější a větší.

---

## 3. Měřítko jako jádro prestiže *(tvůj požadavek na „pocit velikosti")*

Každý Vzestup zvedne **strop měřítka o řád** — přesně to žene touhu po velikosti. Ilustrativní žebřík (čísla k doladění):

| Vzestup | Strop populace | Pocit | Obsah |
|---|---|---|---|
| **0** (první běh) | ~tisíce → ~100 tis. | vesnice → malé město | první éry, základní automatizace |
| **1** | ~miliony | pravé město | další éry, zóny, komba |
| **2** | ~desítky–stovky milionů | metropole / aglomerace | politiky růstu, megastruktury |
| **3** | ~miliardy | megaregion | futuristika, guvernér-vrstva |
| **4+** | 10 mld.+ / planetární | planetární civilizace | late-late-game, endgame spektákl |

- **Miliony a víc jsou schválně cíl, ne strop.** Chtěl jsi je vidět — dorazí kolem Vzestupu 1–2 a dál rostou. Late-late je planetární měřítko.
- Technicky to jde díky přístupu z Tech Stacku: populace = čísla + agregát, jednotlivci se renderují jen u kamery. Strop tedy zvedáme bez zabití výkonu.

---

## 4. Odkaz (meta-měna) a do čeho ho dát

Při Vzestupu se úspěch civilizace **zhustí do Odkazu** (dle dosažené populace, budov, milníků). Za Odkaz kupuješ **trvalé** výhody — to je hlavní strategická volba relaxačního hráče:

- **Násobiče produkce** — každá další civilizace produkuje víc od začátku.
- **Rychlost růstu** — populace i stavba startují svižněji (méně čekání v raných fázích, které už znáš).
- **Vyšší strop / větší mapa** — posun měřítka nad rámec základního žebříku.
- **Startovní balíčky** — začínáš s odemčenými érami / budovami / trochou surovin, ať se rychle dostaneš tam, kde tě to baví.
- **Odemknutí obsahu** — nové biomy, mechaniky, automatizační stupně, kosmetika.
- **Kvalita života** — vyšší výchozí automatizace (dřív se dostaneš do „relax" režimu).

> **Trik proti nudě z opakování:** každý další běh je **kratší a mocnější** v raných fázích (díky Odkazu), takže neomíláš pomalý začátek — proletíš známým a užíváš si nové měřítko a nový obsah.

---

## 5. Relax-first, management-optional *(vyřešená identita)*

- **Bez zásahu:** auto-stavba (Living City) staví domy, silnice a — po odemčení — vše ostatní dle výchozích politik. Hráč může jen sledovat, jak civilizace roste, a jednou za čas rozhodnout o Vzestupu. **Funguje to samo.**
- **S managementem:** kdo chce, ručně staví, maluje zóny, ladí řetězce a politiky, honí efektivitu. Vše opt-in — výchozí pravidla drží chod, i když se hráč nezapojí.
- **Kde žijí zajímavá rozhodnutí i pro relax hráče:** *kdy* provést Vzestup (dřív a častěji vs. dotáhnout běh výš) a *jak* utratit Odkaz. To je strategie, která nevyžaduje mikromanagement — přesně „relax, ale s hloubkou".

---

## 6. Křivka a tempo *(honestní část)*

Aby „musel párkrát prestige, než se dostane do late-late":

- **První Vzestup přijde brzy** (pár hodin) — naučí mechaniku a dá dopamin dřív. Ne až po desítkách hodin.
- **Diminishing returns u stropu** — u konce běhu růst zplošťuje → přirozená pobídka „už jsi vyčerpal měřítko téhle éry, povyš se". Ne trest, ale lákadlo.
- **Pozdější Vzestupy trvají déle, ale skáčou dál** (větší měřítko, víc obsahu).
- **Late-late za ~3–5 Vzestupů** — miliardy a futuristika schválně vzadu.
- **Soft-lock, ne hard-lock:** běžet dál bez Vzestupu jde, jen se růst zpomalí — Vzestup je lákavá cesta vpřed, ne povinnost.

> **Upřímně:** konkrétní čísla (stropy, ceny, násobiče, kadence) jsou **iterativní ladicí práce, co se nikdy nenavrhne jednou**. Tenhle dokument dává *strukturu a principy*; samotné křivky budeš dolaďovat celou dobu a potřebuješ testery brzy. Nevěř prvním číslům — jsou jen startovní.

---

## 7. Jak prodat „pocit velikosti" *(nejen velké číslo)*

Velikost je zážitek, ne jen údaj. Nástroje:

- **Zoom, co odhalí měřítko.** Přiblížíš = jednotliví lidé a auta; oddálíš = moře hustoty a světel po celém megaregionu. **Ten kontrast je to úžas.**
- **Noční pohled** na kontinent pokrytý světly (viz Living Map / Game Feel) — nejsilnější „wow" velikosti.
- **Splývající aglomerace.** Ve vyšších stupních pojmenovaná města srůstají v jednu souvislou megalopoli (viz Living City) — vidíš megaměsto, ne osady.
- **Milníkové spektákly** při překročení 1M, 10M, 100M, 1B — oslava + vizuální posun (napojení na Game Feel).
- **Uspokojivé formátování čísel** — plynulý přechod K → M → B → … s příjemným přetáčením.
- **Agregovaný pohyb** — proudy dopravy a světla tekoucí přes megaregion (LOD agregát).
- **Síň odkazu** — tvé minulé civilizace naskládané za sebou; vizuální důkaz, jak daleko jsi došel.

---

## 8. Demo na Steam = promakaná první éra *(tvůj nápad)*

Demo = **Vzestup 0, ale vyleštěný do lesku.** Ne osekaná plná hra, ale dokonalý vertikální řez prvního běhu:

- **Kompletní malý oblouk:** vesnice → městečko, první automatizace, první ochutnávka velikosti (první „wow" oddálení).
- **Maximum juice** hned od první vteřiny (Game Feel doc dopředu) — demo prodává *pocit*, ne rozsah.
- **Končí u první výzvy k Vzestupu** jako háček: „Povyš se a odemkni grandióznější svět" → výzva k wishlistu / plné verzi.
- **Prodává dvě věci naráz:** že se to skvěle *hraje* (první éra) a že je za tím *obří tah* (příslib měřítka). Přesně to konvertuje hráče z tvého itch publika na Steam wishlisty.

> Sedí to i na scope radu z minula: **postav první éru brilantně, vydej ji, sbírej wishlisty.** Demo je zároveň tvůj marketing i tvůj první milník vývoje.

---

## 9. Implementace (data-driven, jak jinak)

Nové typy definic zapadají do systému z Data-Driven doku:

```json
// ascension-tier
{ "id": "asc_2", "populationCap": 50000000, "unlocks": ["megastructures","growth_policies"],
  "legacyFormula": "sqrt(pop)/1000", "mapSize": "large" }

// legacy-upgrade
{ "id": "prod_mult_1", "cost": 5, "effect": { "productionMult": 0.25 }, "repeatable": true }

// milestone (napojení na Game Feel spektákl)
{ "id": "pop_1m", "threshold": 1000000, "celebration": "fireworks_major", "label": "Milion!" }
```

- **Odkaz a stropy jsou jen čísla** → meta-progrese je levná na výkon.
- **Velká čísla:** pro miliardy+ použij formátování a interně 64-bit / plovoucí reprezentaci; nepočítej milion jednotlivců, jen agregát (Tech Stack).
- Jádro kódu se nemění — nový stupeň měřítka = další JSON.

---

## 10. Rizika

- **Prestige může bojovat s „tohle jsem vybudoval".** Řešíme rámováním (Vzestup = zvětšení, ne smazání) + Síní odkazu. Drž to rámování silné, ať reset nebolí.
- **Balanc je nikdy nekončící.** Kadence Vzestupů a křivky rozhodnou, jestli je hra návyková, nebo otravná. Testeři brzy, čísla iterativně.
- **Autopilot nesmí znudit.** Když se vše řídí samo a není *žádné* zajímavé rozhodnutí, relax se zvrhne v nudu. Proto meta-vrstva (kdy Vzestup, kam Odkaz) musí nabízet skutečná rozhodnutí — to je pojistka proti „hra se hraje sama a je to jedno".
- **Pozor na příliš pomalý první Vzestup.** Kdyby první prestige trval věčnost, hráč mechaniku nepozná. Dej ji ochutnat brzy.
- **Měřítko =技術 disciplína.** Miliardové stropy fungují jen při důsledném agregát/LOD přístupu; jednotlivé entity nikdy neškáluj do miliard.

---

## 11. Jak to zapadá do celku

```
BĚH (Living City, relax-first)  ──roste k──►  STROPU MĚŘÍTKA
STROP  ──láká k──►  VZESTUPU (prestige, zvětšení plátna)
VZESTUP  ──dává──►  ODKAZ  ──kupuje──►  trvalé bonusy + větší měřítko + obsah
NĚKOLIK VZESTUPŮ  ──vede k──►  LATE-LATE (miliardy, futuristika, planetární civilizace)
DEMO  ──ukazuje──►  Vzestup 0 vyleštěný + příslib velikosti  ──►  wishlisty
```

Hra vrcholí tím, že po několika Vzestupech sleduješ planetární civilizaci o miliardách, rozprostřenou a rozsvícenou po nekonečné mapě, kterou řídí pravidla, jež jsi zadal — a kdykoli chceš, sáhneš dovnitř.

---

## 12. Otevřené otázky k doladění

- **Kolik Vzestupů celkem?** 4–5 do late-late je návrh; může jich být víc s jemnějšími skoky.
- **Jak moc resetovat?** Návrh drží tech/unlocky, resetuje mapu. Alternativa: nechat i část zástavby (měkčí reset). Testovat, co líp sedí pocitu.
- **Má být Vzestup jednoklik, nebo malé rozhodnutí** (co si vzít s sebou)? Volba = hloubka, ale i tření.
- **Odkaz — jedna měna, nebo víc vrstev** (meta-meta pro nejdéle hrající)? Zvážit až podle retence.
- **Kde přesně končí demo?** Přesně u prvního Vzestupu, nebo kousek za ním (ochutnávka většího měřítka)? Ovlivní konverzi.

---

*Návrh k iteraci. Struktura je pevná, čísla ne — ta se ladí testováním celou dobu vývoje.*
