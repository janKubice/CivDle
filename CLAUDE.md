# CLAUDE.md — Instrukce pro vývoj

Instrukce, kterými se Claude řídí při psaní kódu tohoto projektu. Drž se jich, pokud ti člověk výslovně neřekne jinak.

## Projekt

2D top-down idle city-builder (C# / .NET 8 / MonoGame). Relaxační jádro s volitelným managementem: hráč staví město, řetězce výroby, a civilizace se časem rozrůstá sama. Late-game = obří aglomerace o milionech+ obyvatel na (prakticky) nekonečné mapě. Progrese přes prestige (Vzestup = zvětšení měřítka).

Design je popsán v návrhových dokumentech — **přečti relevantní dokument, než začneš pracovat na dané oblasti.** Naimportuj je:

- @docs/tech-stack.md — architektura, engine, výkon
- @docs/data-driven-content.md — jak je obsah v JSON (definice vs. instance)
- @docs/mvp-roadmap.md — co stavět a v jakém pořadí
- @docs/content-design.md, @docs/buildings-design.md, @docs/living-map.md, @docs/living-city.md, @docs/game-feel-wow.md, @docs/progression-prestige.md — obsah a mechaniky

## Nejdůležitější pravidlo: data-oriented JÁDRO, čisté OOP KOLEM

Tyhle dva požadavky nejsou v rozporu, když je aplikuješ na správnou vrstvu:

- **Simulace (hot path) = data-oriented.** Entity jako `struct` v plochých polích (SoA / `struct[]`), zpracovávané systémy v dávkách. Žádné stromy objektů, žádné alokace za běhu v tikové smyčce, cache-friendly. Milion entit = milion malých struktur, ne milion tříd.
- **Všechno ostatní = čisté OOP.** Systémy, načítání obsahu, UI, nástroje, ukládání — normální třídy s jasnou zodpovědností, rozhraní, kompozice.

Když si nejsi jistý, do které vrstvy věc patří: je to v tikové smyčce simulace nad hodně entitami? → data-oriented. Jinak → OOP.

## Architektura a vrstvy

Drž striktní oddělení (viz tech-stack.md):

- **Simulace** nezná render ani obrazovku. Tiká ~10–20× za s.
- **Render** čte ze simulace, ale **nikdy do ní nezapisuje**. Běží ~60 FPS.
- **Content** (data-driven): definice se načtou z JSON do registrů při startu; instance v simulaci odkazují na typ přes `int` index, ne string.
- **UI** je oddělené od herní logiky.

Pravidlo: závislosti tečou jedním směrem (render → sim, ne obráceně). Když bys musel z renderu psát do simulace, je to špatně navržené.

## OOP a zodpovědnost

- **Jedna třída = jedna zodpovědnost (SRP).** Když popisuješ třídu slovem „a" („načítá a vykresluje a…"), rozděl ji.
- **Malé třídy a metody.** Preferuj kompozici před dědičností. Dědičnost jen na skutečné „is-a".
- **Rozhraní pro chování** (behavior-ID hooky z data-driven doku) — unikátní efekty (`flood_valley`, `terraform`) jsou implementace za rozhraním, mapované z JSON řetězce.
- **Dependency injection** místo globálních singletonů (kromě jasně odůvodněných výjimek). Systémy dostávají závislosti, netahají si je zpod ruky.
- **Neměnnost, kde to jde** — definice z JSON jsou `record`y, read-only.

## Data-driven: obsah do dat, ne do kódu

- Veškerý obsah (budovy, suroviny, vozidla, fauna, biomy, počasí, zvuky, distrikty, prestige stupně) je v **JSON**, ne natvrdo v kódu.
- **Data = co** (ceny, tiery, recepty, odkazy, sprity). **Kód = jak** (algoritmy, tik výroby, terénní efekty).
- Sprity se odkazují **cestou/ID**, ne binárkou.
- **Validuj při načtení (fail-fast):** chybný odkaz (neexistující surovina/sprite) = jasná chyba hned při startu, ne pád za hodinu hraní.
- Nikdy nedávej do JSON logiku (podmínky, vzorce). Když to tam láká, patří to do kódu nebo za behavior-ID hook.

## Testování

- **Piš testy spolu s kódem, ne po něm.** U simulační logiky ideálně napřed test (TDD), pak implementace.
- **Testuj každý systém izolovaně.** Simulace je deterministická (tiky, žádná náhoda bez seedu) → testovatelná: dej vstupní stav, odtikej, ověř výstup.
- **Content loader testuj** — validní JSON se načte, nevalidní spadne se srozumitelnou hláškou.
- Preferuj rychlé, čisté unit testy nad pomalými integračními, ale měj i pár integračních na klíčové smyčky (řetězec výroby, auto-stavba).
- Než ohlásíš hotovo, **spusť testy a build**; nehlas dokončení s padajícími testy.

## Dokumentace

- **XML doc komentáře** (`/// <summary>`) na veřejném API tříd a metod — hlavně *proč*, ne jen *co*.
- U každého modulu/systému krátké README nebo hlavičkový komentář: co dělá, jak zapadá do vrstev.
- Netriviální rozhodnutí (proč zrovna takhle) komentuj u kódu, ne mimo něj.
- Dokumentaci drž u kódu a aktualizuj ji se změnou; zastaralý komentář je horší než žádný.

## Výkon (viz tech-stack.md)

- **Žádné alokace v tikové smyčce** nad hodně entitami. Používej pooling (částice, dočasné objekty).
- **Nikdy nerenderuj ani nesimuluj miliony jednotlivců.** Populace = agregát (čísla); jednotlivé entity jen u kamery (LOD, culling).
- Systémy růstu (auto-stavba, land value, detekce distriktů) běží na **nízké frekvenci** a na **hrubých mřížkách**, ne každý tik/snímek.
- Velká čísla (miliony→miliardy): vhodná reprezentace a formátování, ne počítání jednotlivců.

## Závislosti — „no balast"

- Drž závislosti na minimu. Default: **MonoGame** (render/IO), **System.Text.Json** (data), případně **MessagePack** (save). UI: Myra.
- Novou knihovnu přidej jen s jasným zdůvodněním; napřed zvaž, jestli to nejde bez ní.

## Styl a git

- Standardní C# konvence (PascalCase typy/metody, camelCase lokály, `_camelCase` privátní pole). Nullable reference types zapnuté.
- Malé, tematické commity s jasnou zprávou. Jedna změna = jeden commit.
- Neupravuj generované soubory ručně.

## Jak Claude pracuje na tomto projektu

1. **Než začneš na oblasti, přečti relevantní design dokument** (viz importy nahoře).
2. **Stavěj po vertikálních řezech** dle mvp-roadmap.md — jedna věc kompletní a funkční, ne deset napůl.
3. **U velkých architektonických rozhodnutí se zeptej**, než je uděláš — nezaváděj velké vzory bez odsouhlasení.
4. **Piš test + implementaci + doc pohromadě.** Nehlas hotovo bez zeleného buildu a testů.
5. **Když něco v datech chybí, přidej JSON typ** — neřeš obsah natvrdo v kódu.
6. Když si nejsi jistý rozsahem nebo prioritou, řiď se `mvp-roadmap.md` (IN/OUT tabulka je zákon proti scope creepu).
