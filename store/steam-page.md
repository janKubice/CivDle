# CivDle — podklady pro Steam stránku

Všechno, co Steamworks při zakládání stránky chce, na jednom místě. Texty jsou
**anglicky** (to je jazyk obchodu); český překlad je pod každou sekcí, kdyby ses
rozhodl přidat i českou lokalizaci stránky.

Obrázky se generují ze hry — viz `store/README.md`.

---

## Základní údaje

| Pole ve Steamworks | Hodnota |
|---|---|
| Name | CivDle |
| Developer | *(doplň)* |
| Publisher | *(doplň)* |
| Release date | *(doplň)* |
| Franchise | — |

---

## Short description (max 300 znaků)

> A calm idle city-builder on an endless map. Place a lumber camp by the forest,
> a quarry under the cliffs, and let your town grow while you're away. Seasons
> turn, roads find their own way, and every ascent rebuilds the world one scale
> larger.

*(283 znaků včetně mezer.)*

**Česky:** Klidný idle city-builder na nekonečné mapě. Postav pilu k lesu, lom
pod skály — a nech město růst, i když u toho nejsi. Střídají se roční období,
silnice si najdou cestu samy a každý Vzestup přestaví svět o měřítko větší.

---

## About the Game

Do Steamworks se vkládá s BBCode. Níže je verze i se značkami.

```bbcode
[h2]A city that keeps going without you[/h2]
CivDle is an idle city-builder for people who want to think, not to grind. You
lay out the first few buildings, and the town takes it from there — houses go up
where they're needed, roads connect themselves, settlements get names. Come back
in an hour and something has happened.

[h2]Where you build matters[/h2]
A sawmill at the forest edge cuts more than one out in the steppe. A quarry under
the cliffs outproduces one on the plain. Every producer tells you what it wants
from its surroundings, and shows the bonus [i]before[/i] you place it — so
choosing a spot is a decision, not a formality.

[h2]Distance costs[/h2]
Goods don't teleport. A mine far from any depot ships slowly, so storage stops
being a bigger number and becomes a place to build around. Remote colonies still
work — they just work harder until you give them a warehouse.

[h2]Four seasons, one rhythm[/h2]
Spring grows, summer feeds, autumn is for gathering, winter takes wood for heat
and thins the harvest. Nothing dies and nobody starves; the year simply asks you
to think a season ahead.

[h2]Wonders take time[/h2]
Megastructures aren't an expensive click. Lay the foundations and a construction
site appears on the map, rising a little at a time — with nothing to give until
it's finished. Then the whole city feels it.

[h2]Ascend and start bigger[/h2]
When a scale runs out, you ascend: the world resets one size larger, and the
upgrades you bought stay with you. Village, city, metropolis, mega-region — the
map never ends, only your patience for the current one.

[h2]Made to be left alone[/h2]
No timers demanding attention, no punishment for logging off, no way to lose.
Progress while away is credited when you return. The pressure is always soft:
run out of something and the city slows down, never collapses.
```

**Česky (kdyby ses rozhodl přidat českou stránku):**

- **Město, které jede i bez tebe** — rozestavíš prvních pár budov a dál si to
  vezme samo: domy vyrostou, kde chybí, silnice se napojí, osady dostanou jména.
- **Na místě záleží** — pila u lesa nese víc než pila ve stepi, lom pod skálou
  víc než lom na pláni. Bonus vidíš dřív, než položíš.
- **Vzdálenost stojí** — zboží se neteleportuje. Důl daleko od skladu svá­ží
  pomaleji, takže sklad je bod, kolem kterého se staví.
- **Čtyři období** — jaro roste, léto živí, podzim se sbírá, zima topí dřevem
  a ubírá polím. Nikdo neumírá, jen musíš myslet o období dopředu.
- **Divy světa se stavějí** — megastruktura není drahé kliknutí. Na mapě stojí
  staveniště a roste; dokud nestojí, nedává nic.
- **Vzestup** — když měřítko dojde, svět se přestaví o velikost větší a koupené
  bonusy ti zůstanou.
- **Dělaná na to, aby ses vrátil** — žádné budíky, žádný trest za odchod,
  nedá se prohrát.

---

## Tags (pořadí je důležité, Steam váží prvních pár)

1. Idler
2. City Builder
3. Base Building
4. Resource Management
5. Automation
6. Relaxing
7. Sandbox
8. Simulation
9. Strategy
10. Singleplayer
11. Procedural Generation
12. Pixel Graphics
13. 2D
14. Top-Down
15. Colony Sim
16. Casual
17. Management
18. Economy
19. Family Friendly
20. Offline

## Genres

Simulation, Strategy, Casual, Indie

## Categories (Steamworks „Supported features")

- Single-player
- Steam Achievements *(hra je má, jen se musí namapovat ve Steamworks)*
- Partial Controller Support — **ne**, hra je na myš a klávesnici
- Steam Cloud — *volitelné; savy jsou v profilu uživatele, dá se doplnit později*

## Jazyky

| Jazyk | Rozhraní | Titulky | Dabing |
|---|---|---|---|
| English | ✅ | — | — |
| Čeština | ✅ | — | — |

---

## System requirements

**Minimum (Windows)**
- OS: Windows 10 64-bit
- Procesor: dvoujádro 2,0 GHz
- Paměť: 4 GB RAM
- Grafika: cokoli s DirectX 11 (integrovaná stačí)
- Místo na disku: 200 MB

**Doporučené (Windows)**
- OS: Windows 11 64-bit
- Procesor: čtyřjádro 3,0 GHz
- Paměť: 8 GB RAM
- Grafika: dedikovaná, DirectX 11
- Místo na disku: 200 MB

**Linux**
- OS: Ubuntu 22.04 nebo novější
- Grafika: OpenGL 3.0 (linuxový build jede na OpenGL)
- Zbytek jako Windows

> Windows build jede schválně na **DirectX**, ne na OpenGL. Bez ovladačů od
> výrobce podstrčí Windows nouzový ovladač, který umí jen OpenGL 1.1 — na něm
> by hra vůbec nenaběhla, i když na tom stroji všechno ostatní běží. DirectX 11
> je na Windows 10+ vždycky, takže tenhle typ „mně se to nespustí" odpadá.

> Čísla vycházejí z toho, co hra opravdu dělá: zátěžové měření drží simulaci
> 250 000 budov na ~2,4 % rozpočtu tiku, takže úzké hrdlo je vykreslování, ne
> výpočet. Ověř si je na svém nejslabším stroji, než je pošleš do Steamworks.

---

## Co ještě musí udělat člověk

- **Trailer** — děláš si sám (podle zadání).
- **Cena a datum vydání.**
- **Právní údaje** — jméno vydavatele, copyright, EULA (Steam vyžaduje).
- **Age rating dotazník** — hra nemá násilí ani nic citlivého, projde snadno.
- **Steam Achievements** — hra jich má 76 v `data/achievements.json`; do
  Steamworks se musí nahrát ID, jména, popisy a ikonky. Ikonky zatím nejsou.
- **Posoudit kapsle** — vygenerované obrázky jsou poctivé (skutečná scéna ze hry),
  ale nejsou to grafické dílo. Na vydání bych je nechal přemalovat grafikem;
  jako placeholder pro založení stránky poslouží.
