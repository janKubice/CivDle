using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Melodie zvonohry — osm tónů, které si hráč přerovná, jak chce.
///
/// <para>Proč to ve hře je: idle hra je tichá práce s čísly. Zvonohra je
/// jediné místo, kde hráč do světa vloží něco <b>svého</b>, co není optimum —
/// a co pozná i kamarád, kterému pošle save. Osm čísel v savu, žádný zvukový
/// soubor: tóny se generují.</para>
///
/// <para>Vrstva: čistý stav simulace. O zvuku neví nic — jen drží, co se má
/// zahrát; kdy a jak to zní, řeší audio vrstva.</para>
/// </summary>
public sealed class Carillon
{
    /// <summary>Kolik tónů melodie má. Osm je taktů akorát na to, aby to šlo zapamatovat.</summary>
    public const int NoteCount = 8;

    /// <summary>Kolik stupňů stupnice je k dispozici (oktáva včetně horního tónu).</summary>
    public const int Degrees = 8;

    /// <summary>Hodnota tónu, který se nezahraje — pauza je součást melodie.</summary>
    public const int Rest = -1;

    private readonly int[] _notes = new int[NoteCount];
    private readonly CarillonConfig _config;

    public Carillon(CarillonConfig config)
    {
        _config = config;
        Reset();
    }

    /// <summary>Melodie ke čtení (UI i přehrávač).</summary>
    public IReadOnlyList<int> Notes => _notes;

    /// <summary>Tón na daném místě: 0–7 stupeň stupnice, −1 pauza.</summary>
    public int this[int index] => _notes[index];

    /// <summary>Je melodie samá pauza? Pak není co hrát.</summary>
    public bool IsSilent
    {
        get
        {
            for (int i = 0; i < _notes.Length; i++)
            {
                if (_notes[i] != Rest)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Přepíše tón. Hodnota mimo rozsah se ořízne na pauzu — melodii mění UI
    /// a špatné číslo z něj nemá znamenat pád ani němý tón bez vysvětlení.
    /// </summary>
    public void Set(int index, int note)
    {
        if (index < 0 || index >= _notes.Length)
        {
            return;
        }

        _notes[index] = note >= 0 && note < Degrees ? note : Rest;
    }

    /// <summary>Vrátí melodii, se kterou zvonohra přišla z dat.</summary>
    public void Reset()
    {
        var tune = _config.DefaultTune;
        for (int i = 0; i < _notes.Length; i++)
        {
            int note = i < tune.Count ? tune[i] : Rest;
            _notes[i] = note >= 0 && note < Degrees ? note : Rest;
        }
    }

    /// <summary>Obnova ze savu. Kratší i delší melodie se snese — data se mohla změnit.</summary>
    public void Restore(IReadOnlyList<int> notes)
    {
        for (int i = 0; i < _notes.Length; i++)
        {
            int note = i < notes.Count ? notes[i] : Rest;
            _notes[i] = note >= 0 && note < Degrees ? note : Rest;
        }
    }
}
