using System.Buffers.Text;
using System.IO.Compression;
using System.Text;

namespace CivDle.Core.Sim;

/// <summary>
/// Šablona jako text, který jde poslat kamarádovi.
///
/// <para><b>Proč kód a ne soubor:</b> „pošli mi ten svůj blok" má být zpráva
/// v chatu, ne hledání složky s profilem. Text se dá vložit kamkoli a nikde
/// po cestě se nerozbije.</para>
///
/// <para><b>Formát:</b> <c>CIVD1:</c> + Base64 ze zkomprimovaného zápisu.
/// Hlavička je tam kvůli verzi — až se formát někdy změní, starší hra kód
/// pozná a řekne to slušně místo toho, aby položila nesmysl. Komprese proto,
/// že blok o padesáti budovách je jinak zeď textu, kterou chat zalomí.</para>
///
/// <para><b>Uvnitř jsou ID, ne indexy</b> — stejně jako v šabloně samotné.
/// Kód od kamaráda s jinými mody se tak dá aspoň částečně použít: co
/// nepozná, přeskočí.</para>
///
/// <para>Vrstva: jádro. Neví o schránce ani o obrazovce — jen převádí
/// šablonu na text a zpátky.</para>
/// </summary>
public static class TemplateCode
{
    /// <summary>Hlavička s verzí formátu. Bez ní by se cizí text tvářil jako šablona.</summary>
    public const string Prefix = "CIVD1:";

    /// <summary>Strop délky kódu, který se ještě zkusí načíst (ochrana proti nesmyslu ve schránce).</summary>
    public const int MaxCodeLength = 64 * 1024;

    /// <summary>Udělá ze šablony text ke sdílení.</summary>
    public static string Write(BuildTemplate template)
    {
        using var raw = new MemoryStream();
        using (var gzip = new GZipStream(raw, CompressionLevel.Optimal, leaveOpen: true))
        using (var writer = new BinaryWriter(gzip, Encoding.UTF8))
        {
            writer.Write(template.Name ?? string.Empty);

            writer.Write(template.Buildings.Count);
            foreach (var part in template.Buildings)
            {
                writer.Write(part.BuildingId);
                writer.Write(part.Dx);
                writer.Write(part.Dy);
            }

            writer.Write(template.Roads.Count);
            foreach (var road in template.Roads)
            {
                writer.Write(road.Dx);
                writer.Write(road.Dy);
            }
        }

        return Prefix + Convert.ToBase64String(raw.ToArray());
    }

    /// <summary>
    /// Přečte šablonu z textu.
    ///
    /// <para>Poškozený kód skončí <c>false</c>, ne výjimkou: do schránky se
    /// dostane leccos a hra kvůli tomu nesmí spadnout. Volající ukáže hlášku
    /// „tohle není kód šablony" a jede dál.</para>
    /// </summary>
    public static bool TryRead(string? code, out BuildTemplate template)
    {
        template = BuildTemplate.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        string trimmed = code.Trim();
        if (trimmed.Length > MaxCodeLength || !trimmed.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            byte[] packed = Convert.FromBase64String(trimmed[Prefix.Length..]);

            using var raw = new MemoryStream(packed);
            using var gzip = new GZipStream(raw, CompressionMode.Decompress);
            using var reader = new BinaryReader(gzip, Encoding.UTF8);

            string name = reader.ReadString();

            int buildingCount = reader.ReadInt32();
            if (buildingCount is < 0 or > 4096)
            {
                return false;
            }

            var buildings = new List<TemplatePart>(Math.Min(buildingCount, 256));
            for (int i = 0; i < buildingCount; i++)
            {
                buildings.Add(new TemplatePart(reader.ReadString(), reader.ReadInt32(), reader.ReadInt32()));
            }

            int roadCount = reader.ReadInt32();
            if (roadCount is < 0 or > 8192)
            {
                return false;
            }

            var roads = new List<(int Dx, int Dy)>(Math.Min(roadCount, 512));
            for (int i = 0; i < roadCount; i++)
            {
                roads.Add((reader.ReadInt32(), reader.ReadInt32()));
            }

            template = new BuildTemplate(name, buildings, roads);
            return true;
        }
        catch (Exception error) when (error is FormatException or InvalidDataException
                                          or EndOfStreamException or IOException
                                          or ArgumentException or OutOfMemoryException)
        {
            return false;
        }
    }
}
