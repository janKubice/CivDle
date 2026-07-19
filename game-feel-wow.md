# Game Feel & Wow-faktor — Uspokojivost

*Pracovní dokument · verze 0.1*

Jak dostat do hry ten „wow" a uspokojivý pocit — od úplně první vteřiny hraní až po late-game spektákl. Navazuje na **Tech Stack** (juice vs. výkon), **Content Design** a **Budovy**. Skoro celý obsah je **[návrh]** — je to menu nápadů k výběru, ne kánon.

---

## 0. Proč je tohle u klikačky nejdůležitější

Hlavní akce (klik na strom, umístění budovy) se během hraní zopakuje **tisíckrát**. Každý drobný kousek zpětné vazby se násobí. Když klik cítíš dobře po tisící, hra tě drží; když je mdlý, žádné late-game lasery to nezachrání.

Dva zákony, kterými se řídí celý dokument:

1. **Juice musí začít v první vteřině.** Ne až u laserů. Kamenná sekera musí být uspokojivá sama o sobě — laser je jen „ještě víc". Nikdy negatuj základní pocit za progresi.
2. **Každá éra = nová hračka.** Aby hra nezevšedněla, každá éra odemkne aspoň jednu **viditelně novou** uspokojivou věc, která tě znovu chytne.

---

## 1. Anatomie uspokojivého kliku (vrstvy juice)

Jeden klik na strom by měl spustit **několik vrstev najednou** — teprve jejich souhra dělá ten pocit. Toolkit:

| Vrstva | Konkrétně u „poražení stromu" |
|---|---|
| **Vizuál** | třísky vyletí do stran, krátký záblesk v místě zásahu |
| **Pohyb (juice)** | strom se zakymácí / lehce zmáčkne (squash & stretch) |
| **Čísla** | „+2 dřevo" vyskočí, poskočí a odpluje nahoru, pak zmizí |
| **Audio** | uspokojivý „thunk", pokaždé s lehce jinou výškou (proti únavě) |
| **Anticipace + payoff** | strom se s každým klikem viditelně zmenšuje → po pár klicích **spadne** s velkým efektem |

Ten poslední řádek je klíčový: **hromadění + vyvrcholení**. Strom nezmizí naráz — chřadne a pak přijde „timber" moment (listí se rozletí, velký kus dřeva vypadne, kamera drobně cukne). Malá anticipace, velká odměna.

---

## 2. Early-game hook (prvních 30 sekund)

Cíl: hráč se usměje dřív, než pochopí pravidla. Konkrétní scénář prvních vteřin:

- **První klik na strom** → třísky, „thunk", strom se zhoupne, „+2 dřevo" vyskočí. Okamžitá odezva.
- **Strom padá** (po pár klicích) → „timber", rozlétlé listí, velký kus dřeva, jemné cuknutí kamery.
- **Suroviny letí do skladu** → proud malých ikonek dřeva se slétne do skladu s cinknutím.
- **První budova** → žuchne na zem s obláčkem prachu, „pop" se squash/stretch efektem, jako by zapadla na místo.
- **První panáček** → vyběhne z domku, krátce zamává, dojde k práci a začne se hýbat.

Žádná z těch věcí není drahá, ale dohromady dělají ten pocit „tohle je příjemné, chci klikat dál". To je celý hook.

---

## 3. Komba a řetězové reakce *(tvůj nápad, rozvedený)*

Přesně ta „šance, že proběhne výbuch kamene vedle" je skvělý idle mechanismus — dává variabilní odměnu a emergentní momenty. Menu:

| Mechanika | Jak funguje | Pocit |
|---|---|---|
| **Řetězový pád** | poražený strom má šanci srazit sousední (domino), každý další s vyšší výškou zvuku | eskalující uspokojení |
| **Kaskáda těžby [tvůj nápad]** | vytěžený uzel má šanci spustit sousední (kámen vedle praskne a vydá surovinu) | „ó, bonus zadarmo" |
| **Kritický zásah** | náhodná šance na 5× výnos se zlatým výbuchem a jiným zvukem | slot-machine dopamin (lehce, eticky) |
| **Combo meter** | rychlé kliky za sebou budují multiplikátor; obrazovka „ožívá" (víc částic, jemný tint, stoupající tón); vyprší při pauze | odměna za aktivitu — ne trest za idle |
| **Přetečení / naplnění** | když se sklad naplní, malá oslava (záblesk, cink) | pocit dokončení |

> **Důležité pro idle žánr:** komba **odměňují aktivní hru, ale netrestají idle**. Kdo chce klikat a řetězit, dostane víc a intenzivnější zážitek. Kdo přijde po hodině, najde svět, který si klidně bzučel dál. Obě publika spokojená.

---

## 4. Tech-tree, který mění vizuál i pocit hry

Klíčová myšlenka, kterou jsi vystihl: upgrady nemají být jen „+10 %", ale mají **viditelně proměnit svět i akci**. Každý upgrade = jiný vzhled, jiný zvuk, jiné tempo.

