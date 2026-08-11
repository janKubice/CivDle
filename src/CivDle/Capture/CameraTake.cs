using Microsoft.Xna.Framework;

namespace CivDle.Capture;

/// <summary>
/// Zaznamenaná jízda kamery — „záběr", který se natočí naživo a pak přehraje
/// při renderu videa.
///
/// <para>Proč to takhle: hráč chce video <b>v plné kvalitě a bez LOD</b>, jenže
/// takový snímek se ve 4K nevyrenderuje za šestnáct milisekund. V reálném čase
/// se to tedy natočit nedá. Dá se ale natočit <em>pohyb kamery</em> — ten je
/// levný — a pak scénu vyrenderovat mimo reálný čas, snímek po snímku, každý
/// tak dlouho, jak potřebuje. Výsledek se přehraje v 60 fps, i když se každý
/// snímek počítal půl vteřiny.</para>
///
/// <para>Ukládají se jen klíče, ne každý snímek: stojící kamera se scvrkne na
/// dva body a plynulý přejezd se lineárně dopočítá. Bez toho by z půlminutové
/// jízdy byly dva tisíce záznamů, ve kterých je devadesát procent šumu z chvění
/// myši.</para>
///
/// <para>Vrstva: čistá data o kameře, žádná grafika — tedy testovatelné.</para>
/// </summary>
public sealed class CameraTake
{
    /// <summary>O kolik se musí kamera hnout, aby to byl nový klíč (world pixely).</summary>
    private const float PositionEpsilon = 0.5f;

    /// <summary>O kolik se musí změnit zoom, aby to byl nový klíč.</summary>
    private const float ZoomEpsilon = 0.001f;

    /// <summary>Jeden bod jízdy: kde kamera byla a jak byla přiblížená.</summary>
    public readonly record struct Key(double Time, Vector2 Position, float Zoom);

    private readonly List<Key> _keys = new();

    /// <summary>Kolik klíčů záběr má.</summary>
    public int KeyCount => _keys.Count;

    /// <summary>Je záběr prázdný (nic se nenatočilo)?</summary>
    public bool IsEmpty => _keys.Count == 0;

    /// <summary>Délka záběru v sekundách.</summary>
    public double Duration => _keys.Count == 0 ? 0 : _keys[^1].Time;

    /// <summary>Zahodí natočené a začne znovu.</summary>
    public void Clear() => _keys.Clear();

    /// <summary>
    /// Zaznamená polohu kamery v daném čase. Volá se každý snímek při natáčení.
    ///
    /// <para>Když se kamera od posledního klíče prakticky nehnula, jen se
    /// posune čas toho klíče — z nehybného záběru tak zůstanou dva body místo
    /// tisícovky.</para>
    /// </summary>
    public void Record(double time, Vector2 position, float zoom)
    {
        if (_keys.Count > 0)
        {
            var last = _keys[^1];
            bool moved = Vector2.DistanceSquared(last.Position, position) > PositionEpsilon * PositionEpsilon
                || Math.Abs(last.Zoom - zoom) > ZoomEpsilon;

            if (!moved)
            {
                // Druhý klíč na témž místě jen prodlouží stání; třetí ho posune.
                if (_keys.Count >= 2 && Same(_keys[^2], last))
                {
                    _keys[^1] = last with { Time = time };
                    return;
                }

                _keys.Add(new Key(time, position, zoom));
                return;
            }
        }

        _keys.Add(new Key(time, position, zoom));
    }

    /// <summary>
    /// Kde je kamera v daném čase. Mezi klíči lineárně, za koncem stojí na
    /// posledním — přetečení o zlomek snímku nesmí vrátit nesmysl.
    /// </summary>
    public Key Sample(double time)
    {
        if (_keys.Count == 0)
        {
            return new Key(0, Vector2.Zero, 1f);
        }

        if (time <= _keys[0].Time)
        {
            return _keys[0] with { Time = time };
        }

        if (time >= _keys[^1].Time)
        {
            return _keys[^1] with { Time = time };
        }

        int index = FindSegment(time);
        var from = _keys[index];
        var to = _keys[index + 1];
        double span = to.Time - from.Time;
        float t = span <= 0 ? 0f : (float)((time - from.Time) / span);

        return new Key(
            time,
            Vector2.Lerp(from.Position, to.Position, t),
            MathHelper.Lerp(from.Zoom, to.Zoom, t));
    }

    /// <summary>Poslední klíč s časem nejvýš <paramref name="time"/> (binárně).</summary>
    private int FindSegment(double time)
    {
        int low = 0;
        int high = _keys.Count - 1;
        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            if (_keys[mid].Time <= time)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return Math.Min(low, _keys.Count - 2);
    }

    private static bool Same(Key a, Key b) =>
        Vector2.DistanceSquared(a.Position, b.Position) <= PositionEpsilon * PositionEpsilon
        && Math.Abs(a.Zoom - b.Zoom) <= ZoomEpsilon;
}
