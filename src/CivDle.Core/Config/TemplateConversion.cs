using CivDle.Core.Sim;

namespace CivDle.Core.Config;

/// <summary>
/// Převod šablon mezi tvarem pro profil (JSON, veřejné settery) a tvarem pro
/// simulaci (neměnný record).
///
/// <para>Proč dva tvary: profil musí být serializovatelný a tolerantní
/// (rozbitý soubor nesmí shodit hru), simulace chce neměnná data. Sloučit to
/// do jednoho typu by znamenalo mít v jádře třídu se settery, kterou může
/// kdokoli po cestě přepsat.</para>
/// </summary>
public static class TemplateConversion
{
    /// <summary>Z profilu do simulace.</summary>
    public static BuildTemplate ToTemplate(this SavedTemplate saved)
    {
        var buildings = new List<TemplatePart>(saved.Buildings.Count);
        foreach (var part in saved.Buildings)
        {
            if (!string.IsNullOrWhiteSpace(part.Building))
            {
                buildings.Add(new TemplatePart(part.Building, part.X, part.Y));
            }
        }

        var roads = new List<(int Dx, int Dy)>(saved.Roads.Count);
        foreach (var tile in saved.Roads)
        {
            roads.Add((tile.X, tile.Y));
        }

        return new BuildTemplate(saved.Name ?? string.Empty, buildings, roads);
    }

    /// <summary>Ze simulace do profilu.</summary>
    public static SavedTemplate ToSaved(this BuildTemplate template)
    {
        var saved = new SavedTemplate { Name = template.Name };
        foreach (var part in template.Buildings)
        {
            saved.Buildings.Add(new SavedTemplatePart { Building = part.BuildingId, X = part.Dx, Y = part.Dy });
        }

        foreach (var (dx, dy) in template.Roads)
        {
            saved.Roads.Add(new SavedTemplateTile { X = dx, Y = dy });
        }

        return saved;
    }
}
