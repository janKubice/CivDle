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

Hned první běhy ukázaly dvě věci, které při ručním ladění nebyly vidět:

1. **Ekonomika naráží na strop kolem 30–35 obyvatel** a dál se nehne ani za tři
   hodiny herního času. Vázne jídlo.
2. **Přestavění aktivně škodí.** Obsazenost se počítá globálně
   (`populace / všechna pracovní místa`), takže každá další výrobna zpomalí
   *všechny ostatní*. Náhradní hráč postavil 91 budov a tím si sám podřízl výrobu —
   a hra mu o tom nedala žádnou zpětnou vazbu.
3. Kvůli (1) a (2) je **první Vzestup (250 obyvatel) mimo dosah**. Ten práh byl
   nastaven odhadem, ne měřením.

Body 2 a 3 jsou otevřené — viz poznámky v `docs/mvp-roadmap.md`.
