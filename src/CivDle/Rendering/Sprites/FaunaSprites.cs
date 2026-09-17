using CivDle.Core.Content;
using Microsoft.Xna.Framework;

namespace CivDle.Rendering.Sprites;

/// <summary>Kam sprite tvora patří vůči bodu, na kterém tvor „je".</summary>
public enum FaunaAnchor
{
    /// <summary>
    /// Nohama na zem — kotva je dole uprostřed. Kresba má u paty stín a tvor
    /// z ní vyrůstá nahoru, stejně jako chodci a stromy.
    /// </summary>
    Ground,

    /// <summary>
    /// Volně v prostoru — kotva je uprostřed kresby. Pták v letu ani ryba pod
    /// hladinou na ničem nestojí; postavit je „nohama na zem" by je posunulo
    /// o půl těla stranou a letěli by pod sebou.
    /// </summary>
    Free,
}

/// <summary>
/// Jeden druh zvěře i s tím, jak se kreslí.
/// </summary>
/// <param name="Id">ID spritu v knihovně (<c>fauna.&lt;druh&gt;</c>).</param>
/// <param name="Width">Šířka plátna ve world pixelech (kreslí se 1:1).</param>
/// <param name="Height">Výška plátna ve world pixelech.</param>
/// <param name="Anchor">Kam kresba patří vůči pozici tvora.</param>
/// <param name="Draw">Vlastní kresba.</param>
public readonly record struct FaunaSprite(
    string Id,
    int Width,
    int Height,
    FaunaAnchor Anchor,
    Action<PixelCanvas> Draw);

/// <summary>
/// Tvar jednoho druhu: proporce, kotva a vlastní kresba.
///
/// <para>Barvu ani měřítko tvar nezná — ty přicházejí z <c>data/fauna.json</c>.
/// Tohle je to „jak" z pravidla „data = co, kód = jak": že má srnec čtyři nohy
/// a paroží, je vlastnost kresby; jakou má barvu a jak je velký, je obsah.</para>
/// </summary>
/// <param name="BaseWidth">Šířka plátna při měřítku 1.</param>
/// <param name="BaseHeight">Výška plátna při měřítku 1.</param>
/// <param name="Anchor">Kam kresba patří vůči pozici tvora.</param>
/// <param name="Draw">Kresba; dostane plátno a srst namíchanou z barvy v datech.</param>
internal readonly record struct FaunaShape(
    int BaseWidth,
    int BaseHeight,
    FaunaAnchor Anchor,
    Action<PixelCanvas, FaunaSprites.Pelt> Draw);

/// <summary>
/// Kresby ambientní zvěře.
///
/// <para><b>Proč to vzniklo:</b> zvěř se kreslila jako barevný čtvereček o dvou
/// až pěti pixelech. Dokud byly druhy tři, dalo se to omluvit; s osmačtyřiceti
/// z toho byla jen mapa různobarevných teček, na které medvěd od lišky poznat
/// nejde — a zvíře, které hráč nepozná, je z hlediska zážitku totéž jako
/// zvíře, které tam není.</para>
///
/// <para><b>Proč archetypy, a ne osmačtyřicet kreseb:</b> na deseti pixelech
/// nese informaci <b>silueta</b>, ne detail. Srnec, gazela a zebra mají tutéž
/// siluetu čtyřnožce a liší se rohy, ocasem, kresbou srsti a poměrem nohou
/// k tělu — což jsou přesně ty parametry, které archetyp bere. Osmačtyřicet
/// samostatných kreseb by bylo osmačtyřicetkrát totéž tělo opsané znovu
/// a pokaždé o pixel jinak.</para>
///
/// <para><b>Kotva patří ke kresbě, ne do dat:</b> jestli se sprite staví
/// nohama na zem, nebo se věší doprostřed, plyne z toho, <em>jak je nakreslený</em>
/// (mám dole stín a nohy, nebo volné tělo). Je to „kód = jak", ne „data = co".</para>
///
/// <para>Vrstva: čistý render. O simulaci neví nic; barvu a velikost druhu si
/// bere z <c>data/fauna.json</c> přes <see cref="FaunaSpriteFactory"/>.</para>
/// </summary>
public static class FaunaSprites
{
    /// <summary>ID spritu pro druh z <c>data/fauna.json</c>.</summary>
    public static string IdFor(string faunaId) => $"fauna.{faunaId}";

    /// <summary>
    /// Kresby pro druhy, jak jsou v datech — s jejich barvou a měřítkem.
    ///
    /// <para><b>Proč to bere obsah:</b> barva každého druhu byla napsaná
    /// dvakrát — jednou v <c>fauna.json</c> a podruhé natvrdo tady. Změna
    /// v datech pak přebarvila zvíře na minimapě, ale sprite ne. To je ta
    /// nejhorší varianta chyby: nic nespadne, jen si dva kusy hry myslí něco
    /// jiného. Teď je barva jen na jednom místě — v datech.</para>
    ///
    /// <para>Druh bez kresby se přeskočí, aby cizí obsah neshodil start hry;
    /// že žádný takový není, hlídá test pokrytí.</para>
    /// </summary>
    public static IReadOnlyList<FaunaSprite> For(IReadOnlyList<FaunaDef> fauna)
    {
        var list = new List<FaunaSprite>(fauna.Count);

        foreach (var def in fauna)
        {
            if (!Shapes.TryGetValue(def.Id, out var shape))
            {
                continue;
            }

            var pelt = Pelt.Of(def.Color);
            list.Add(new FaunaSprite(
                IdFor(def.Id),
                Scaled(shape.BaseWidth, def.Scale),
                Scaled(shape.BaseHeight, def.Scale),
                shape.Anchor,
                canvas => shape.Draw(canvas, pelt)));
        }

        return list;
    }

    /// <summary>
    /// Rozměr plátna po zvětšení podle dat.
    ///
    /// <para>Meze nejsou dekorace: dlaždice má šestnáct pixelů a chodec dvanáct.
    /// Pod pěti pixely je zvíře zase jen tečka, nad dvaceti přeroste dlaždici,
    /// na které stojí, a rozbije měřítko krajiny.</para>
    /// </summary>
    private static int Scaled(int baseSize, double scale) =>
        Math.Clamp((int)Math.Round(baseSize * scale), 4, 20);

    /// <summary>Zná knihovna kresbu pro tenhle druh?</summary>
    public static bool Knows(string faunaId) => Shapes.ContainsKey(faunaId);

    /// <summary>
    /// Kam patří sprite daného druhu. Volá render při kreslení; neznámý druh
    /// se chová jako tvor na zemi, což je většina.
    /// </summary>
    public static FaunaAnchor AnchorFor(string faunaId) =>
        Shapes.TryGetValue(faunaId, out var shape) ? shape.Anchor : FaunaAnchor.Ground;

    private static readonly Dictionary<string, FaunaShape> Shapes = Build();

    // ---------------------------------------------------------------------
    // Barvy
    // ---------------------------------------------------------------------

    /// <summary>Stín u paty. Jeden pro všechny — díky němu zvíře stojí na zemi a není to nálepka.</summary>
    private static readonly Color GroundShadow = new(0, 0, 0, 72);

    /// <summary>Oko, kopyto, dráp. Nikdy čistě černá — ta na paletě vypadá jako díra.</summary>
    private static readonly Color Ink = new(32, 30, 38);

