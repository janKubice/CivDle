# Content Design — Idle City Builder

*Pracovní dokument · verze 0.1*

Návrh herního obsahu: suroviny, obyvatelé a jejich práce, biomy světa, katastrofy a eventy. Vychází z jádra hry: **relaxační idle smyčka + lehká správa výrobního řetězce** (hlídat, aby číslicka moc neklesla, a případně dostavět). Co bylo ponecháno na doplnění, je označeno jako **[návrh]** — ber jako startovní bod k úpravě, ne jako finální kánon.

---

## 0. Návrhová filozofie obsahu

Tři pravidla, kterými se řídí veškerý obsah níže:

1. **Soft pressure, ne stres.** Hráč hlídá toky, ne přežití. Když něco vyschne, navazující výroba se *zpomalí nebo zastaví*, nikdy nezničí. Řešení je vždy „přesuň lidi / dostav budovu", ne „začni znovu".
2. **Expanze táhne progresi.** Nové suroviny nejsou jen odemčené výzkumem — leží v jiných biomech. Chceš kov? Musíš dojít do hor. Tím se propojuje růst města s objevováním mapy.
3. **Katastrofy jsou vlnky, ne vlny.** Žádný event nemaže stock ani nesmaže budovu natrvalo (viz sekce 4). Cíl je okořenit rutinu, ne potrestat hráče za to, že byl chvíli pryč.

---

## 1. Suroviny

Suroviny jsou rozdělené do **tierů (vrstev)** kopírujících technologický postup od doby kamenné po futuristiku. Vyšší tier skoro vždy potřebuje vstupy z nižšího → vzniká výrobní řetězec, který hráč hlídá.

### 1.1 Dva typy surovin

| Typ | Chování | Příklady | Role ve hře |
|---|---|---|---|
| **Stock** (zásoba) | Hromadí se, čeká ve skladu | dřevo, kámen, kov | Stavební materiál, obchod |
| **Flow / upkeep** (tok / spotřeba) | Spotřebovává se každý tik | jídlo, energie | Gate na populaci a provoz — hlavní „hlídací" mechanika |

Jádro soft-pressure smyčky = **flow suroviny**. Když jídlo klesne pod spotřebu, populace neroste (a lidé začnou být nespokojení), ale *neumírají hromadně* — viz anti-frustrace.

### 1.2 Tier tabulka

| Tier | Éra | Suroviny | Vzniká z |
|---|---|---|---|
| **T0** | Sběr | dřevo, kámen, hlína, voda, jídlo (sběr/lov), vlákno **[návrh]** | přímo z mapy |
| **T1** | Základní zpracování | prkna, cihly, dřevěné uhlí, nástroje, tkanina **[návrh]** | T0 |
| **T2** | Doba bronzová | měď, cín, **bronz** (měď+cín) **[návrh: pořadí]** | ruda z hor |
| **T3** | Doba železná | železná ruda, **železo**, uhlí | ruda + palivo |
| **T4** | Průmysl | **ocel** (železo+uhlí), sklo, beton, cement **[návrh]** | T3 + suroviny |
| **T5** | Moderní | hliník, ropa, plast, elektronika, palivo **[návrh]** | T4 + ropná těžba |
| **T6** | Futuristika | kompozity, polovodiče/čipy, energočlánky, **nanoocel**, supravodič **[návrh]** | T5 + výzkum |

> **Poznámka k bronzu vs. železu:** historicky předchází bronz železu (proto je zařazen do T2 před železo). Dává to i lepší progresi — víc kroků mezi kamenem a ocelí. Pokud chceš, můžeš je klidně prohodit, ale takhle to sedí líp.

### 1.3 Peníze

**Peníze** stojí mimo tiery — jsou univerzální měna. Zdroje: daně z populace, prodej přebytků surovin, obchodní eventy, přístavy/karavany. Utrácí se za upgrady, automatizaci a nákup surovin, které v tvých biomech chybí (elegantní pojistka proti zaseknutí).

### 1.4 Ukázka řetězce (proč to hráč hlídá)

```
strom ──► dřevorubec ──► DŘEVO ──┬─► pila ──► PRKNA ──► stavba budov
                                 └─► milíř ──► DŘEVĚNÉ UHLÍ ─┐
hory ──► horník ──► ŽELEZNÁ RUDA ───────────────────────────┴─► huť ──► ŽELEZO ──► ocel...
```

