# Trailer, screenshoty a GIFy

Co natočit, co vyfotit a v jakém pořadí to na Steamu poskládat. Doplňuje
`store-page-cs.md` (texty) a `graficke-assety.md` (kapsle).

Všechny snímky vyrábí sama hra:

```
dotnet run --project src/CivDle -- --capture out/shots
```

Kamera i stav světa jsou dané seedem, takže se dá kdykoli přesně zopakovat
totéž. Focení běží **bez LOD** — na obrazovce se detail při oddálení odbourává
správně, ale na obrázku v obchodě je chybějící detail jediná věc, které si
zákazník všimne.

---

## 1. Trailer

### Pravidlo, které rozhoduje o všem ostatním

Steam pouští video **bez zvuku a bez tvého souhlasu** hned po otevření
stránky. První dvě vteřiny nemají nic vysvětlovat — mají udělat obrázek, kvůli
kterému zákazník nezavře záložku. U idle hry je tím obrázkem **měřítko**:
ne budova, ne UI, ale „jak z toho jednoho baráku bude tohle".

Proto trailer začíná růstem, ne logem.

### Struktura (0:38)

| Čas | Záběr | Proč zrovna tohle |
|---|---|---|
| 0:00–0:04 | **Časosběr**: jedna chatrč → čtvrť, zrychleně, pevná kamera | Do dvou vteřin je jasné, o čem hra je. Žádný text, žádné logo. |
| 0:04–0:09 | Kamera se **odjíždí** a růst pokračuje — čtvrť → město | Jeden nepřerušený pohyb. Střih by ten pocit rozbil. |
| 0:09–0:14 | **Přiblížení na pobřeží** za podvečera: auta na silnicích, kouř z komínů, odlesky na vodě | Tady se prodává, že to není tabulka čísel, ale místo. |
| 0:14–0:19 | **Přechod den → noc** na jednom místě, bez střihu | Nejsilnější vizuál, co hra má. Rozsvěcující se okna udělají práci za tebe. |
| 0:19–0:24 | Guvernér staví sám: zóna se namaluje, čtvrť v ní vyroste podél cest | „Hraje se to i beze mě" — hlavní argument žánru. |
| 0:24–0:29 | **×N vyskočí o řád**, obrazovka Vzestupu, svět se restartuje a hned roste rychleji | Progrese. Tohle je důvod ke koupi, ne dekorace. |
| 0:29–0:35 | **Odjezd až na aglomeraci v noci** — světelná mapa přes celou obrazovku | Vyvrcholení. Nejvyšší číslo populace, jaké máš, nech chvíli stát. |
| 0:35–0:38 | Logo + *Wishlist now* | Krátce. Kdo to dokoukal, už je rozhodnutý. |

### Čtyři pravidla k tomu

**Nestříhej rychleji než po ~4 vteřinách.** Relaxační hra sestříhaná jako
akční trailer slibuje něco, co pak nedodá — a to je nejhorší refund, jaký
můžeš mít.

**HUD nech vidět, ale ne vpředu.** Zákazník chce vědět, jak hra vypadá při
hraní. Skrytý HUD ve všech záběrech působí, jako bys ho schovával.

**Jedno číslo přes celý trailer.** Ať populace roste napříč záběry a nikdy
neklesne (kromě toho jednoho Vzestupu, kde to je pointa). Divák to podvědomě
sleduje.

**Titulky ano, mluvené slovo ne.** Tři až čtyři krátké popisky
(*„Roste, i když nehraješ"*, *„Vzestup: začni znovu, o řád výš"*) stačí a hrají
i bez zvuku, což je většina zhlédnutí.

### Co do traileru nedávat

Strom výzkumu, editor modů a obrazovky se statistikami. Jsou to dobré
screenshoty a špatné video — jsou statické a v pohybu vypadají jako menu.
Editor modů dej na konec jako *poslední* věc před logem, nebo vůbec.

---

## 2. Screenshoty na store stránku

Steam ukazuje v náhledu **první čtyři**. Zbytek si zákazník proklikne, jen když
ho ty čtyři zaujmou, takže rozhoduje pořadí, ne počet.

