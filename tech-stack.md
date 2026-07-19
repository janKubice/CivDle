# Tech Stack — Idle City Builder

*Pracovní dokument · verze 0.1*

Technologická dokumentace pro plnohodnotnou Steam verzi navazující na tech demo (itch.io). Žánr: top-down 2D idle / city-builder s lehkou správou výrobního řetězce. Cíl: vizuálně rostoucí město až po civilizaci o milionech obyvatel na (prakticky) nekonečné mapě.

---

## 1. Vůdčí princip

Celý stack stojí na jednom rozhodnutí, které je potřeba držet od prvního commitu:

> **Simulace a vykreslování jsou dvě oddělené vrstvy, které o sobě vědí co nejméně.**

Populaci a ekonomiku *simuluješ jako čísla* (levné — milion je jen `int`). *Vykresluješ jen to*, co se vejde na obrazovku (pár tisíc spritů max). Milion agentů se nikdy nerenderuje, protože se na obrazovku nevejde. Tohle rozhodnutí je jediná věc, která rozhoduje o tom, jestli hra škáluje, nebo ne — všechno ostatní je vyměnitelné.

---

## 2. Jazyk a runtime

| Vrstva | Volba | Poznámka |
|---|---|---|
| Jazyk | **C# 12** | Moderní jazykové prvky (records, pattern matching, `Span<T>`). |
| Runtime | **.NET 8+ (LTS)** | LTS = stabilní podpora, silný JIT, dobrá práce s pamětí. |
| Styl kódu | **Data-oriented** | Struktury v plochých polích, ne stromy objektů — cache-friendly. |

Instinkt „čisté C#, data-oriented" je pro **simulační jádro** správný. Tam se vyplatí. Poznámka platí jen pro renderer (viz níže) — tam vlastní řešení nepiš.

---

## 3. Framework: MonoGame

**MonoGame** (nebo fork **FNA**) — framework, ne engine.

Proč právě tohle a ne vlastní renderer nebo těžký engine:

- **Žádný balast.** Dostaneš okno, vstup, zvuk, časování a odladěný `SpriteBatch`. Žádné node-stromy, žádný scéna-editor, žádná scene-graph vrstva, kterou nechceš.
- **Renderer zadarmo a otestovaný.** Batching spritů, texture atlasy a správa GPU jsou hotové a rychlejší, než co bys dohnal po večerech. Napsat vlastní vykreslovací pipeline = několik měsíců práce, kterou hráč neuvidí.
- **Ověřený žánrově.** Stardew Valley, Terraria, Celeste běží na XNA/FNA rodině — vizuálně bohaté 2D hry blízké tvému záměru.
- **Herní logiku píšeš celou sám.** ECS, UI, produkční řetězec — plná kontrola. Přesně „no balast" duch, jen renderer nevynalézáš znovu.

**Fallback:** kdyby později došlo na potřebu víc built-in nástrojů (editor, tilemapy, particly z krabice), je tu **Godot 4 s C#**. Na minimalistický přístup je ale MonoGame čistší.

---

## 4. Architektura: rozdělení SIM / RENDER

```
┌─────────────────────────────────────────────┐
│  SIMULATION LAYER  (pure C#, data-oriented)  │
│  · tik 10–20× / s (ne 60+)                   │
│  · populace, ekonomika, produkční řetězec    │
│  · plochá pole struktur, žádné GC spiky       │
│  · nezná MonoGame, nezná obrazovku           │
└───────────────────────┬─────────────────────┘
                        │  read-only snapshot
                        ▼
┌─────────────────────────────────────────────┐
│  RENDER LAYER  (MonoGame, 60 FPS)            │
│  · vykresluje jen viditelné chunky           │
│  · culling + LOD podle zoomu                 │
│  · panáčci / auta / vlaky = vizuální dekorace│
│  · nikdy nemění stav simulace                │
└─────────────────────────────────────────────┘
```

Render vrstva čte ze simulace, ale nikdy do ní nezapisuje. Díky tomu můžeš simulaci tikat jiným tempem než render, pauzovat ji, přetáčet offline progres, nebo ji celou přepsat, aniž bys sáhl na grafiku.

---

## 5. Simulační vrstva (detail)

- **Tik ~10–20 Hz.** Idle hra nepotřebuje simulovat na 144 FPS. Nižší frekvence = víc prostoru pro víc entit.
- **Data-oriented layout.** Entity jako struktury v souvislých polích (SoA / `struct[]`), ne třídy s referencemi. Cílem je, aby CPU četlo paměť sekvenčně.
- **Produkční řetězec jako graf zdrojů.** Uzly = budovy (produkují / spotřebovávají), hrany = tok materiálu. Hráčův „soft pressure" úkol = držet vstupy nad nulou, aby nevyschla navazující výroba (dřevo → nástroje → železo → …).
- **Populace jako agregát.** Milion lidí = statistiky (počty, spokojenost, poptávka), ne milion objektů. Jednotliví panáčci existují jen jako vizuál blízko kamery.
- **Offline progres.** Protože simulace je jen čísla, „přišel jsem po hodině a narostlo mi to" = jeden výpočet delty času, ne dohánění tiků.