Když hráč přesune všechny horníky jinam, vyschne ruda → huť stojí → dojde železo → zastaví se ocel → stavba pokročilých budov čeká. **Žádná katastrofa, jen důsledek špatné alokace** — přesně ten „hlídací" gameplay, který chceš.

---

## 2. Lidé a jejich práce

Populace je zdroj sama o sobě. Hráč má **pool obyvatel** a rozděluje je do prací. Každá práce potřebuje **pracoviště (budovu) se sloty** — nemůžeš přiřadit 100 horníků, když máš jeden důl o 10 slotech. To je jádro managementu: *kolik lidí dělá co*.

### 2.1 Kategorie prací

| Kategorie | Práce | Produkuje | Éra |
|---|---|---|---|
| **Sběr** | dřevorubec | dřevo | od začátku |
| | farmář | jídlo (plodiny) | od začátku |
| | rybář | jídlo (ryby) | pobřeží/řeka |
| | lovec **[návrh]** | jídlo (maso), kůže | les |
| | horník | kámen, ruda, uhlí | hory |
| | kopáč hlíny **[návrh]** | hlína | bažina/nížina |
| **Zpracování** | tesař | prkna | T1 |
| | hutník / slévač **[návrh]** | kovy (bronz, železo, ocel) | T2+ |
| | řemeslník | nástroje, zboží | T1+ |
| | dělník | univerzální zpracování | T1+ |
| **Stavba** | stavitel | staví budovy | od začátku |
| **Služby** | obchodník | peníze | T1+ |
| | vědec / inženýr **[návrh]** | výzkum, T6 materiály | pozdní hra |

### 2.2 Populace jako vrstvy **[návrh]**

Aby management dával smysl, populace není jen „počet dělníků":

- **Děti** — nepracují, ale rostou v budoucí dělníky. Spotřebovávají jídlo. Gate na dlouhodobý růst.
- **Dělníci** — přiřaditelní do prací.
- **Nezaměstnaní / volní** — pool k dispozici; sami nic neprodukují, jen čekají na přiřazení (a jedí).
- **Specialisté** (pozdní hra) — vědci, inženýři; vznikají vzděláním, ne jen narozením.

### 2.3 Spokojenost **[návrh]**

Lehká vrstva, ne micromanagement: dostatek jídla + bydlení + trocha luxusu = spokojenost. Vysoká spokojenost = rychlejší růst populace a produktivita. Nízká = pomalý růst. **Nikdy revolta co smaže progres** — jen zpomalení. Drží to v souladu s anti-frustrací.

---

## 3. Svět — biomy

Mapa je (prakticky) nekonečná a dělí se na biomy. **Každý biom má výhodu i nevýhodu** a hlavně jinou skladbu surovin → hráč expanduje, aby získal přístup k tomu, co doma nemá. Startuješ v nížině, ale pro kov musíš do hor, pro obchod k moři atd.

| Biom | Výhody | Nevýhody | Klíčové suroviny |
|---|---|---|---|
| **Nížina / louka** (start) | úrodná půda, snadná stavba, vyvážený | žádné vzácné suroviny | jídlo, hlína |
| **Les** | hodně dřeva, lov (maso/kůže) | málo místa na pole (nutno kácet), pomalá stavba | dřevo, zvěř |
| **Hory** | ruda, kovy, kámen | skoro žádná půda, těžká stavba, chladno | ruda, kámen, uhlí |
| **Pobřeží** | rybolov (jídlo), přístavy = bonus k obchodu | málo souše, náchylné na bouře | ryby, obchod |
| **Poušť [návrh]** | ropa, sklo/písek, pozdní solární energie | skoro žádné jídlo/voda, drsné podmínky | ropa, energie |
| **Tundra / sníh [návrh]** | vzácné materiály, plyn/ropa v pozdní hře | pomalý růst, vysoká spotřeba (topení) | plyn, vzácné |
| **Bažina [návrh]** | hojnost hlíny, ryby, voda | těžká stavba, riziko nemocí (event) | hlína, voda |
| **Řeka** (průřezový prvek) [návrh] | voda, mlýny, doprava, transport surovin | dělí souš (mosty) | voda, doprava |

> **Design pointa:** biomy nejsou dekorace — jsou to „klíče" k tierům surovin. Bez hor se nedostaneš ke kovu, bez pobřeží těžko rozjedeš obchod. Expanze = přirozená motivace prozkoumávat nekonečnou mapu, přesně jak zněl původní vizuál (auta/vlaky/letadla propojující vzdálené kolonie).

---

## 4. Katastrofy a eventy

