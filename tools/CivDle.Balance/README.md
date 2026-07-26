# civdle-balance

Odsimuluje hru **bez okna** a vypíše křivky: populace, budovy, spokojenost, postup
k Vzestupu a zásoby surovin. Balanc se tím dá **měřit a porovnat** před změnou dat
a po ní, místo aby se odhadoval od oka.

```bash
dotnet run --project tools/CivDle.Balance -- --minutes 60
dotnet run --project tools/CivDle.Balance -- --minutes 180 --runs 3 --csv krivky.csv
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