| Řetěz | Vývoj (vzhled + pocit) |
|---|---|
| **Kácení** | kamenná sekera → ocelová → motorová pila (souvislá, řve) → **laserový harvestor** (paprsek, strom se vypaří se zábleskem) |
| **Těžba** | krumpáč → parní bagr → **laserový vrt** (paprsek taví horu, záře, vaporizace) |
| **Cesty** | pěšina → dlažba → asfalt → **zářící neonová dálnice** s létajícími auty |
| **Doprava** | pěšky → povoz → náklaďák → drony → hyperloop |
| **Práce** | ruční klik → robotický auto-klikač → **plně automatické stroje** (hráč vidí, jak stroje fyzicky přebírají práci) |

> **Pointa:** hráč nevidí jen větší číslo — vidí, jak se **jeho svět i jeho akce proměňují**. Motorová pila zní a kácí jinak než sekera. Laser je nový zážitek, ne jen nový násobič. Tohle je ten „wow, teď to hraju jinak" moment, který drží dlouhodobě.

---

## 5. Eskalace „wow" napříč hrou

Aby to chytlo hned a zároveň vydrželo, wow-faktor eskaluje ve třech patrech:

```
EARLY  →  taktilní juice   (třísky, popy, zvuky, padající stromy)     → chytne HNED
MID    →  transformace     (nástroje/cesty/vozidla se viditelně mění, odemknou se komba)
LATE   →  spektákl         (lasery, létající auta, megastruktury, celá mapa žije)
```

Každé patro re-hookuje hráče něčím novým. Early ho chytne, mid ho překvapí, late ho ohromí.

---

## 6. Late-game spektákl *(tvoje nápady + doplnění)*

| Efekt | Popis |
|---|---|
| **Laserová těžba** [tvůj nápad] | paprsky krájí hory a lesy, záře, vaporizační částice — brutálně uspokojivé |
| **Létající auta** [tvůj nápad] | proudy mezi mrakodrapy, v noci světelné stopy |
| **Rakety z kosmodromu** | start rakety = spektákl (napojení na megastruktury) |
| **Hromadná automatizace** | celá mapa žije tisíci pohyby; LOD z dálky ukáže proudy světel |
| **Denní / noční cyklus [návrh]** | v noci se město rozzáří okny a světly — obří wow při oddálení nad milionovou civilizací |
| **Milníkové oslavy [návrh]** | při dosažení mety (milion obyvatel, dokončený div) civilizační pulz / ohňostroj |

> **Denní/noční cyklus zvlášť doporučuju:** je relativně levný (světelný overlay) a při pohledu na oddálenou noční civilizaci plnou světel dělá přesně ten „wow", který jsi popisoval na začátku celého projektu.

---

## 7. Uměřenost a výkon *(caveat)*

Juice je koření — přesolený jídlo zničí. A u milionu entit musí být chytře cílený:

- **Juice u kamery a na akcích hráče.** Nemůžeš dát výbuch částic na milion entit (viz tech dok). Koncentruj spektákl tam, kam se hráč dívá, a na to, co právě dělá. Z dálky stačí agregovaný pohyb a světla.
- **Šetři velké efekty pro velké chvíle.** Když třese a exploduje všechno pořád, nic není zvláštní. Screen shake, zlaté výbuchy a oslavy si nech na kritické zásahy, pády, dokončení, komba.
- **Slidery v nastavení.** Intenzita screen shake a částic ať jde stáhnout (přístupnost — někdo shake nesnáší, někdo chce výkon).
- **Technicky:** pooling částic (nealokovat za běhu), strop na počet efektů naráz, **LOD i pro juice** (žádné částice na maximálním oddálení).

---

## 8. Proč to funguje (krátká psychologie)

- **Okamžitá odezva** — akce → reakce v řádu milisekund. Bez prodlevy = návykové.
- **Variabilní odměna** — kritické zásahy, komba, kaskády. Lehce a eticky, ne manipulativně.
- **Viditelný pokrok** — čísla stoupají, svět viditelně roste a mění se.
- **Anticipace → payoff** — strom chřadne a pak padne; sklad se plní a pak se sklidí. Napětí a úleva v malém.

Vše v souladu s anti-frustrací z předchozích doků: juice je **samá odměna**, oslava, nikdy trest.

---

## 9. Otevřené otázky k doladění

- **Combo meter — do launche, nebo později?** Je to skvělý hook pro aktivní hráče, ale kus práce navíc. Dá se přidat v updatu.
- **Denní/noční cyklus — od začátku, nebo jako „wow" update?** Levný na efekt, ale je to systém navíc.
- **Jak silné kritické zásahy?** Moc časté/velké → rozbijí ekonomiku; vzácné a šťavnaté → koření. Ladit s balancem.
- **Kolik screen shake je ještě příjemné?** Snadno se přežene; začni jemně a přidávej podle testů.
- **Zvuková paleta** — kdo dělá SFX? Zvuk je půlka pocitu; vyplatí se investovat dřív než do dalších vizuálů.

---

*Návrh k iteraci. Skoro vše je menu možností — vyber, co ti sedí, a zbytek škrtni.*