Sada, kterou vyrobí `--capture`:

| # | Soubor | Co je na něm | Proč je na téhle pozici |
|---|---|---|---|
| 1 | `01-city.png` | Rozrostlé město za dne, HUD vidět | Musí odpovědět „co to je" bez čtení. |
| 2 | `07-night-scale.png` | Aglomerace z výšky v noci — světelná mapa | Nejhezčí obrázek, co hra má. Patří dopředu, ne na konec. |
| 3 | `03-coast.png` | Město na pobřeží, podvečer, pěna a odlesky | Ukáže svět, ne jen mřížku budov. |
| 4 | `06-scale.png` | Odzoomovaná aglomerace za dne | „Takhle velké to bude." Měřítko prodává idle hru. |
| 5 | `02-night.png` | Noční město zblízka, rozsvícená okna | |
| 6 | `04-golden-hour.png` | Podvečer, dlouhé stíny | |
| 7 | `05-winter.png` | Zimní město | Živý svět, roční období. |
| 8 | `08-tech-tree.png` | Souhvězdí technologií | Hloubka obsahu. |
| 9 | `09-achievements.png` | Achievementy | Kolik toho hra nabízí. |

**Proč noční aglomerace na druhém místě, a ne první:** první screenshot musí
být čitelný i jako miniatura ve vyhledávání. Noční mapa je krásná, ale
v malém je z ní tmavá plocha se světlou skvrnou. Denní město v malém pořád
vypadá jako město.

**Nedávej dva podobné snímky vedle sebe.** Denní a podvečerní město ze stejného
místa jsou pro zákazníka jeden obrázek dvakrát.

---

## 3. GIFy — kam patří a kam ne

**Do store stránky GIFy nepatří.** Steam v popisu animované GIFy nepovoluje;
místo nich se používají další videa (*trailery*), kterých můžeš nahrát víc.
GIFy jsou na **Twitter/X, Reddit, Discord, Bluesky a devlog**.

Každý GIF má mít **jednu myšlenku** a smyčku do 6 vteřin. Delší nikdo
nedokouká a Reddit ho zkomprimuje na kaši.

| GIF | Délka | Co je vidět | Kam s ním |
|---|---|---|---|
| **Den → noc** | 5 s | Zrychlený přechod na jednom místě, rozsvěcující se okna | Nejlepší jediný GIF, co máš. Univerzální. |
| **Časosběr růstu** | 6 s | Chatrč → město, pevná kamera | r/incremental_games, r/IdleGames |
| **Guvernér staví** | 5 s | Namaluje se zóna, čtvrť v ní vyroste podél cest | Odpovídá na „a co tam vlastně dělám" |
| **Vzestup** | 4 s | ×N vyskočí, svět se restartuje a hned roste rychleji | Prestige publikum |
| **Pobřeží** | 4 s | Odlesky na hladině, pěna, kouř z komínů | Čistě „koukejte, jak to vypadá" |
| **Teraformace** | 5 s | Hráč přidá a ubere vodu, krajina se změní | Nejvíc „sdílitelná" mechanika |
| **Editor modů** | 6 s | Nakreslí se sprite, budova se hned postaví ve hře | r/gamedev, Discord |

Praktické:

- **Nahrávej ve 1920×1080 a zmenšuj až na konci.** GIF z malého okna je
  neopravitelně rozmazaný.
- **Na Twitter/X nahrávej MP4, ne GIF.** Kvalita je nesrovnatelná a X GIFy
  stejně převádí na video.
- **Ve smyčce nesmí být střih.** GIF, který „cukne", vypadá jako chyba hry.
- **Nechávej HUD.** V GIFu na Redditu je HUD to, podle čeho lidi poznají, že
  jde o skutečnou hru a ne o mockup.

---

## 4. Pořadí prací

1. `--capture` → devět snímků, vyber z nich sadu podle tabulky výš
2. Natoč surové záběry pro trailer (OBS, 60 fps, bez komprese)
3. Z těch samých záběrů vyřež GIFy — nic se nenatáčí dvakrát
4. Trailer sestříhej **naposled**, až budeš vědět, které záběry vyšly nejlíp
