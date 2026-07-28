# store/ — podklady pro obchod

Obrázky v téhle složce se **negenerují ručně**. Vyrábí je sama hra, aby na nich
byla doopravdy ona: stejný terén, stejné sprity, stejný HUD, jaké uvidí hráč.
Cokoli nakresleného mimo hru by byl obrázek hry, ne hra — a to se pozná na první
screenshot po koupi.

## Jak je vyrobit znovu

Obojí potřebuje běžící grafiku. Na stroji bez obrazovky stačí `xvfb-run`:

```bash
./publish.sh linux-x64

# 6 snímků 1920×1080 do store/screenshots
xvfb-run -a -s "-screen 0 1920x1080x24" \
  dist/linux-x64/CivDle --capture store/screenshots

# kapsle ve všech rozměrech, které Steam vyžaduje, do store/capsules
xvfb-run -a -s "-screen 0 1920x1080x24" \
  dist/linux-x64/CivDle --capsules store/capsules
```

Na Windows/macOS se `xvfb-run` vynechá — hra otevře okno a sama se zavře, až
bude hotová.

Oba režimy jsou **deterministické**: stejná verze hry a stejné seedy dají stejné
obrázky. Když se změní grafika, stačí příkazy pustit znovu.

## Co se fotí a proč zrovna to

| Soubor | Co ukazuje |
|---|---|
| `01-city` | Město za jasného poledne — hlavní záběr |
| `02-night` | Totéž v noci, rozsvícená okna |
| `03-winter` | Zima: modravý nádech scény, období v HUD |
| `04-achievements` | Seznam achievementů — kolik toho hra nabízí |
| `05-tech-tree` | Strom technologií |
| `06-scale` | Odzoomovaná aglomerace (agregátní pohled) |

Scéna není náhodná. Kulisa (`CityFixture`) hledá místo s pestrým, zeleným
okolím, staví do mřížky bloků s ulicemi, dává pily k lesu a lomy pod skály —
a čeká na okamžik, kdy je zároveň poledne a jasno. Bez toho vycházely snímky
zašedlé deštěm nebo jako kamenné pole.

Růst populace je v kulise zrychlený. Je to **stejný stav**, ke kterému hráč
dojde taky, jen dřív — ne stav, který by ve hře nešel dosáhnout.

## Čemu se to vyhýbá

- **Toasty a oslavy** se ve focení nekreslí. Zrychlená simulace jich vyrobí
  desítky naráz a na snímku by z nich byla zeď přes půl obrazovky.
- **Uvítací okna** (denní odměna, souhrn offline) se zahazují — patří hráči,
  ne fotografovi.
- **Průvodce** je přeskočený: rada „nasekej patnáct dřeva" nad velkoměstem
  vypadá jako nedohraná hra.
- **Vyzkoumá se jen část stromu.** S kompletním stromem se odemkne tolik
  nástrojů, že se spodní lišta nevejde na obrazovku.

## Meze, o kterých je dobré vědět

- **Kapsle nejsou grafické dílo.** Jsou poctivé (skutečná scéna + název), ale
  na vydání by je měl přemalovat grafik. Jako placeholder pro založení stránky
  stačí.
- **Div světa mezi snímky není.** Megastruktury se odemykají až měřítkem
  (dva Vzestupy), a kulisa se k nim v rozumném čase nedostane. Až budeš mít
  rozehranou pozdní hru, stojí za to ho vyfotit ručně — staveniště s pruhem
  postupu je jeden z nejhezčích momentů, co hra má.
- **Logo s průhledným pozadím** (`library logo 1280×720`) tady není. Chce to
  skutečné logo, ne text vysázený herním fontem.
