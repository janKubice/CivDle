# Záběry do traileru

Hra si umí sama natočit záběry, ze kterých se dá slepit trailer. Nejsou to
screenshoty rozehrané hry — je to kulisa postavená schválně tak, aby se na ni
dalo koukat.

## Jak to spustit

Na Windows dvojklikem:

| soubor | co udělá |
| --- | --- |
| `trailer-zabery.bat` | ostrá verze: 1920×1080, 60 fps, čtyři městečka |
| `trailer-nahled.bat` | náhled: 960×540, 30 fps, dvě městečka — na posouzení kompozice |

Z příkazové řádky (i na Linuxu):

```
./trailer.sh              # ostrá verze
./trailer.sh nahled       # náhled
```

Okno hry během natáčení blikne a zmizí. Je to v pořádku: kreslí se mimo
obrazovku, do render targetu, ne na plochu.

## Co z toho vypadne

Složka `trailer/`, v ní jedna podsložka na záběr a v každé číslovaná sekvence
`frame-000123.png`:

| složka | záběr | délka |
| --- | --- | --- |
| `01-prehlidka` | všechny sprity budov krouží ve čtyřech kolotočích, přes ně titulek s počty budov a technologií | 9 s |
| `02-mesto` … `05-mesto` | pomalý přelet nad hotovým městečkem 40×40; první nese titulek „Design your dream city" | 7 s každý |

Čísla v titulku se berou **z dat**, ne z ruky. Když do hry přibude dvacet budov,
opraví se titulek sám — trailer tak nemůže slíbit víc, než hra má.

## Jak z toho udělat video

Hotové video hra nedělá schválně: kodek by znamenal další závislost (viz „no
balast" v CLAUDE.md), a takhle si sám zvolíš kvalitu. Příkaz pro ffmpeg vypíše
hra na konci hotový, vypadá takhle:

```
ffmpeg -framerate 60 -i "trailer/01-prehlidka/frame-%06d.png" \
       -c:v libx264 -preset slow -crf 16 -pix_fmt yuv420p trailer/01-prehlidka.mp4
```

`-crf 16` je hodně vysoká kvalita; pro YouTube i Steam bohatě stačí. Pixel art
nemá rád rozmazání, takže ho nepřeškálovávej — 1080p sekvence je 1080p video.

Na rychlé prohlédnutí bez ffmpegu je vedle toho:

```
python3 tools/make_trailer_gif.py trailer/01-prehlidka nahled.gif --sirka 640
```

GIF má 256 barev, takže je to náhled, ne výstup.

## Odkud se ta městečka berou

`ShowcaseTown` postaví z půdorysu (`TownPlanner`) skutečný svět — stejná
simulace, stejné sprity, stejné cesty jako ve hře. Půdorys se nesnaží být
mřížka:

* ulice mají nepravidelné rozteče (4–8 dlaždic),
* středem vedou dvě širší třídy, které se kříží na náměstí,
* bloky se zastavují **po obvodu** a uvnitř zůstává dvorek,
* každý blok má svůj hlavní typ domu a k němu pár výjimek,
* část bloků se nezastaví vůbec — to jsou parky,
* poslední prstenec je řidší, aby město nekončilo ostrým čtvercem.

Guvernérovi se pro tu chvíli zakážou všechny kategorie a vypne se vylepšování
i slučování. Bez toho auto-stavba během pár vteřin přisype vlastní domy a cesty
a z vyskládaných bloků udělá souvislou mřížku.

Semínka městeček jsou pevná a vybraná: každé staví na pláni, kde se z plánu
postaví přes devadesát procent parcel. Na horším semínku zůstanou v ulicích
díry. Když je budeš chtít vyměnit, jsou v `TrailerDirector.CitySeeds` a hlídá
je test `ShowcaseTownTests.EverySeedGivesAWorkingTown`.
