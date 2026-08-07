# Tagy, kategorie a obrázkové assety

Tři věci, které se na Steamu vyplňují mimo textová pole a každá má svá pravidla.

---

## 1. Tagy

Steam bere až 20 tagů, ale **rozhoduje pořadí** — prvních pět nese největší váhu
při doporučování. Zbytek jen upřesňuje. Nedávej tagy, které hru nepopisují: Steam
je čistí a nepřesné tagy ti kazí doporučovací profil.

Pořadí od nejdůležitějšího:

| # | Tag | Proč |
|---|---|---|
| 1 | `Idle` | Přesně to, co hra je. Publikum tenhle tag aktivně prochází. |
| 2 | `City Builder` | Druhá půlka žánru. |
| 3 | `Incremental` | Sesterský tag k Idle, jiné publikum. |
| 4 | `Base Building` | Široký a hodně sledovaný. |
| 5 | `Management` | Chytá „relaxační management" publikum. |
| 6 | `Resource Management` | Řetězce výroby. |
| 7 | `Automation` | Guvernér a zóny. |
| 8 | `Simulation` | Široký žánr. |
| 9 | `Strategy` | Široký žánr. |
| 10 | `Singleplayer` | |
| 11 | `2D` | |
| 12 | `Top-Down` | |
| 13 | `Pixel Graphics` | Jen pokud sedí na finální art — jinak vyhoď. |
| 14 | `Relaxing` | Odpovídá „relaxační jádro" z návrhu. |
| 15 | `Casual` | |
| 16 | `Economy` | |
| 17 | `Moddable` | Máš editor modů i Workshop — málokdo to má. |
| 18 | `Colony Sim` | |
| 19 | `Sandbox` | |
| 20 | `Family Friendly` | |

## 2. Kategorie (Steamworks → Application → Store Settings)

Zaškrtni:

- **Single-player**
- **Steam Achievements**
- **Steam Cloud** *(až budeš mít cloud nastavený — viz `setup-guide.md`)*
- **Steam Leaderboards**
- **Steam Workshop**
- **Partial Controller Support** *(nebo **Full Controller Support**, pokud projde ověření — hra ovládání gamepadem má)*
- **Stats**

Nezaškrtávej nic, co v buildu opravdu není. Steam to kontroluje a nesoulad je
důvod k zamítnutí review.

## 3. Žánry

Primární: **Simulation**
Sekundární: **Strategy**, **Indie**, **Casual**

## 4. Obrázkové assety — přesné rozměry

Steam nahrání ve špatném rozměru **odmítne**. Tabulka je zdroj pravdy; generátor
v `--capsules` vyrábí přesně tyhle soubory.

| Asset | Rozměr | Kde se ukazuje |
|---|---|---|
| Header capsule | 460 × 215 | Karta hry, wishlist, doporučení |
| Small capsule | 231 × 87 | Výsledky vyhledávání, top sellery |
| Main capsule | 616 × 353 | Hlavní stránka, výprodeje |
| Vertical capsule | 374 × 448 | Sezónní stránky, festivaly |
| Library capsule | 600 × 900 | Knihovna hráče (svislá dlaždice) |
| Library header | 920 × 430 | Knihovna, řádek „nedávno hrané" |
| Library hero | 1920 × 620 | Pozadí stránky hry v knihovně |
| Library logo | 1280 × 720 (PNG s průhledností) | Logo přes hero |
| Page background | 1438 × 810 | Pozadí store stránky |
| Community icon | 184 × 184 | Komunitní huby |
| Client icon | 32 × 32 (ICO) | Lišta Steam klienta |
| Screenshoty | 1920 × 1080, min. 5 | Store stránka |

### Co je hotové a co ne

Příkaz `CivDle.exe --capsules <složka>` vygeneruje **všechny kapsle** kromě
`library-logo` (ten potřebuje průhledné logo, ne render scény — to je grafická
práce, ne generátor) a `client-icon` (ICO se dělá z ikony aplikace).

Příkaz `CivDle.exe --capture <složka>` nafotí **screenshoty ze skutečné hry** —
ne makety. Kamera i stav světa jsou dané seedem, takže se dají kdykoli přesně
zopakovat.

### Doporučené pořadí screenshotů na store stránce

Steam ukazuje první čtyři v náhledu. Pořadí je marketingové rozhodnutí:

1. **Rozrostlé město za dne** — hlavní obrázek, ukazuje měřítko
2. **Odzoomovaná aglomerace** — „takhle velké to bude"
3. **Noční město** — nejhezčí záběr, rozsvícená okna
4. **Strom technologií** — hloubka obsahu
5. **Zimní město** — roční období, živý svět
6. **Achievementy** — kolik toho hra nabízí

## 5. Trailer

Steam chce video, ne GIF. GIFy z `docs/steam/generated/gifs/` použij na
**Twitter/X, Reddit a Discord**, ne do store stránky.

Struktura, která u idle her funguje (30–45 s):

| Čas | Co je vidět |
|---|---|
| 0:00–0:03 | Jedna chatrč na prázdné mapě. Ticho. |
| 0:03–0:10 | Zrychlený růst — časosběr, město se rozlévá |
| 0:10–0:18 | Přiblížení: auta, lidi, roční období |
| 0:18–0:25 | Číslo ×N vyskočí o řád; Vzestup |
| 0:25–0:35 | Odzoomovaná aglomerace přes celou obrazovku |
| 0:35–0:42 | Editor modů — „a můžeš si přidat vlastní" |
| 0:42–0:45 | Logo + „Wishlist now" |

Nahoď na začátek to největší číslo, jaké máš. Idle publikum kupuje měřítko.
