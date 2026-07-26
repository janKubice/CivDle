using CivDle.Core.Content;
using Microsoft.Xna.Framework;

namespace CivDle.Screens;

/// <summary>
/// Rozmístění technologií do <b>souhvězdí</b>: hloubka v závislostech = prstenec
/// od středu (kořeny uprostřed, pokročilé na okraji), úhel = větev. Čistý výpočet
/// bez vykreslování — spočítá se jednou a pak se z něj jen čte (SRP: layout zvlášť,
/// kreslení zvlášť).
///
/// <para>Proč prstence místo sloupců: sloupcový graf s desítkami uzlů vypadá jako
/// pavučina a nutí scrollovat do dálky. Radiální rozvržení drží celý strom kolem
/// jednoho středu a větve se přirozeně rozbíhají ven.</para>
///
/// <para>Uzly v prstenci se řadí podle úhlu svých prerekvizit, takže navazující
/// technologie zůstane blízko té své — čáry se nekříží přes celý kruh. Poloměr
/// prstence roste i s počtem uzlů, aby se hvězdy nikdy nepřekrývaly.</para>
///
/// <para>Hloubka = nejdelší cesta od kořene. Cyklus (chybná data) se ošetří
/// strážcem — uzel skončí v prstenci 0, místo aby výpočet zacyklil.</para>
/// </summary>
public sealed class TechGraphLayout
{
    /// <summary>Průměr jádra hvězdy v pixelech.</summary>
    public const int StarSize = 18;

    /// <summary>Klikací (a hover) čtverec kolem středu hvězdy.</summary>
    public const int HitSize = 52;

    private const int FirstRing = 165;
    private const int RingSpacing = 165;
    private const float MinArcSpacing = 150f; // minimální rozestup sousedů po obvodu
    private const int Margin = 140;           // místo na popisky u krajních hvězd

    private readonly Vector2[] _centers;

    public TechGraphLayout(DefRegistry<TechDef> techs)
    {
        int count = techs.Count;
        _centers = new Vector2[count];
        if (count == 0)
        {
            Width = Height = 1;
            return;
        }

        var depth = new int[count];
        var state = new byte[count]; // 0 = nespočítáno, 1 = počítá se, 2 = hotovo
        int maxDepth = 0;
        for (int i = 0; i < count; i++)
        {
            depth[i] = ComputeDepth(techs, i, depth, state);
            maxDepth = Math.Max(maxDepth, depth[i]);
        }

        var angles = new float[count];
        float maxRadius = 0f;
        for (int ring = 0; ring <= maxDepth; ring++)
        {
            var members = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (depth[i] == ring)
                {
                    members.Add(i);
                }
            }

            if (members.Count == 0)
            {
                continue;
            }

            // Uzel drž u své prerekvizity — jinak by čáry vedly napříč celým kruhem.
            members.Sort((a, b) =>
            {
                int byAngle = ParentAngle(techs, a, angles).CompareTo(ParentAngle(techs, b, angles));
                return byAngle != 0 ? byAngle : a.CompareTo(b);
            });

            // Poloměr roste s prstencem i s počtem uzlů — hvězdy se nikdy nepřekryjí.
            float radius = ring == 0 && members.Count == 1
                ? 0f
                : Math.Max(FirstRing + (ring - 1) * RingSpacing,
                           members.Count * MinArcSpacing / MathHelper.TwoPi);
            maxRadius = Math.Max(maxRadius, radius);

            float step = MathHelper.TwoPi / members.Count;
            float phase = ParentAngle(techs, members[0], angles);
            for (int k = 0; k < members.Count; k++)
            {
                float angle = phase + k * step;
                angles[members[k]] = angle;
                _centers[members[k]] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            }
        }

        int side = (int)(maxRadius + Margin) * 2;
        Width = Height = side;

        // Střed souhvězdí posadíme doprostřed plátna, ať se s ním dá počítat jako
        // s obyčejným obdélníkem (posun, ořez).
        var origin = new Vector2(side / 2f, side / 2f);
        for (int i = 0; i < count; i++)
        {
            _centers[i] += origin;
        }
    }

    /// <summary>Celková šířka plátna souhvězdí (pro omezení posunu).</summary>
    public int Width { get; }

    /// <summary>Celková výška plátna souhvězdí (pro omezení posunu).</summary>
    public int Height { get; }

    /// <summary>Střed hvězdy v souřadnicích plátna.</summary>
    public Vector2 Center(int techIndex) => _centers[techIndex];

    /// <summary>Klikací čtverec kolem hvězdy (hit test i culling).</summary>
    public Rectangle Bounds(int techIndex)
    {
        var center = _centers[techIndex];
        return new Rectangle((int)center.X - HitSize / 2, (int)center.Y - HitSize / 2, HitSize, HitSize);
    }

    /// <summary>Průměrný úhel už umístěných prerekvizit; bez nich 0 (kořeny).</summary>
    private static float ParentAngle(DefRegistry<TechDef> techs, int index, float[] angles)
    {
        var prereqs = techs[index].PrerequisiteIndices;
        if (prereqs.Count == 0)
        {
            return 0f;
        }

        float sum = 0f;
        foreach (int prereq in prereqs)
        {
            sum += angles[prereq];
        }

        return sum / prereqs.Count;
    }

    private static int ComputeDepth(DefRegistry<TechDef> techs, int index, int[] depth, byte[] state)
    {
        if (state[index] == 2)
        {
            return depth[index];
        }

        if (state[index] == 1)
        {
            return 0; // strážce cyklu — chybná data nesmí zacyklit vykreslení
        }

        state[index] = 1;
        int best = 0;
        foreach (int prereq in techs[index].PrerequisiteIndices)
        {
            best = Math.Max(best, ComputeDepth(techs, prereq, depth, state) + 1);
        }

        depth[index] = best;
        state[index] = 2;
        return best;
    }
}
