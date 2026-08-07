# Steam store stránka — česká verze

Text je připravený k vložení do Steamworks → *Store Page*. Sekce odpovídají polím
ve formuláři jedna ku jedné; kde Steam používá BBCode, je BBCode přímo v textu.

---

## Název aplikace

```
CivDle
```

## Podtitul / krátký popis (Short Description)

Steam ho ukazuje pod obrázkem ve vyhledávání i na kartě hry. **Limit 300 znaků**,
ale prvních ~120 se ukáže v náhledech — to podstatné musí být na začátku.

```
Relaxační idle city-builder, ve kterém z jedné chatrče vyroste aglomerace pro
miliony lidí. Stavěj řetězce výroby, nech civilizaci růst i bez tebe a Vzestupem
posuň měřítko celého světa o řád výš.
```

*(238 znaků)*

## O této hře (About This Game)

Vlož do pole *About This Game*. BBCode Steam renderuje.

```
[h1]Z jedné chatrče do aglomerace pro miliony[/h1]

CivDle je 2D idle city-builder, který se hraje sám — ale odměňuje každou minutu,
kdy ho hraješ ty. Založíš osadu, postavíš první pilu, a než se naděješ, tvůj
guvernér staví čtvrti podél cest, karavany objíždějí sousední města a ty se díváš,
jak číslo nahoře přeskočí z tisíců do milionů.

[h2]Postav řetěz, ne jen barák[/h2]
Dřevo se mění v prkna, prkna v nábytek, ruda v ocel a ocel ve stroje. Každá budova
má vstupy, výstupy a lidi, kteří v ní musí pracovat. Přiblížení ukáže město, které
opravdu žije: auta na silnicích, lidi s vlastními jmény a přáními, roční období,
počasí, které se převaluje přes krajinu.

[h2]Civilizace roste, i když se nedíváš[/h2]
Guvernér umí stavět podle skutečné potřeby — chybí bydlení, staví domy; chybí
prkna, postaví pilu. Namaluj zónu a čtvrť se v ní postaví sama, podél cest a
v nejlepší kvalitě, na jakou máš. Co se stane, když nehraješ, ti hra po návratu
poctivě spočítá.

[h2]Vzestup: začni znovu, ale o řád výš[/h2]
Když město dosáhne stropu, můžeš Vzestoupit. Svět se resetuje, ale trvalá
vylepšení zůstanou — a příští civilizace roste násobně rychleji. Nad tím leží
[b]Odkaz[/b]: hlubší řez, který smaže i Vzestupy a dá měnu, která zrychluje
samotné vzestupování. A pak je tu [b]Velké dílo[/b] — bezedná stavba, do které
sypeš přebytky donekonečna.

[h2]Velká čísla, na která je vidět[/h2]
Nahoře svítí jedno velké ×N: kolikrát je tvá civilizace silnější, než byla na
začátku. Pod ním rozpis, odkud ta síla je. Žádné hádání, jestli se ten upgrade
vyplatil.

[h2]Svět, který se objevuje[/h2]
Mapa začíná pod mlhou. Vysíláš průzkumníky, stavíš radary, nacházíš sopky,
gejzíry, kaňony a cizí města — s těmi můžeš obchodovat, nebo je přibrat ke své
říši i s jejich budovami.

[h2]Co v CivDle najdeš[/h2]
[list]
[*]Přes 90 budov od chatrče po orbitální prstenec
[*]Sedm ér: od dřeva a kamene po fúzi a nanotechnologie
[*]Souhvězdí technologií, které se odkrývá, jak zkoumáš
[*]96 achievementů
[*]Distrikty, které vzniknou samy a dají synergii
[*]Víra a modlitby s rizikem — přivolej déšť, nebo meteor
[*]Kronika, grafy a časosběr celé partie
[*]Denní výzvy a zakázky
[*]Plná podpora gamepadu a Steam Decku
[*]Čeština, angličtina, němčina, polština, španělština
[/list]

[h2]Moddovatelné do posledního čísla[/h2]
Veškerý obsah hry je v JSON — budovy, suroviny, výzkum, události, biomy, počasí.
Vestavěný editor modů tě nechá vytvořit vlastní budovu i surovinu přímo ve hře,
nakreslit jí sprite a hned ji vyzkoušet. Hotový mod pak jedním tlačítkem sdílíš
na Workshopu.
```

## Krátký popis pro Steam Deck / kompatibilitu

```
Plně ovladatelné gamepadem, UI je čitelné na 1280×800.
```

---

## Systémové požadavky

### Minimální
| Položka | Hodnota |
|---|---|
| OS | Windows 10 64-bit |
| Procesor | Dvoujádrový 2,4 GHz |
| Paměť | 4 GB RAM |
| Grafika | GPU s podporou OpenGL 3.0 / DirectX 11 |
| DirectX | Verze 11 |
| Pevný disk | 500 MB volného místa |

### Doporučené
| Položka | Hodnota |
|---|---|
| OS | Windows 11 64-bit |
| Procesor | Čtyřjádrový 3,0 GHz |
| Paměť | 8 GB RAM |
| Grafika | Dedikovaná GPU s 2 GB VRAM |
| Pevný disk | 1 GB volného místa |

> Hodnoty vycházejí z toho, že hra je 2D MonoGame nad .NET 8 a simulace běží
> na agregátech, ne na jednotlivcích. Než je vyplníš do Steamworks, **změř si to
> na svém nejslabším stroji** — Steam za ně ručíš ty, ne já.

---

## Právní pole

```
© <rok> <tvé jméno nebo firma>. Všechna práva vyhrazena.
```

Pokud používáš MonoGame, FontStashSharp a Myra, patří do *Legal* i jejich licence
(MIT / Ms-PL). Seznam je v `docs/steam/third-party-licenses.md`.
