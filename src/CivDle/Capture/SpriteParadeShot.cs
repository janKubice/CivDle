using CivDle.Core.Content;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Capture;

/// <summary>
/// Přehlídka: všechny sprity budov krouží ve čtyřech kolotočích a přes ně běží
/// titulek s tím, kolik toho hra vlastně má.
///
/// <para>Proč kolotoč a ne mřížka: mřížka je katalog — divák ji přečte za vteřinu
/// a víc už z ní nedostane. Kolotoč <b>nikdy neskončí</b>: pořád se objevuje něco
/// dalšího, a přesně ten pocit má číslo v titulku podepřít.</para>
///
/// <para>Hloubka je fingovaná plochým trikem: poloha po elipse, z ní kosinus jako
/// „jak daleko" a podle něj velikost, jas a pořadí kreslení. Skutečné 3D by
/// znamenalo shader a matice — na čtyři kruhy spritů zbytečná mašinerie.</para>
///
/// <para>Čísla se berou <b>z obsahu</b>, ne z ruky. Trailer nesmí slíbit devadesát
/// budov a hra jich mít osmdesát; když se do dat něco přidá, titulek se opraví
/// sám.</para>
/// </summary>
internal sealed class SpriteParadeShot : ITrailerShot
{
    private const double DurationSeconds = 9.0;

    /// <summary>Kolik kruhů spritů běží přes sebe.</summary>
    private const int Rows = 4;

    /// <summary>
    /// Kde mají kruhy střed (návrhové pixely). Spodní řada je schválně výš, než
    /// by symetrie chtěla — u dolní hrany ji ztmavení okrajů požíralo.
    /// </summary>
    private static readonly float[] RowY = { 150f, 372f, 738f, 946f };

    /// <summary>Jak rychle se který kruh otáčí (otáčky za sekundu, znaménko = směr).</summary>
    private static readonly float[] RowSpeed = { 0.055f, -0.042f, 0.048f, -0.036f };

    private const float RingRadiusX = 1180f;
    private const float RingRadiusY = 34f;
    private const float NearSize = 132f;
    private const float FarSize = 62f;

    /// <summary>Pruh, ve kterém sedí titulek i značka.</summary>
    private const float BandTop = 452f;
    private const float BandHeight = 214f;

    private const float FadeSeconds = 0.65f;

    private static readonly Color Gold = new(255, 226, 150);
    private static readonly Color Ink = new(232, 236, 244);

    private readonly TrailerCanvas _canvas;
    private readonly List<Texture2D>[] _rows = new List<Texture2D>[Rows];
    private readonly Texture2D? _logo;
    private readonly int _buildingCount;
    private readonly int _techCount;

    public SpriteParadeShot(TrailerCanvas canvas, SpriteLibrary sprites, GameContent content)
    {
        _canvas = canvas;
        _logo = sprites.Get("ui.logo");
        _techCount = content.Techs.Count;

        for (int i = 0; i < Rows; i++)
        {
            _rows[i] = new List<Texture2D>();
        }

        // Do řad se rozdává po jedné dokola, ne po blocích: jinak by v jednom
        // kruhu byly samé domy a v jiném samé doly.
        int taken = 0;
        for (int i = 0; i < content.Buildings.Count; i++)
        {
            var texture = sprites.Get($"building.{content.Buildings[i].Id}");
            if (texture is null)
            {
                continue;
            }

            _rows[taken % Rows].Add(texture);
            taken++;
        }

        _buildingCount = taken;
    }

    public string Name => "01-prehlidka";

    public int FrameCount => VideoTiming.FrameCount(DurationSeconds);

    public void DrawFrame(int frameIndex)
    {
        float time = (float)VideoTiming.TimeOfFrame(frameIndex);

        _canvas.Begin();
        _canvas.VerticalGradient(new Color(26, 31, 42), new Color(12, 14, 20));

        for (int row = 0; row < Rows; row++)
        {
            DrawRing(row, time);
        }

        _canvas.Vignette(0.5f);
        DrawHeadline(time);
        _canvas.FadeToBlack(FadeAmount(time));
        _canvas.End();
    }

    public void Dispose()
    {
        // Sprity ani písmo záběr nevlastní — patří knihovně, která je přežije.
    }

    /// <summary>
    /// Jeden kolotoč. Sprity se řadí podle hloubky, aby ty vzadu mizely za těmi
    /// vpředu — bez toho by se kruh rozpadl na plochou girlandu.
    /// </summary>
    private void DrawRing(int row, float time)
    {
        var textures = _rows[row];
        if (textures.Count == 0)
        {
            return;
        }

        float centerY = RowY[row];
        float phase = time * RowSpeed[row] * MathF.Tau + row * 0.7f;

        Span<int> order = textures.Count <= 128 ? stackalloc int[textures.Count] : new int[textures.Count];
        for (int i = 0; i < textures.Count; i++)
        {
            order[i] = i;
        }

        SortByDepth(order, textures.Count, phase);

        foreach (int i in order)
        {
            float angle = phase + MathF.Tau * i / textures.Count;
            float depth = MathF.Cos(angle); // +1 = úplně vpředu, −1 = za kruhem
            float near = (depth + 1f) * 0.5f;

            float x = TrailerCanvas.DesignWidth / 2f + MathF.Sin(angle) * RingRadiusX;
            float y = centerY + depth * RingRadiusY;
            float size = MathHelper.Lerp(FarSize, NearSize, near);

            // Vzdálené sprity se ztrácejí v pozadí — jinak by kruh vzadu tahal
            // oko stejně jako ten vepředu a nevznikla by hloubka.
            var tint = Color.Lerp(new Color(96, 106, 128), Color.White, near * near);
            _canvas.DrawSprite(textures[i], new Vector2(x, y), size, tint);
        }
    }

