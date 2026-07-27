# civdle-balance

Odsimuluje hru **bez okna** a vypíše křivky: populace, budovy, spokojenost, postup
k Vzestupu a zásoby surovin. Balanc se tím dá **měřit a porovnat** před změnou dat
a po ní, místo aby se odhadoval od oka.

```bash
dotnet run --project tools/CivDle.Balance -- --minutes 60
dotnet run --project tools/CivDle.Balance -- --minutes 180 --runs 3 --csv krivky.csv
dotnet run -c Release --project tools/CivDle.Balance -- --stress
```

Simulace je deterministická, takže stejný seed dá vždy stejný výsledek.

## Co nástroj NENÍ

Náhradní hráč není dobrý hráč. Staví podle jednoduché priority (zažehnat hlad →
dostavět bydlení → služby → výroba) a klika v pravidelném rytmu. Nevyzkoumává
technologie, nepoužívá slavnosti ani zóny. Jeho křivka je **referenční základ pro
srovnání**, ne predikce toho, jak daleko dojde člověk.

## První nálezy (2026-07)

Hned první běhy ukázaly tři věci, které při ručním ladění nebyly vidět:

1. **Ekonomika naráží na strop kolem 30–35 obyvatel** a dál se nehne ani za tři
   hodiny herního času. Vázne jídlo.
2. **Přestavění aktivně škodí.** Obsazenost se počítala globálně
   (`populace / všechna pracovní místa`), takže každá další výrobna zpomalila
   *všechny ostatní*. Náhradní hráč postavil 91 budov a tím si sám podřízl výrobu —
   a hra mu o tom nedala žádnou zpětnou vazbu.
3. Kvůli (1) a (2) byl **první Vzestup (250 obyvatel) mimo dosah**. Ten práh byl
   nastaven odhadem, ne měřením.

### Jak to dopadlo

Všechny tři nálezy padly na jednu změnu: dělníci se přidělují **budovu po budově**
a přednost mají výrobny, jejichž surovina zrovna dochází (`gameplay.json` →
`staffing.scarcityThreshold`). Město se tím samo přeskupí na to, čeho je nedostatek.

Mezikrok „nejstarší budovy mají přednost" se v měření **neosvědčil** a je dobré
vědět proč: staré farmy si držely všechny lidi, nová pila nedostala nikoho, dřevo
přestalo téct a růst se zastavil na 10 obyvatelích už v první minutě. Bez nástroje
by se to hledalo těžko.

Stejný seed, 60 minut, před a po:

| | populace | budov | spokojenost | → Vzestup | růst se zastavil |
|---|---|---|---|---|---|
| globální obsazenost | 26 | 87 | 0,30 | 10 % | v 35,8 min |
| nedostatek má přednost | 133 | 94 | 0,76 | 53 % | ne |

**První Vzestup je dostupný v 96,7 min** (dřív vůbec) a populace roste dál —
ve 150. minutě je na 335 obyvatelích.

Zbývá otevřené: hodnota prahu 250 je pořád nastavená odhadem, jen už je dosažitelná.

## Zátěžový režim (`--stress`)

Balanční běh se do velkých čísel nedostane — náhradní hráč staví pomalu. Zátěžový
režim proto město naskládá rovnou a měří dobu tiku. Vypisuje dvě tabulky:

- **nečinné město** — reálná populace (pár lidí), většina budov bez dělníků. Tohle
  je stav, do kterého se hra opravdu dostane.
- **plně obsazené město** — startovní populace nasazená tak, aby vyráběla každá
  budova. Reálná hra se sem nedostane (populace roste lineárně, viz nález níže),
  ale je to jediný poctivý **horní odhad**: jinak se výrobní smyčka přeskakuje
  a číslo lže.

Měří se celý tik, ne jednotlivé systémy — na otázku „vejdeme se do rozpočtu?"
to stačí. Rozpočet = podíl reálného času (10 Hz → 100 ms na tik).

### Naměřeno (2026-07, plně obsazené město)

| budov | obyvatel | µs/tik před | µs/tik po | rozpočet po |
|---|---|---|---|---|
| 1 000 | 2 000 | 18 | 11 | 0,01 % |
| 10 000 | 20 000 | 178 | 91 | 0,09 % |
| 50 000 | 100 000 | 1 053 | 461 | 0,46 % |
| 250 000 | 500 000 | 4 553 | 2 383 | **2,38 %** |

Obava, že přidělování dělníků bude na velkém městě problém, se **nepotvrdila**:
i před optimalizací sežral tik při 250 tisících budovách 4,5 % rozpočtu. Zrychlení
na dvojnásobek přišlo z jedné změny — nedostatkovost se počítá jednou za tik nad
*definicemi* budov (desítky), ne u každé *budovy* zvlášť (statisíce). Výsledek je
pro všechny budovy téhož typu stejný, takže se to počítalo pořád dokola.

Skutečný strop tedy neleží ve výkonu simulace, ale v tom, že se hráč k městu téhle
velikosti nedostane: populace roste konstantní rychlostí, zatímco stupně měřítka
rostou násobně. To je otevřená otázka návrhu, ne optimalizace.