**Železné pravidlo (tvoje zadání):** žádný event nemaže stock surovin ani nezničí budovu natrvalo. Katastrofy působí jen na **flow** (dočasně sníží produkci) nebo přepnou budovu do stavu „offline → potřebuje levnou opravu". Cíl = okořenit rutinu a dát hráči malé rozhodnutí, ne ho potrestat.

### 4.1 Negativní eventy (soft) **[návrh]**

| Event | Efekt | Jak hráč řeší | Biom |
|---|---|---|---|
| **Sucho** | farmy produkují míň po X min | spolehnout se na ryby / zásoby | nížina, poušť |
| **Bouře** | rybolov/přístav pauza, drobná oprava | přečkat, mít rezervu jídla | pobřeží |
| **Požár** | 1 budova offline → levná oprava (nemizí!) | opravit, případně přeřadit lidi | kdekoli, vzácně |
| **Nemoc** | část dělníků dočasně „nemocná" (nepracují) | přesunout práci, přečkat | bažina, hustá města |
| **Škůdci** | plodiny dočasně snížené | dočasně jiný zdroj jídla | les, nížina |
| **Mráz** | vyšší spotřeba jídla/energie po dobu zimy | mít rezervu, topit | tundra, hory |
| **Sesuv (mírný)** | důl dočasně zpomalen | přečkat, těžit jinde | hory |

Design zásada: efekt je **dočasný a lokální**. Nejhorší, co se stane, je „týden pomalejší železo". Nikdy „přišel jsi o město".

### 4.2 Pozitivní eventy **[návrh]**

Aby svět nebyl jen o obraně — polovina eventů je odměna:

- **Bohatá úroda** — nárazový příval jídla.
- **Obchodní karavana** — prodej přebytku za bonusové peníze.
- **Objev ložiska** — dočasný boost těžby nebo nový surovinový uzel.
- **Přistěhovalci** — příliv populace zdarma.
- **Festival** — dočasný boost spokojenosti a produktivity.
- **Vynález** — malé postrčení výzkumu / odemčení upgradu.

### 4.3 Eventy s volbou **[návrh]**

Občas malé rozhodnutí místo automatického efektu (lehké, ne dialogové stromy):

- **Poutník** nabízí obchod: dej suroviny teď → dostaneš bonus později.
- **Delegace** žádá pomoc: pošli jídlo sousedům → získáš obchodní slevu / reputaci.
- **Riskantní expedice** do neznámého biomu: malá investice → šance na vzácnou surovinu.

### 4.4 Chill mód **[návrh, doporučeno]**

Přepínač v nastavení: intenzita katastrof (Vypnuto / Mírné / Normální). Idle publikum má rádo kontrolu nad tím, kolik „údržby" chce. Nula frustrace pro toho, kdo chce jen sledovat rostoucí město, výzva pro toho, kdo chce hlídat.

---

## 5. Jak to do sebe zapadá

```
BIOM  ──určuje──►  dostupné SUROVINY (tier)
                        │
SUROVINY  ──potřebují──►  PRÁCE (dělníky ve slotech budov)
                        │
PRÁCE  ──gate──►  POPULACE  ──spotřebovává──►  JÍDLO/ENERGII (flow)
                        │
EVENTY  ──dočasně kolísají──►  FLOW  (nikdy stock, nikdy natrvalo)
```

Celý loop: **expanduj do biomu → získej surovinu → přiřaď lidi → hlídej flow → reaguj na drobné eventy → dostav → opakuj**. Klidné, ale ne bezmyšlenkovité.

---

## 6. Otevřené otázky k doladění

- **Kolik tierů surovin reálně chceš?** T0–T6 je hodně obsahu; možná stačí do T5 pro launch a futuristiku přidat v updatu.
- **Je spokojenost potřeba už na startu, nebo až později?** Dá se přidat jako vrstva ve druhé fázi.
- **Řeka jako biom, nebo jako průřezový modifikátor?** (Návrh výše ji bere jako modifikátor.)
- **Mají eventy vlastní frekvenci podle biomu, nebo globální timer?** Ovlivní to, jak „živě" svět působí.
- **Kolik prací je ještě příjemné managovat?** Moc typů = micromanagement; příliš málo = nuda. Doporučuju začít s ~6–8 a rozšiřovat.

---

*Tento dokument je návrh k iteraci. Suroviny, biomy a eventy označené **[návrh]** jsou moje doplnění tvého zadání — klidně škrtej a přepisuj.*