    /// <summary>Vzestupně podle hloubky (nejdřív to, co je nejdál).</summary>
    private static void SortByDepth(Span<int> order, int count, float phase)
    {
        for (int i = 1; i < count; i++)
        {
            int value = order[i];
            float key = MathF.Cos(phase + MathF.Tau * value / count);
            int j = i - 1;
            while (j >= 0 && MathF.Cos(phase + MathF.Tau * order[j] / count) > key)
            {
                order[j + 1] = order[j];
                j--;
            }

            order[j + 1] = value;
        }
    }

    /// <summary>
    /// Titulek na tmavém pruhu. Čísla se <b>natáčejí</b> od nuly — počítadlo je
    /// starý trailerový trik a funguje: oko sleduje pohyb a číslo si zapamatuje.
    ///
    /// <para>Rozvržení se počítá z <b>konečného</b> textu a číslo se do svého
    /// místa zarovnává zprava. Kdyby se středilo každý snímek zvlášť, celý řádek
    /// by při počítání poskakoval.</para>
    /// </summary>
    private void DrawHeadline(float time)
    {
        float appear = Ease(Progress(time, 0.35f, 1.0f));
        if (appear <= 0.001f)
        {
            return;
        }

        _canvas.Fill(0, BandTop, TrailerCanvas.DesignWidth, BandHeight, new Color(9, 12, 17) * (0.86f * appear));
        _canvas.Fill(0, BandTop, TrailerCanvas.DesignWidth, 2, Gold * (0.55f * appear));
        _canvas.Fill(0, BandTop + BandHeight - 2, TrailerCanvas.DesignWidth, 2, Gold * (0.55f * appear));

        string buildings = _buildingCount.ToString();
        string techs = _techCount.ToString();
        const string middle = "  BUILDINGS      ";
        const string tail = "  TECHNOLOGIES";

        string full = buildings + middle + techs + tail;
        float fontScale = _canvas.FitScale(full, TrailerCanvas.DesignWidth - 220f, 4.2f);
        float totalWidth = _canvas.MeasureWidth(full, fontScale);
        float x = (TrailerCanvas.DesignWidth - totalWidth) / 2f;
        float y = BandTop + 46f;

        var textColor = Ink * appear;
        var numberColor = Gold * appear;

        // Číslo doprava do svého místa, text za ním na pevných souřadnicích.
        float buildingsSlot = _canvas.MeasureWidth(buildings, fontScale);
        _canvas.DrawText(
            Counting(_buildingCount, time), x + buildingsSlot, y, fontScale, numberColor, rightAligned: true);
        x += buildingsSlot;

        _canvas.DrawText(middle, x, y, fontScale, textColor);
        x += _canvas.MeasureWidth(middle, fontScale);

        float techsSlot = _canvas.MeasureWidth(techs, fontScale);
        _canvas.DrawText(Counting(_techCount, time), x + techsSlot, y, fontScale, numberColor, rightAligned: true);
        x += techsSlot;

        _canvas.DrawText(tail, x, y, fontScale, textColor);

        DrawWordmark(appear);
    }

    /// <summary>
    /// Značka pod titulkem, uvnitř pruhu — ať je z každého vystřiženého kousku
    /// poznat, čí to je.
    ///
    /// <para>Dřív byla dole u spodní hrany a padala mezi sprity poslední řady;
    /// v pruhu má klid a vypadá jako záměr, ne jako přetisk.</para>
    /// </summary>
    private void DrawWordmark(float appear)
    {
        const float y = BandTop + BandHeight - 44f;
        const float scale = 1.5f;

        float width = _canvas.MeasureWidth("CivDle", scale);
        float logoSize = _logo is null ? 0f : 34f;
        float left = (TrailerCanvas.DesignWidth - (width + logoSize + 12f)) / 2f;

        if (_logo is not null)
        {
            _canvas.DrawSprite(
                _logo, new Vector2(left + logoSize / 2f, y + 13f), logoSize, Color.White * (0.8f * appear));
        }

        _canvas.DrawText("CivDle", left + logoSize + 12f, y, scale, new Color(150, 160, 180) * appear);
    }

    /// <summary>Mezistav počítadla v daném čase.</summary>
    private static string Counting(int target, float time)
    {
        float t = Ease(Progress(time, 0.5f, 1.7f));
        return Math.Max(1, (int)MathF.Round(target * t)).ToString();
    }

    private static float Progress(float time, float from, float length) =>
        Math.Clamp((time - from) / length, 0f, 1f);

    /// <summary>Zpomalení na konci — pohyb, který dojede, působí líp než ten, co sekne.</summary>
    private static float Ease(float t) => 1f - (1f - t) * (1f - t);

    private static float FadeAmount(float time)
    {
        float inAmount = 1f - Math.Clamp(time / FadeSeconds, 0f, 1f);
        float outAmount = 1f - Math.Clamp((float)(DurationSeconds - time) / FadeSeconds, 0f, 1f);
        return Math.Max(inAmount, outAmount);
    }
}