---

## 6. Renderovací vrstva (detail)

- **Chunking mapy.** Svět rozdělený na chunky. V paměti aktivní jen ty kolem kamery; zbytek serializovaný jako souhrnný stav. Takto vznikne „nekonečná" mapa.
- **Culling.** Vykresluje se jen to, co je ve viewportu. Základní a nutné.
- **LOD podle zoomu:**
  - *Přiblíženo* → jednotliví lidé, auta, animace.
  - *Oddáleno* → agregovaná reprezentace (hustota, světla, tok dopravy), žádní jednotliví agenti.
- **Sprite batching** řeší MonoGame. Tvůj úkol je jen minimalizovat počet texture-swapů → **texture atlasy**.
- **Výkonová realita:** na obrazovce je vždy jen pár tisíc spritů. To je pro moderní GPU nuda. Skutečná zátěž je simulace v pozadí, ne render.

---

## 7. UI

Herní svět kreslí MonoGame; HUD/menu potřebují vlastní řešení (MonoGame UI z krabice nemá).

| Možnost | Kdy zvážit |
|---|---|
| **Myra** | Lehká retained-mode UI knihovna přímo pro MonoGame. Dobrý default. |
| **Vlastní immediate-mode UI** | Když chceš plnou kontrolu a UI je jednoduché (idle hry mají hodně panelů s čísly — jde to i ručně). |
| **EmptyKeys / jiné** | Jen pokud narazíš na limity Myry. |

Doporučení: začni s **Myrou**, ať neztrácíš čas skládáním tlačítek. Custom UI jen tam, kde je potřeba něco specifického.

---

## 8. Perzistence / save systém

- **Formát:** binární serializace (ne JSON) kvůli velikosti — milionová civilizace je hodně dat.
- **Knihovna:** **MessagePack for C#** (rychlá, kompaktní, malý balast) nebo vlastní binární writer, pokud chceš plnou kontrolu.
- **Struktura save:** aktivní chunky detailně + zbytek světa jako agregát + globální ekonomika + časové razítko (pro offline progres).
- **Verzování save souborů** řeš od začátku — přidej hlavičku s číslem verze, ať pozdější patch nerozbije uložené hry.

---

## 9. Podpůrné knihovny (drž seznam krátký)

| Účel | Volba | Poznámka |
|---|---|---|
| Serializace | MessagePack-CSharp | Save/load. |
| Audio | z MonoGame | Zvuk zvládne framework sám. |
| Šum / procedurální mapa | vlastní / malá noise lib | Pro generování nekonečného terénu. |
| Steam | **Steamworks.NET** | Achievementy, cloud saves, statistiky (viz níže). |

Princip: každou závislost si zasluž. Míň knihoven = míň balastu, míň konfliktů, míň updatů.

---

## 10. Steam integrace

- **Steamworks.NET** — C# wrapper nad Steamworks SDK.
- Minimum pro launch: achievementy, statistiky, **Steam Cloud** pro save soubory.
- Zvážit později: workshop (pokud bys chtěl moddování), rich presence.
- Steam Cloud pozor na velikost save — u milionové civilizace hlídej kvótu.

---

## 11. Nástroje a build

- **IDE:** Visual Studio / Rider / VS Code.
- **Verzování:** Git. Pro binární assety zvaž **Git LFS**.
- **Asset pipeline:** MonoGame Content Pipeline (build texture atlasů, zvuků).
- **Cílové platformy:** Windows primárně; MonoGame umí i Linux/macOS, ale testuj cíleně, ne „zdarma".

---

## 12. Hlavní rizika a jak jim předejít

| Riziko | Prevence |
|---|---|
| **Renderování „milionu" entit** | Nikdy nerenderuj populaci. Jen čísla + LOD. Rozhodni hned. |
| **Psaní vlastního enginu místo hry** | MonoGame jako renderer. Neřeš batching, okna, vstup ručně. |
| **GC spiky ze simulace** | Data-oriented struktury v polích, minimum alokací per tik. |
| **Scope creep z tech dema** | Definuj MVP: co je jádro (idle loop + 1 řetězec) a co je koření. |
| **Save soubory bez verzování** | Hlavička s verzí od prvního uloženého stavu. |

---

## 13. Shrnutí stacku

```
Jazyk / runtime  →  C# 12 · .NET 8 LTS
Framework        →  MonoGame (fallback: Godot 4 C#)
Simulace         →  pure C#, data-oriented, tik 10–20 Hz
Render           →  MonoGame SpriteBatch · chunking · culling · LOD
UI               →  Myra (+ custom kde třeba)
Save             →  MessagePack-CSharp, binární, verzované
Steam            →  Steamworks.NET
Verzování        →  Git (+ LFS pro assety)
```

**Jádro filozofie:** simulace jsou čísla, render je iluze. Drž je oddělené a všechno ostatní je jen detail.