    /// <summary>
    /// Tři odstíny jedné srsti: hřbet do světla, tělo, břicho do stínu.
    ///
    /// <para>Bez ramp je zvíře placka. Tři tóny jsou minimum, na kterém se
    /// z obdélníku stane objem — a zároveň strop toho, co se na deseti
    /// pixelech dá rozeznat.</para>
    /// </summary>
    internal readonly record struct Pelt(Color Coat, Color Shade, Color Light, Color Accent)
    {
        /// <summary>
        /// Rampa z jedné barvy druhu, aby se barva držela dat.
        ///
        /// <para>Rampa se <b>ověřuje proti paletě</b>, neodhaduje se z jasu.
        /// Paleta hry má dvaatřicet barev, takže dva tóny vzdálené o desetinu
        /// po srovnání splynou v jeden — a ze zvířete je jednolitá skvrna bez
        /// nohou. Práh na jasu tohle neuhlídá: přímorožec má součet složek 597
        /// a při hranici 600 spadl mezi světlá zvířata právě o tři body.
        /// Tady se místo hádání zkusí, jestli je odstín po srovnání opravdu
        /// jiný, a když není, sáhne se po tmavším.</para>
        ///
        /// <para>Světlá srst se proto stínuje <em>odspodu</em>: u bílé lišky
        /// se „ještě světlejší" nikam nevejde, zatímco tmavší tón ano.</para>
        /// </summary>
        /// <summary>Rampa z barvy druhu, jak je zapsaná v <c>data/fauna.json</c>.</summary>
        public static Pelt Of(RgbColor color, Color? accent = null) =>
            Of((color.R << 16) | (color.G << 8) | color.B, accent);

        public static Pelt Of(int rgb, Color? accent = null)
        {
            var coat = new Color((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            var light = Lighter(coat, coat);

            if (light is null)
            {
                // Nadsvětlení se do palety nevejde — celá rampa jde dolů.
                var body = Darker(coat, coat, 0.88f, 0.64f) ?? Scale(coat, 0.74f);
                return new Pelt(body, Darker(coat, body, 0.60f, 0.38f) ?? Scale(coat, 0.48f), coat, accent ?? Ink);
            }

            return new Pelt(coat, Darker(coat, coat, 0.72f, 0.44f) ?? Scale(coat, 0.58f), light.Value, accent ?? Ink);
        }

        /// <summary>
        /// Světlejší tón, který paleta <b>nesrovná</b> na tutéž barvu jako
        /// <paramref name="against"/>; <c>null</c>, když takový není.
        ///
        /// <para>Světlí se <b>k bílé</b>, ne násobkem. Násobek totiž zesílí ten
        /// nejsytější kanál nejvíc, takže z hnědého hřbetu srnce byl po
        /// srovnání na paletu <em>oranžový</em> pruh a z gazely žlutá. Míchání
        /// k bílé barvu odbarvuje, a tím zůstává ve svém odstínu.</para>
        /// </summary>
        private static Color? Lighter(Color source, Color against)
        {
            var reference = GamePalette.Snap(against);
            for (int i = 1; i <= 8; i++)
            {
                var candidate = Mix(source, Color.White, 0.16f + (i * 0.045f));
                if (GamePalette.Snap(candidate) != reference)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Tmavší tón, který paleta nesrovná na tutéž barvu jako
        /// <paramref name="against"/>. Tmavnout násobkem je v pořádku —
        /// ubírá ze všech kanálů úměrně, takže odstín drží.
        /// </summary>
        private static Color? Darker(Color source, Color against, float from, float to)
        {
            var reference = GamePalette.Snap(against);
            for (int i = 0; i <= 8; i++)
            {
                var candidate = Scale(source, from + ((to - from) * i / 8f));
                if (GamePalette.Snap(candidate) != reference)
                {
                    return candidate;
                }
            }

            return null;
        }
    }

    private static Color Scale(Color c, float f) =>
        new((int)(c.R * f), (int)(c.G * f), (int)(c.B * f), c.A);

    private static Color Mix(Color a, Color b, float t) => new(
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t),
        a.A);

    // ---------------------------------------------------------------------
    // Popis těla
    // ---------------------------------------------------------------------

    /// <summary>Co má zvíře na hlavě. Právě tím se čtyřnožci od sebe poznají.</summary>
    private enum Horns
    {
        None,

        /// <summary>Paroží srnce — dvě větvičky vzhůru.</summary>
        Antlers,

        /// <summary>Lopaty losa — široké a ploché.</summary>
        Palmate,

        /// <summary>Krátké špičaté růžky (gazela, kamzík).</summary>
        Spikes,

        /// <summary>Stočené rohy kozorožce, dozadu přes hřbet.</summary>
        Curved,

        /// <summary>Dlouhé rovné rohy přímorožce, skoro svisle.</summary>
        Straight,

        /// <summary>Kly kance — dopředu a nahoru od čenichu.</summary>
        Tusks,
    }

    private enum Tails
    {
        /// <summary>Pahýl (medvěd, kanec).</summary>
        Stub,

        /// <summary>Tenký provázek (zebra, slon).</summary>
        Thin,

        /// <summary>Huňatý ocas (liška, vlk) — druhá nejnápadnější věc po hlavě.</summary>
        Bushy,

        /// <summary>Střapec na konci (lev).</summary>
        Tuft,
    }

    private enum Ears
    {
        Small,

        /// <summary>Vztyčené uši fenka — na hlavě jsou skoro tak velké jako hlava.</summary>
        Big,

        /// <summary>Boltec slona: plachta přes celou tvář, ne dva špunty nahoře.</summary>
        Flap,

        Round,
    }

    /// <summary>Kresba srsti. Přidává se přes hotové tělo, takže nemění siluetu.</summary>
    private enum Marks
    {
        None,

        /// <summary>Pruhy zebry.</summary>
        Stripes,

        /// <summary>Skvrny jaguára.</summary>
        Spots,

        /// <summary>Hříva lva — tmavý věnec kolem hlavy.</summary>
        Mane,

        /// <summary>Hrb velblouda.</summary>
        Hump,

        /// <summary>Chobot slona.</summary>
        Trunk,

        /// <summary>Hřeben štětin po hřbetě (kanec).</summary>
        Bristles,

        /// <summary>Bílý pruh přes hlavu (jezevec).</summary>
        Blaze,
    }

    /// <summary>Čtyřnožec i s tím, čím se liší od ostatních čtyřnožců.</summary>
    private sealed record Beast
    {
        public Horns Horn { get; init; } = Horns.None;

        public Tails Tail { get; init; } = Tails.Thin;

        public Ears Ear { get; init; } = Ears.Small;

        public Marks Mark { get; init; } = Marks.None;

        /// <summary>
        /// Délka krku v pixelech. Nula je medvěd s hlavou v ramenou, tři je
        /// velbloud. Je to ten nejlevnější parametr, který na siluetě pozná
        /// i oko, které se nedívá.
        /// </summary>
        public int Neck { get; init; } = 1;

        /// <summary>
        /// Násobek délky nohou. Pod jednou je podsaditý (jezevec, jezevčí
        /// chůze u země), nad jednou vysokonohý (gazela).
        /// </summary>
        public float Legs { get; init; } = 1f;
    }

    // ---------------------------------------------------------------------
    // Seznam druhů
    // ---------------------------------------------------------------------

    private static Dictionary<string, FaunaShape> Build()
    {
        var shapes = new Dictionary<string, FaunaShape>(StringComparer.Ordinal);

        void Add(string id, int w, int h, FaunaAnchor anchor, Action<PixelCanvas, Pelt> draw) =>
            shapes[id] = new FaunaShape(w, h, anchor, draw);

        void Quad(string id, int w, int h, Beast beast) =>
            Add(id, w, h, FaunaAnchor.Ground, (c, p) => Quadruped(c, p, beast));

        void Sit(string id, int w, int h, int ears, bool tail) =>
            Add(id, w, h, FaunaAnchor.Ground, (c, p) => Sitter(c, p, ears, tail));

        void Flyer(
            string id,
            int w,
            int h,
            Color? beak = null,
            Color? wingTip = null,
            bool fingered = false,
            bool bigBeak = false,
            bool owlFace = false) =>
            Add(id, w, h, FaunaAnchor.Free,
                (c, p) => Bird(c, p, beak, wingTip, fingered, bigBeak, owlFace));

        // ----- čtyřnožci -----
        Quad("deer", 13, 12, new Beast
        {
            Horn = Horns.Antlers, Tail = Tails.Stub, Neck = 2, Legs = 1.1f,
        });
        Quad("reindeer", 13, 12, new Beast
        {
            Horn = Horns.Antlers, Tail = Tails.Stub, Neck = 2,
        });
        Quad("moose", 15, 13, new Beast
        {
            Horn = Horns.Palmate, Tail = Tails.Stub, Neck = 2, Legs = 1.15f,
        });
        Quad("camel", 13, 13, new Beast
        {
            Tail = Tails.Thin, Mark = Marks.Hump, Neck = 4, Legs = 1.2f,
        });
        Quad("gazelle", 12, 11, new Beast
        {
            Horn = Horns.Spikes, Tail = Tails.Stub, Neck = 2, Legs = 1.2f,
        });
        Quad("oryx", 13, 13, new Beast
        {
            Horn = Horns.Straight, Tail = Tails.Tuft, Neck = 2, Legs = 1.1f,
        });
        Quad("ibex", 12, 11, new Beast
        {
            Horn = Horns.Curved, Tail = Tails.Stub, Neck = 1,
        });
        Quad("chamois", 12, 11, new Beast
        {
            Horn = Horns.Spikes, Tail = Tails.Stub, Neck = 1,
        });
        Quad("zebra", 13, 11, new Beast
        {
            Tail = Tails.Tuft, Mark = Marks.Stripes, Neck = 2, Legs = 1.05f,
        });
        Quad("elephant", 16, 14, new Beast
        {
            Tail = Tails.Thin, Ear = Ears.Flap, Mark = Marks.Trunk, Neck = 0, Legs = 1.15f,
        });
        Quad("lion", 13, 11, new Beast
        {
            Tail = Tails.Tuft, Mark = Marks.Mane, Neck = 1,
        });
        Quad("jaguar", 13, 11, new Beast
        {
            Tail = Tails.Thin, Mark = Marks.Spots, Neck = 1, Legs = 0.85f,
        });
        Quad("wolf", 13, 11, new Beast
        {
            Tail = Tails.Bushy, Neck = 1,
        });
        Quad("fox", 12, 10, new Beast
        {
            Tail = Tails.Bushy, Neck = 1, Legs = 0.9f,
        });
        Quad("arctic_fox", 12, 10, new Beast
        {
            Tail = Tails.Bushy, Neck = 1, Legs = 0.9f,
        });
        Quad("fennec", 11, 10, new Beast
        {
            Tail = Tails.Bushy, Ear = Ears.Big, Neck = 1, Legs = 0.9f,
        });
        Quad("bear", 14, 11, new Beast
        {
            Tail = Tails.Stub, Ear = Ears.Round, Neck = 0, Legs = 0.85f,
        });
        Quad("polar_bear", 14, 11, new Beast
        {
            Tail = Tails.Stub, Ear = Ears.Round, Neck = 0, Legs = 0.85f,
        });
        Quad("boar", 12, 11, new Beast
        {
            Horn = Horns.Tusks, Tail = Tails.Stub, Mark = Marks.Bristles, Neck = 0, Legs = 0.8f,
        });
        Quad("badger", 11, 9, new Beast
        {
            Tail = Tails.Stub, Mark = Marks.Blaze, Neck = 0, Legs = 0.7f,
        });

        // ----- drobní, co sedí -----
        Sit("rabbit", 8, 9, ears: 3, tail: true);
        Sit("snow_hare", 9, 10, ears: 3, tail: true);
        Sit("marmot", 8, 9, ears: 2, tail: false);
        Add("frog", 8, 6, FaunaAnchor.Ground, (c, p) => Frog(c, p));
        Add("monkey", 9, 10, FaunaAnchor.Ground, (c, p) => Monkey(c, p));

        // ----- ptáci v letu (shora) -----
        Flyer("bird", 9, 5);
        Flyer("crow", 10, 7);
        Flyer("parrot", 10, 7, beak: new Color(226, 160, 47));
        Flyer("seagull", 12, 6, wingTip: Ink);
        Flyer("eagle", 13, 7, beak: new Color(226, 160, 47), fingered: true);
        Flyer("vulture", 13, 7, beak: new Color(185, 141, 92), fingered: true);
        Flyer("owl", 10, 7, owlFace: true);
        Flyer("toucan", 11, 7, beak: new Color(245, 210, 92), bigBeak: true);

        // ----- ptáci po svých (z boku) -----
        Add("penguin", 8, 11, FaunaAnchor.Ground, (c, _) => Penguin(c));
        Add("heron", 11, 14, FaunaAnchor.Ground, (c, p) => Wader(c, p, neck: 5));
        Add("ostrich", 12, 13, FaunaAnchor.Ground, (c, p) => Wader(c, p, neck: 4, plump: true));

        // ----- voda -----
        Add("fish_school", 9, 5, FaunaAnchor.Free, (c, p) => Fish(c, p));
        Add("whale", 18, 8, FaunaAnchor.Free, (c, p) => Whale(c, p));
        Add("jellyfish", 8, 9, FaunaAnchor.Free, (c, p) => Jellyfish(c, p));
        Add("seal", 13, 7, FaunaAnchor.Ground, (c, p) => Seal(c, p));
        Add("crocodile", 16, 6, FaunaAnchor.Ground, (c, p) => Crocodile(c, p));

        // ----- hmyz a plazi -----
        Add("firefly", 5, 5, FaunaAnchor.Free, (c, p) => Spark(c, p.Light));
        Add("glow_beetle", 7, 5, FaunaAnchor.Ground, (c, p) => Beetle(c, p));
        Add("butterfly", 9, 7, FaunaAnchor.Free, (c, p) => Butterfly(c, p, round: true));
        Add("ash_moth", 9, 6, FaunaAnchor.Free, (c, p) => Butterfly(c, p, round: false));
        Add("dragonfly", 10, 7, FaunaAnchor.Free, (c, p) => Dragonfly(c, p));
        Add("scorpion", 10, 7, FaunaAnchor.Ground, (c, p) => Scorpion(c, p));
        Add("desert_lizard", 11, 5, FaunaAnchor.Ground, (c, p) => Lizard(c, p));

        return shapes;
    }

    private static FaunaSprite Sit(string id, int w, int h, Pelt pelt, int ears, bool tail) =>
        new(IdFor(id), w, h, FaunaAnchor.Ground, c => Sitter(c, pelt, ears, tail));

    // ---------------------------------------------------------------------
    // Archetypy
    // ---------------------------------------------------------------------

    /// <summary>
    /// Čtyřnožec z boku, hledící doprava.
    ///
    /// <para>Kreslí se odspodu: stín, nohy, trup, krk, hlava, ozdoby. Pořadí
    /// není libovolné — zadní nohy musí zmizet pod trupem, jinak má zvíře
    /// osm nohou vedle sebe a vypadá jako stonožka.</para>
    /// </summary>
    private static void Quadruped(PixelCanvas c, Pelt p, Beast b)
    {
        int w = c.Width;
        int h = c.Height;

        // Plátno se dělí odshora: co má zvíře nad hlavou (rohy, uši), pak
        // hlava, pak trup, pak nohy, dole stín. Dřív se všechno počítalo
        // zlomky z výšky — a rohy pak vyčnívaly mimo plátno, kde je plátno
        // tiše zahodilo. Bezrohý kozorožec nespadne, jen není kozorožec.
        int topRoom = Math.Max(HornRoom(b.Horn), b.Ear == Ears.Big ? 3 : 1);
        int feetY = h - 2;

        int headTop = topRoom;
        int bodyTop = headTop + b.Neck;

        // Co zbude na trup a nohy, se dělí mezi ně. Poměr drží `Legs`:
        // jezevec u země má nohy sotva vidět, gazela stojí skoro jen na nich.
        int available = Math.Max(5, feetY - bodyTop + 1);
        int bodyHeight = Math.Clamp((int)MathF.Round(available * (0.58f / b.Legs)), 3, available - 2);
        int bodyBottom = bodyTop + bodyHeight - 1;
        int legTop = bodyBottom;

        int headW = Math.Max(3, (int)MathF.Round(w * 0.30f));

        // Huňatý ocas potřebuje místo vlevo. Bez toho ho plátno ořízlo celý —
        // a u lišky je to po hlavě ta nejnápadnější věc, kterou má.
        int bodyLeft = b.Tail == Tails.Bushy ? 3 : 1;
        int bodyRight = w - headW - 1;
        int bodyW = Math.Max(4, bodyRight - bodyLeft + 1);

        Shadow(c, w * 0.5f, h - 1, w * 0.40f);

        // Nohy. Zadní pár tmavší — na siluetě se tím oddělí od předního,
        // i když se překrývají.
        int legW = Math.Max(1, w / 10);
        Leg(c, bodyLeft + 1, legTop, feetY, legW, p.Shade);
        Leg(c, bodyRight - legW, legTop, feetY, legW, p.Shade);
        Leg(c, bodyLeft + legW + 2, legTop, feetY, legW, p.Coat);
        Leg(c, bodyRight - (legW * 2) - 2, legTop, feetY, legW, p.Coat);

        // Trup: hřbet do světla, břicho do stínu. Bez těch dvou řádků je to
        // obdélník; horní řádek je navíc o pixel zúžený, aby měl hřbet oblouk
        // a zvíře nevypadalo jako bedna na nohou.
        c.FillRect(bodyLeft, bodyTop + 1, bodyW, bodyHeight - 1, p.Coat);
        c.FillRect(bodyLeft + 1, bodyTop, bodyW - 2, 1, p.Light);
        c.FillRect(bodyLeft, bodyBottom, bodyW, 1, p.Shade);

        if (b.Mark == Marks.Hump)
        {
            // Hrb sedí nad plecemi, ne uprostřed hřbetu — velbloud ho nese vpředu.
            int humpW = Math.Max(3, bodyW / 2);
            int humpX = bodyRight - humpW;
            c.FillRect(humpX, bodyTop - 2, humpW, 2, p.Coat);
            c.FillRect(humpX + 1, bodyTop - 3, humpW - 2, 1, p.Coat);
            c.FillRect(humpX + 1, bodyTop - 3, humpW - 2, 1, p.Light);
        }

        if (b.Mark == Marks.Bristles)
        {
            for (int x = bodyLeft + 1; x <= bodyRight - 1; x += 2)
            {
                c.Blend(x, bodyTop - 1, Scale(p.Coat, 0.5f));
            }
        }

        if (b.Mark == Marks.Stripes)
        {
            // Pruhy nesmí přejet hřbet: se světlým horním řádkem zůstane vidět
            // oblý tvar, bez něj je ze zebry housenka.
            for (int x = bodyLeft + 2; x <= bodyRight - 1; x += 3)
            {
                c.FillRect(x, bodyTop + 1, 1, bodyHeight - 1, Scale(p.Coat, 0.35f));
            }
        }

        if (b.Mark == Marks.Spots)
        {
            var spot = Scale(p.Coat, 0.42f);
            for (int x = bodyLeft + 1; x < bodyRight - 1; x += 3)
            {
                c.Blend(x, bodyTop + 1, spot);
                c.Blend(x + 1, bodyBottom - 1, spot);
            }
        }

        Tail(c, p, b, bodyLeft, bodyTop, bodyBottom);

        // Krk a hlava.
        int headH = Math.Max(2, bodyHeight - 1);
        int headLeft = bodyRight;
        int neckW = Math.Max(2, headW - 1);

        c.FillRect(bodyRight - 1, headTop + 1, neckW, bodyTop - headTop + 2, p.Coat);
        c.FillRect(headLeft, headTop, headW - 1, headH, p.Coat);
        c.FillRect(headLeft, headTop, headW - 1, 1, p.Light);

        // Čenich přesahuje o pixel dopředu — bez něj je hlava kostka.
        int muzzleY = headTop + headH - 2;
        c.FillRect(headLeft + headW - 1, muzzleY, 1, 2, p.Shade);

        if (b.Mark == Marks.Trunk)
        {
            // Chobot visí od čenichu skoro k zemi a na konci se stáčí. Je to
            // jediná věc, podle které se slon pozná i jako silueta.
            int trunkX = headLeft + headW - 2;
            int trunkLen = feetY - muzzleY;
            c.FillRect(trunkX, muzzleY, 2, trunkLen, Scale(p.Coat, 0.72f));
            c.FillRect(trunkX + 1, muzzleY, 1, trunkLen, Scale(p.Coat, 0.50f));
            c.Blend(trunkX + 2, muzzleY + trunkLen - 1, Scale(p.Coat, 0.72f)); // stočený konec
        }

        if (b.Mark == Marks.Mane)
        {
            // Hříva je tmavý věnec kolem hlavy, ne čepice na ní — a musí být
            // opravdu tmavá, jinak ji paleta srovná zpátky do barvy srsti.
            var mane = Scale(p.Coat, 0.45f);
            c.FillRect(headLeft - 2, headTop - 1, headW + 1, headH + 2, mane);
            c.FillRect(headLeft + 1, headTop, headW - 2, headH, p.Coat);
            c.FillRect(headLeft + 1, headTop, headW - 2, 1, p.Light);
        }

        if (b.Mark == Marks.Blaze)
        {
            // Bílý pruh přes hlavu. Jezevec je jinak šedý váleček.
            c.FillRect(headLeft, headTop, headW - 1, 1, new Color(240, 240, 244));
            c.FillRect(headLeft + 1, headTop + 1, 1, headH - 1, new Color(240, 240, 244));
        }

        Ear(c, b, headLeft, headTop, headW, p);
        Horn(c, b, headLeft, headTop, headW, p);

        // Oko až úplně nakonec, aby ho nepřekryla hříva ani roh.
        c.Blend(headLeft + headW - 2, headTop + 1, p.Accent);
    }

    /// <summary>Kolik pixelů nad hlavou si ozdoba bere. Plátno podle toho začíná níž.</summary>
    private static int HornRoom(Horns horn) => horn switch
    {
        Horns.Antlers or Horns.Palmate => 3,
        Horns.Straight => 4,
        Horns.Spikes => 2,
        Horns.Curved => 3,
        _ => 1,
    };

    private static void Tail(PixelCanvas c, Pelt p, Beast b, int bodyLeft, int bodyTop, int bodyBottom)
    {
        int y = bodyTop + 1;

        switch (b.Tail)
        {
            case Tails.Stub:
                c.Blend(bodyLeft - 1, y, p.Coat);
                break;

            case Tails.Thin:
                c.FillRect(bodyLeft - 1, y, 1, bodyBottom - y, p.Shade);
                break;

            case Tails.Bushy:
                // Huňatý ocas je po hlavě to nejnápadnější, co liška má —
                // dělá skoro třetinu siluety a kreslí se proto v plné barvě.
                // Nese se zvednutý: svěšený splyne s nohama.
                c.FillRect(bodyLeft - 3, y - 1, 3, 3, p.Coat);
                c.FillRect(bodyLeft - 3, y - 1, 2, 1, p.Light);
                c.Blend(bodyLeft - 3, y - 2, p.Light);
                c.FillRect(bodyLeft - 1, y + 1, 1, 1, p.Shade);
                break;

            case Tails.Tuft:
                c.FillRect(bodyLeft - 1, y, 1, bodyBottom - y, p.Shade);
                c.Blend(bodyLeft - 1, bodyBottom, p.Accent);
                break;
        }
    }

    private static void Ear(PixelCanvas c, Beast b, int headLeft, int headTop, int headW, Pelt p)
    {
        switch (b.Ear)
        {
            case Ears.Small:
                c.Blend(headLeft, headTop - 1, p.Coat);
                break;

            case Ears.Round:
                c.Blend(headLeft, headTop - 1, p.Coat);
                c.Blend(headLeft + headW - 2, headTop - 1, p.Coat);
                break;

            case Ears.Big:
                // Fenek nese uši vztyčené nad hlavou. Kdyby se kreslily za ni,
                // vypadaly by jako hrb na krku — a přesně tak to vypadalo.
                c.FillRect(headLeft, headTop - 2, 1, 2, p.Coat);
                c.FillRect(headLeft + headW - 2, headTop - 2, 1, 2, p.Coat);
                c.Blend(headLeft, headTop - 2, p.Shade);
                c.Blend(headLeft + headW - 2, headTop - 2, p.Shade);
                break;

            case Ears.Flap:
                // Boltec leží na tváři, ne za ní. Posazený za hlavu splynul
                // s trupem a byl z něj svislý sloupek uprostřed slona.
                // Musí být i znatelně tmavší, jinak hlava jen ztloustne.
                c.FillRect(headLeft - 1, headTop, 2, headW, Scale(p.Coat, 0.55f));
                c.FillRect(headLeft - 1, headTop, 1, headW - 1, Scale(p.Coat, 0.74f));
                break;
        }
    }

    private static void Horn(PixelCanvas c, Beast b, int headLeft, int headTop, int headW, Pelt p)
    {
        // Roh musí být vidět proti srsti. Jednotná světlá kost fungovala na
        // srnci a na bílém přímorožci zmizela — bílé zvíře dostane roh tmavý,
        // tmavé zvíře světlý.
        bool paleCoat = p.Coat.R + p.Coat.G + p.Coat.B > 480;
        var horn = paleCoat
            ? new Color(78, 68, 58)
            : Mix(p.Light, new Color(240, 221, 187), 0.7f);
        int front = headLeft + headW - 3;

        switch (b.Horn)
        {
            case Horns.None:
                break;

            case Horns.Antlers:
                // Dvě větvičky vzhůru a jedna odbočka — paroží se pozná podle
                // toho, že se větví, ne podle toho, že je vysoké.
                c.FillRect(headLeft, headTop - 3, 1, 3, horn);
                c.FillRect(headLeft + 2, headTop - 3, 1, 3, horn);
                c.Blend(headLeft - 1, headTop - 3, horn);
                c.Blend(headLeft + 3, headTop - 2, horn);
                break;

            case Horns.Palmate:
                c.FillRect(headLeft - 2, headTop - 3, 2, 2, horn);
                c.FillRect(headLeft + 1, headTop - 3, 2, 2, horn);
                c.FillRect(headLeft, headTop - 2, 1, 2, horn);
                break;

            case Horns.Spikes:
                c.FillRect(headLeft + 1, headTop - 2, 1, 2, horn);
                c.Blend(headLeft + 2, headTop - 2, horn);
                break;

            case Horns.Curved:
                // Kozorožec nese rohy vzhůru a teprve pak dozadu. Ležely-li
                // rovnou na hřbetě, splynuly s ním a koza byla bezrohá.
                c.FillRect(headLeft + 1, headTop - 2, 1, 2, horn);
                c.Blend(headLeft, headTop - 3, horn);
                c.Blend(headLeft - 1, headTop - 3, horn);
                c.Blend(headLeft - 2, headTop - 2, horn);
                break;

            case Horns.Straight:
                c.FillRect(headLeft + 1, headTop - 4, 1, 4, horn);
                c.FillRect(headLeft + 2, headTop - 3, 1, 3, horn);
                break;

            case Horns.Tusks:
                c.Blend(front + 2, headTop + 1, horn);
                c.Blend(front + 2, headTop, horn);
                break;
        }
    }

    /// <summary>
    /// Drobný savec, co sedí na zadku — králík, zajíc, svišť.
    ///
    /// <para>Kreslí se odshora, od uší. Dřív se počítalo od spodního okraje
    /// a uši vycházely na záporný řádek, kde je plátno tiše zahodilo: králík
    /// bez uší je jen chlupatá hrouda a od svišťě se nepozná.</para>
    /// </summary>
    private static void Sitter(PixelCanvas c, Pelt p, int ears, bool tail)
    {
        int w = c.Width;
        int h = c.Height;

        Shadow(c, w * 0.5f, h - 1, w * 0.36f);

        int headTop = ears;
        int bodyTop = headTop + 1;
        int bodyBottom = h - 2;

        // Tělo je kapka: široké dole, užší nahoře.
        c.FillRect(1, bodyTop + 1, w - 3, bodyBottom - bodyTop, p.Coat);
        c.FillRect(2, bodyTop, w - 5, 1, p.Coat);
        c.FillRect(1, bodyBottom, w - 3, 1, p.Shade);

        // Hlava vpředu vpravo.
        c.FillRect(w - 4, headTop, 3, 3, p.Coat);
        c.FillRect(w - 4, headTop, 3, 1, p.Light);
        c.Blend(w - 1, headTop + 2, p.Shade); // čumák

        // Uši. Jejich délka je celý rozdíl mezi králíkem a sviští, takže
        // musí být vidět: vnitřek tmavší, špička světlá.
        c.FillRect(w - 4, 0, 1, ears, p.Coat);
        c.FillRect(w - 3, 1, 1, Math.Max(1, ears - 1), p.Shade);
        c.Blend(w - 4, 0, p.Light);

        if (tail)
        {
            c.FillRect(0, bodyBottom - 2, 1, 2, p.Light);
        }

        c.Blend(w - 2, headTop + 1, p.Accent); // oko
    }

    private static void Frog(PixelCanvas c, Pelt p)
    {
        int w = c.Width;
        int h = c.Height;

        Shadow(c, w * 0.5f, h - 1, w * 0.38f);

        c.FillRect(1, h - 3, w - 2, 2, p.Coat);   // trup u země
        c.FillRect(2, h - 4, w - 4, 1, p.Coat);
        c.FillRect(1, h - 2, w - 2, 1, p.Shade);
        c.FillRect(0, h - 3, 2, 2, p.Shade);      // skrčené zadní stehno

        // Oči na temeni. Žába se pozná podle nich dřív než podle těla — bez
        // nich je to zelený váleček.
        c.Blend(w - 3, h - 5, p.Light);
        c.Blend(w - 2, h - 5, p.Light);
        c.Blend(w - 3, h - 5, Ink);
        c.Blend(w - 1, h - 4, p.Shade);           // tlama
    }

    private static void Monkey(PixelCanvas c, Pelt p)
    {
        int w = c.Width;
        int h = c.Height;

        Shadow(c, w * 0.5f, h - 1, w * 0.32f);

        c.FillRect(2, h - 6, 4, 5, p.Coat);          // trup
        c.FillRect(2, h - 6, 4, 1, p.Light);
        c.FillRect(2, h - 2, 1, 1, p.Shade);         // nohy
        c.FillRect(5, h - 2, 1, 1, p.Shade);
        c.FillRect(6, h - 6, 1, 3, p.Shade);         // paže

        c.FillRect(3, h - 9, 3, 3, p.Coat);          // hlava
        c.FillRect(4, h - 8, 2, 2, Mix(p.Light, Color.White, 0.3f)); // světlá tvář
        c.Blend(2, h - 9, p.Coat);                   // uši
        c.Blend(6, h - 9, p.Coat);
        c.Blend(5, h - 8, p.Accent);                 // oko

        // Ocas do oblouku. Na devíti pixelech je to jediné, co odliší opici
        // od jakéhokoli jiného chlupatého drobka.
        c.FillRect(0, h - 7, 1, 4, p.Shade);
        c.Blend(1, h - 7, p.Shade);
        c.Blend(0, h - 8, p.Shade);
    }

    /// <summary>
    /// Pták v letu, shora. Ne z boku: letící pták se nad krajinou vidí shora
    /// nebo zdola a jeho poznávací znamení je <b>rozpětí</b>, ne profil.
    ///
    /// <para>Křídlo musí být aspoň dva pixely silné. Jednopixelová čára se na
    /// pozadí ztratí a z ptáka zbude tečka s vousy — přesně ten čtvereček,
    /// kvůli kterému celá tahle práce vznikla.</para>
    /// </summary>
    private static void Bird(
        PixelCanvas c,
        Pelt p,
        Color? beak,
        Color? wingTip,
        bool fingered,
        bool bigBeak,
        bool owlFace)
    {
        int w = c.Width;
        int h = c.Height;
        int midX = w / 2;

        // Sova má krátká široká křídla u těla, dravec dlouhá. Tělo sedí tak,
        // aby se pod křídla vešel ocas.
        int bodyY = owlFace ? h / 2 : (h / 2) + (h >= 7 ? 1 : 0);
        int span = midX - 1;

        for (int i = 1; i <= span; i++)
        {
            // Křídlo stoupá od těla ke špičce; poslední článek se zlomí zpět,
            // takže vznikne „m", a ne stříška.
            float t = i / (float)span;
            int rise = (int)MathF.Round(t * (bodyY - (owlFace ? 1 : 0)));
            int y = Math.Max(0, bodyY - rise);
            var tone = i >= span && wingTip is { } tip ? tip : p.Coat;

            c.FillRect(midX - i, y, 1, 2, tone);
            c.FillRect(midX + i, y, 1, 2, tone);

            if (i == 1)
            {
                c.Blend(midX - i, y, p.Light);
                c.Blend(midX + i, y, p.Light);
            }

            if (fingered && i >= span - 1)
            {
                // Roztažené letky na konci křídla. Dravec je má, racek ne —
                // a je to jediný rozdíl, který je na deseti pixelech vidět.
                c.Blend(midX - i, y + 2, tone);
                c.Blend(midX + i, y + 2, tone);
            }
        }

        // Tělo a ocas.
        c.FillRect(midX - 1, bodyY - 1, 3, 3, p.Coat);
        c.FillRect(midX - 1, bodyY - 1, 3, 1, p.Light);
        c.FillRect(midX - 1, bodyY + 2, 3, 1, p.Shade);
        c.Blend(midX, bodyY + 3, p.Shade);

        if (owlFace)
        {
            // Sova nemá krk a dívá se dopředu. Na deseti pixelech ji udělají
            // dvě světlá kola s tmavým středem — nic jiného tam nezbude.
            var face = Mix(p.Light, Color.White, 0.45f);
            c.FillRect(midX - 1, bodyY - 1, 3, 2, face);
            c.Blend(midX - 1, bodyY - 1, Ink);
            c.Blend(midX + 1, bodyY - 1, Ink);
            c.Blend(midX, bodyY, new Color(226, 160, 47));
            return;
        }

        // Hlava a zobák vpředu. Zobák jde o řádek níž než konec křídla —
        // na jedné úrovni se s ním pral a z tukana byla barevná šmouha.
        c.FillRect(midX, bodyY - 2, 2, 2, p.Coat);
        if (beak is { } bill)
        {
            c.Blend(midX + 2, bodyY - 1, bill);
            if (bigBeak)
            {
                // Tukan je zobák s ptákem vzadu. Bez toho je to papoušek.
                c.FillRect(midX + 2, bodyY - 1, 3, 2, bill);
                c.Blend(midX + 4, bodyY, Scale(bill, 0.7f));
            }
        }

        c.Blend(midX + 1, bodyY - 2, Ink); // oko
    }

    private static void Penguin(PixelCanvas c)
    {
        int w = c.Width;
        int h = c.Height;
        var back = new Color(43, 47, 56);
        var belly = new Color(240, 240, 244);
        var bill = new Color(226, 160, 47);

        Shadow(c, w * 0.5f, h - 1, w * 0.34f);

        c.FillRect(1, 2, w - 2, h - 4, back);       // záda
        c.FillRect(3, 4, w - 5, h - 7, belly);      // bílé bříško
        c.FillRect(2, 1, w - 4, 3, back);           // hlava
        c.Blend(w - 3, 2, bill);                    // zobák
        c.Blend(w - 4, 2, Ink);                     // oko
        c.FillRect(1, 4, 1, 4, back);               // ploutev
        c.FillRect(2, h - 2, 2, 1, bill);           // nohy
        c.FillRect(w - 4, h - 2, 2, 1, bill);
    }

    /// <summary>Brodivý pták z boku: dlouhé nohy, dlouhý krk, malé tělo.</summary>
    private static void Wader(PixelCanvas c, Pelt p, int neck, bool plump = false)
    {
        int w = c.Width;
        int h = c.Height;
        int bodyY = h - 6;

        Shadow(c, w * 0.5f, h - 1, w * 0.28f);

        // Nohy — právě ony z brodivého ptáka dělají brodivého ptáka.
        var leg = Mix(p.Shade, new Color(226, 160, 47), 0.35f);
        c.FillRect(w / 2 - 2, bodyY + 3, 1, h - bodyY - 4, leg);
        c.FillRect(w / 2 + 1, bodyY + 3, 1, h - bodyY - 4, leg);

        int bodyW = plump ? w - 4 : w - 6;
        c.FillRect(2, bodyY, bodyW, 4, p.Coat);
        c.FillRect(2, bodyY, bodyW, 1, p.Light);
        c.FillRect(2, bodyY + 3, bodyW, 1, p.Shade);
        c.Blend(1, bodyY + 1, p.Shade); // ocas

        int neckX = 2 + bodyW - 2;
        c.FillRect(neckX, bodyY - neck, 1, neck + 1, p.Coat);
        c.FillRect(neckX, bodyY - neck - 2, 2, 2, p.Coat);            // hlava
        c.Blend(neckX + 2, bodyY - neck - 1, new Color(226, 160, 47)); // zobák
        c.Blend(neckX + 1, bodyY - neck - 2, Ink);                     // oko
    }

    private static void Fish(PixelCanvas c, Pelt p)
    {
        int w = c.Width;
        int h = c.Height;
        int midY = h / 2;

        c.FillRect(2, midY - 1, w - 4, 3, p.Coat);
        c.FillRect(2, midY - 1, w - 4, 1, p.Light);
        c.FillTriangle(0, midY - 2, 0, midY + 2, 3, midY, p.Shade); // ocasní ploutev
        c.Blend(w - 2, midY, p.Coat);
        c.Blend(w - 3, midY - 1, Ink);                               // oko
    }

    private static void Whale(PixelCanvas c, Pelt p)
    {
        int w = c.Width;
        int h = c.Height;
        int midY = h / 2;

        // Nad hladinou je vidět hřbet, ne celá velryba. Tvar se proto ke krajům
        // ztenčuje — plný obdélník vypadal jako ponorka.
        c.FillRect(5, midY, w - 7, 2, p.Coat);
        c.FillRect(6, midY - 1, w - 10, 1, p.Coat);
        c.FillRect(6, midY - 1, w - 10, 1, p.Light);
        c.FillRect(5, midY + 2, w - 9, 1, p.Shade);
        c.Blend(w - 2, midY, p.Coat);            // čenich
        c.Blend(w - 4, midY - 1, p.Light);       // hřbetní ploutev

        // Ocasní ploutev je rozdvojená. Trojúhelník z ní dělal příď lodi.
        c.FillRect(1, midY - 2, 2, 2, p.Shade);
        c.FillRect(1, midY + 1, 2, 2, p.Shade);
        c.FillRect(3, midY, 2, 2, p.Shade);

        // Fontánka. Bez ní je z velryby modrý pruh. Kreslí se přes Paint:
        // průsvitná bílá přes prázdno by se smíchala s černým podkladem plátna
        // a z tříště by byl šedý sloupek.
        var spray = new Color((byte)232, (byte)244, (byte)250, (byte)190);
        c.Paint(w - 6, midY - 2, spray);
        c.Paint(w - 7, midY - 3, spray);
        c.Paint(w - 5, midY - 3, spray);
        c.Paint(w - 6, midY - 4, new Color((byte)232, (byte)244, (byte)250, (byte)130));
    }

    private static void Jellyfish(PixelCanvas c, Pelt p)
    {
        int w = c.Width;
        int h = c.Height;

        // Fialová, ne šeď: světlounký odstín paleta srovnala na šedou a ze
        // sasanky byl hřib. Zvon je proto sytější, než jaká je barva v datech.
        var bell = Mix(p.Coat, new Color(145, 96, 184), 0.55f);

        c.PaintRect(1, 1, w - 2, 2, new Color(bell.R, bell.G, bell.B, (byte)225));
        c.PaintRect(2, 0, w - 4, 1, Mix(bell, Color.White, 0.45f));
        c.PaintRect(0, 2, w, 1, new Color(bell.R, bell.G, bell.B, (byte)205));
        c.PaintRect(1, 3, w - 2, 1, new Color(bell.R, bell.G, bell.B, (byte)165));

        // Chapadla: tenká, různě dlouhá a průsvitná. Tři tlusté sloupky
        // vypadaly jako nohy stolu.
        var tentacle = new Color(bell.R, bell.G, bell.B, (byte)140);
        for (int x = 1; x < w - 1; x += 2)
        {
            c.PaintRect(x, 4, 1, 2 + (x % 3) + (h - 7), tentacle);
        }
    }

    private static void Seal(PixelCanvas c, Pelt p)
    {
        int w = c.Width;
        int h = c.Height;

        Shadow(c, w * 0.5f, h - 1, w * 0.44f);

        // Tuleň leží. Tvar je banán: hlava nahoře vpravo, ocas zvednutý vlevo.
        c.FillRect(2, h - 4, w - 4, 3, p.Coat);
        c.FillRect(2, h - 4, w - 4, 1, p.Light);
        c.FillRect(w - 5, h - 5, 3, 2, p.Coat);   // krk
        c.FillRect(w - 4, h - 6, 3, 2, p.Coat);   // hlava
        c.Blend(w - 1, h - 5, p.Shade);           // čenich
        c.Blend(w - 2, h - 6, p.Accent);          // oko
        c.FillTriangle(0, h - 5, 0, h - 2, 3, h - 3, p.Shade); // ocasní ploutev
        c.FillRect(w - 7, h - 2, 2, 1, p.Shade);  // přední ploutev
    }

    private static void Crocodile(PixelCanvas c, Pelt p)
    {
        int w = c.Width;
        int h = c.Height;

        Shadow(c, w * 0.5f, h - 1, w * 0.46f);

        // Nízký dlouhý tvar u země — krokodýl je z profilu skoro čára.
        c.FillRect(3, h - 4, w - 6, 2, p.Coat);
        c.FillRect(3, h - 4, w - 6, 1, p.Light);
        c.FillRect(w - 5, h - 4, 4, 2, p.Coat);   // hlava
        c.FillRect(w - 5, h - 3, 4, 1, p.Shade);  // čelist
        c.Blend(w - 3, h - 5, p.Accent);          // oko nad hladinou

        // Zubatý hřbet a ocas, který se ztenčuje.
        for (int x = 4; x < w - 5; x += 2)
        {
            c.Blend(x, h - 5, p.Shade);
        }

        c.FillRect(1, h - 3, 3, 1, p.Coat);
        c.Blend(0, h - 3, p.Shade);

        c.FillRect(5, h - 2, 1, 1, p.Shade);      // nohy
        c.FillRect(w - 7, h - 2, 1, 1, p.Shade);
    }

    private static void Lizard(PixelCanvas c, Pelt p)
    {
        int w = c.Width;
        int h = c.Height;

        Shadow(c, w * 0.5f, h - 1, w * 0.34f);

        c.FillRect(4, h - 3, w - 7, 2, p.Coat);
        c.FillRect(4, h - 3, w - 7, 1, p.Light);

        // Klínovitá hlava a ocas, který se ztenčuje do špičky — ještěrka je
        // z nadhledu hlavně ten přechod, ne váleček.
        c.FillRect(w - 3, h - 3, 2, 2, p.Coat);
        c.Blend(w - 1, h - 3, p.Shade);
        c.Blend(w - 2, h - 3, p.Accent);          // oko

        c.FillRect(2, h - 2, 2, 1, p.Coat);       // ocas
        c.Blend(1, h - 2, p.Shade);
        c.Blend(0, h - 2, p.Shade);

        // Nožky do stran — ještěrka je nese od těla, ne pod ním.
        c.Blend(5, h - 4, p.Shade);
        c.Blend(w - 5, h - 4, p.Shade);
        c.Blend(5, h - 1, p.Shade);
        c.Blend(w - 5, h - 1, p.Shade);
    }

    private static void Beetle(PixelCanvas c, Pelt p)
    {
        int w = c.Width;
        int h = c.Height;

        Shadow(c, w * 0.5f, h - 1, w * 0.34f);

        c.FillRect(1, h - 4, w - 2, 3, p.Shade);
        c.FillRect(2, h - 4, w - 3, 2, p.Coat);
        c.FillRect(w / 2, h - 4, 1, 3, p.Shade);   // šev krovek
        c.Blend(w - 1, h - 3, p.Shade);            // hlava

        // Světélko na zadečku. Kvůli němu brouk ve hře je — v noci je z něj
        // jiskra v trávě, ne tmavá tečka. Proto je vzadu, ne přes celé tělo.
        c.Paint(0, h - 3, new Color((byte)240, (byte)255, (byte)190, (byte)235));
        c.Paint(0, h - 2, new Color((byte)240, (byte)255, (byte)190, (byte)160));
    }

    private static void Spark(PixelCanvas c, Color glow)
    {
        int w = c.Width;
        int h = c.Height;
        float cx = w * 0.5f;
        float cy = h * 0.5f;

        // Světluška je jen bod se svatozáří; tělo by na pěti pixelech zmizelo.
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float d = MathF.Sqrt(((x + 0.5f - cx) * (x + 0.5f - cx)) + ((y + 0.5f - cy) * (y + 0.5f - cy)));
                if (d > 2.2f)
                {
                    continue;
                }

                byte alpha = (byte)Math.Clamp((int)(235 - (d * 95f)), 0, 255);
                c.Paint(x, y, new Color(glow.R, glow.G, glow.B, alpha));
            }
        }

        c.Paint((int)cx, (int)cy, new Color(255, 255, 225));
    }

    private static void Butterfly(PixelCanvas c, Pelt p, bool round)
    {
        int w = c.Width;
        int h = c.Height;
        int midX = w / 2;
        int midY = h / 2;

        // Čtyři křídla shora. Musí se dotýkat těla a mít <b>zkosený</b> okraj:
        // dva plné obdélníky vypadaly jako dvě cedulky letící vedle sebe.
        int topH = round ? 3 : 2;
        int topW = midX - 1;
        var lower = Mix(p.Coat, p.Shade, 0.55f);

        for (int i = 0; i < topW; i++)
        {
            // Vnější konec křídla je kratší → kresba dostane tvar.
            int tall = topH - (i == topW - 1 ? 1 : 0);
            c.FillRect(midX - 1 - i, midY - tall, 1, tall, p.Coat);
            c.FillRect(midX + 1 + i, midY - tall, 1, tall, p.Coat);
            c.Blend(midX - 1 - i, midY - tall, p.Light);
            c.Blend(midX + 1 + i, midY - tall, p.Light);
        }

        int lowW = round ? topW - 1 : topW - 2;
        for (int i = 0; i < lowW; i++)
        {
            int tall = 2 - (i == lowW - 1 ? 1 : 0);
            c.FillRect(midX - 1 - i, midY, 1, tall, lower);
            c.FillRect(midX + 1 + i, midY, 1, tall, lower);
        }

        // Tělo přes celou výšku křídel a tykadla nahoru.
        c.FillRect(midX, midY - topH, 1, topH + 2, Scale(p.Shade, 0.65f));
        c.Blend(midX - 1, midY - topH - 1, p.Shade);
        c.Blend(midX + 1, midY - topH - 1, p.Shade);
    }

    private static void Dragonfly(PixelCanvas c, Pelt p)
    {
        int w = c.Width;
        int h = c.Height;
        int midY = h / 2;

        // Křídla jdou první a jsou tenká. Dvoupixelové plochy je udělaly
        // hlavním tvarem a z vážky byl mrak — přitom vážka je hlavně to
        // dlouhé tenké tělo.
        var wing = new Color((byte)222, (byte)240, (byte)246, (byte)135);
        c.PaintRect(w - 7, midY - 2, 4, 1, wing);
        c.PaintRect(w - 7, midY + 2, 4, 1, wing);
        c.PaintRect(w - 5, midY - 3, 3, 1, wing);
        c.PaintRect(w - 5, midY + 3, 3, 1, wing);

        c.FillRect(1, midY, w - 4, 1, p.Coat);      // zadeček
        c.Blend(1, midY, p.Shade);
        c.FillRect(w - 4, midY - 1, 2, 3, p.Coat);  // hruď
        c.FillRect(w - 2, midY - 1, 2, 2, p.Light); // hlava s velkýma očima
        c.Blend(w - 1, midY, Ink);
    }

    private static void Scorpion(PixelCanvas c, Pelt p)
    {
        int w = c.Width;
        int h = c.Height;

        Shadow(c, w * 0.5f, h - 1, w * 0.30f);

        // Ocas se zvedá nad hřbet a stáčí dopředu. Kreslí se první, aby ho
        // tělo nepřekrylo; svěšený ocas splyne s tělem a ze štíra je šváb.
        var tail = Scale(p.Coat, 0.8f);
        c.Blend(2, h - 4, tail);
        c.Blend(2, h - 5, tail);
        c.Blend(3, h - 6, tail);
        c.Blend(4, h - 6, tail);
        c.Blend(5, h - 5, p.Accent); // žihadlo míří dopředu

        c.FillRect(3, h - 3, w - 6, 2, p.Coat);   // zavalité tělo
        c.FillRect(3, h - 3, w - 6, 1, p.Light);

        // Klepeta vpředu, každé o dvou článcích. Bez nich je to brouk.
        c.FillRect(w - 4, h - 4, 2, 1, p.Coat);
        c.FillRect(w - 4, h - 2, 2, 1, p.Coat);
        c.Blend(w - 2, h - 5, p.Shade);
        c.Blend(w - 2, h - 1, p.Shade);

        for (int x = 4; x < w - 4; x += 2)
        {
            c.Blend(x, h - 1, p.Shade); // nožky
        }
    }

    // ---------------------------------------------------------------------
    // Drobné nástroje
    // ---------------------------------------------------------------------

    /// <summary>Nohy: svislý sloupek s tmavším kopýtkem na konci.</summary>
    private static void Leg(PixelCanvas c, int x, int top, int bottom, int width, Color color)
    {
        c.FillRect(x, top, width, bottom - top + 1, color);
        c.FillRect(x, bottom, width, 1, Scale(color, 0.6f));
    }

    /// <summary>
    /// Stín u paty. Jeden pixel vysoký a užší, než je zvíře široké — plný
    /// ovál pod tvorem vypadá jako díra v zemi.
    /// </summary>
    private static void Shadow(PixelCanvas c, float centerX, int y, float radius)
    {
        int from = (int)MathF.Round(centerX - radius);
        int to = (int)MathF.Round(centerX + radius);
        for (int x = from; x <= to; x++)
        {
            c.Blend(x, y, GroundShadow);
        }
    }
}
