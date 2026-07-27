using System.Globalization;
using System.Text;
using CivDle.Core.Content;

namespace CivDle.Balance;

/// <summary>
/// Vstupní bod nástroje. Spustí jeden nebo víc běhů a vypíše křivky — buď
/// čitelnou tabulku do konzole, nebo CSV k dalšímu zpracování.
///
/// <para>Použití: <c>civdle-balance [--minutes 60] [--seed 1] [--runs 3]
/// [--csv soubor.csv] [--data cesta]</c></para>
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return 0;
        }

        try
        {
            string dataPath = Argument(args, "--data") ?? Path.Combine(AppContext.BaseDirectory, "data");
            var content = new ContentLoader().LoadFrom(dataPath);

            if (args.Contains("--stress"))
            {
                var sizes = new[] { 100, 1_000, 10_000, 50_000, 250_000 };
                var stress = new StressRun(content);
                PrintStress("nečinné město (reálná populace)", stress.Run(sizes, fullyStaffed: false));
                PrintStress("plně obsazené město (horní odhad)", stress.Run(sizes, fullyStaffed: true));
                return 0;
            }

            double minutes = Number(args, "--minutes") ?? 60;
            long seed = (long)(Number(args, "--seed") ?? 12345);
            int runs = (int)(Number(args, "--runs") ?? 1);
            string? csvPath = Argument(args, "--csv");

            var results = new List<(long Seed, BalanceResult Result)>();
            for (int i = 0; i < runs; i++)
            {
                var options = new BalanceOptions(
                    Minutes: minutes,
                    Seed: seed + i,
                    SampleSeconds: Number(args, "--sample-seconds") ?? 60);
                results.Add((options.Seed, new BalanceRun(content, options).Run()));
            }

            if (csvPath is not null)
            {
                WriteCsv(csvPath, content, results);
                Console.WriteLine($"CSV zapsáno: {csvPath}");
            }

            PrintSummary(content, results);
            return 0;
        }
        catch (ContentLoadException ex)
        {
            Console.Error.WriteLine($"Chyba v datech: {ex.Message}");
            return 1;
        }
    }

    /// <summary>Tabulka zátěžového měření: doba tiku a kolik z rozpočtu 10 Hz sežere.</summary>
    private static void PrintStress(string title, IReadOnlyList<StressSample> samples)
    {
        Console.WriteLine();
        Console.WriteLine($"=== zátěž simulace: {title} ===");
        Console.WriteLine($"{"budov",9} {"obyvatel",9} {"µs/tik",10} {"rozpočet",10}");
        foreach (var sample in samples)
        {
            Console.WriteLine(
                $"{sample.Buildings,9} {sample.Population,9:0} {sample.MicrosecondsPerTick,10:0.0} {sample.RealtimeBudgetPercent,9:0.00}%");
        }

        Console.WriteLine();
        Console.WriteLine("Rozpočet = podíl reálného času na simulaci (10 Hz → 100 ms na tik).");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            civdle-balance — odsimuluje hru bez okna a vypíše křivky balancu.

              --minutes N   kolik herních minut odsimulovat (výchozí 60)
              --seed N      seed prvního běhu (výchozí 12345)
              --runs N      kolik běhů se sousedními seedy (výchozí 1)
              --sample-seconds N  jak často odečítat stav (výchozí 60)
              --csv SOUBOR  zapsat všechny odečty do CSV
              --data CESTA  složka s herními daty (výchozí data/ vedle binárky)

            Smysl: balanc se dá MĚŘIT a porovnat před změnou a po ní, místo aby se
            odhadoval. Simulace je deterministická — stejný seed dá stejný výsledek.
            """);
    }

    private static void PrintSummary(GameContent content, List<(long Seed, BalanceResult Result)> results)
    {
        foreach (var (seed, result) in results)
        {
            Console.WriteLine();
            Console.WriteLine($"=== seed {seed} ===");
            Console.WriteLine($"{"min",6} {"obyvatel",10} {"budov",7} {"spokoj.",8} {"→Vzestup",9}  suroviny");

            foreach (var sample in result.Samples)
            {
                Console.WriteLine(
                    $"{sample.Minutes,6:0.0} {sample.Population,10:0.0} {sample.Buildings,7} " +
                    $"{sample.Happiness,8:0.00} {sample.AscensionProgress,9:0%}  {TopResources(content, sample)}");
            }

            Console.WriteLine(result.MinutesToFirstAscension is { } ascend
                ? $"První Vzestup dostupný v {ascend:0.0} min."
                : "První Vzestup nedosažen.");

            if (result.StalledAtMinutes is { } stalled)
            {
                Console.WriteLine($"POZOR: růst se zastavil v {stalled:0.0} min a už se nerozjel.");
            }
        }
    }

    /// <summary>Tři nejzásobenější suroviny — celý výčet by tabulku rozbil.</summary>
    private static string TopResources(GameContent content, BalanceSample sample)
    {
        return string.Join("  ", sample.Resources
            .Select((amount, index) => (Name: content.Resources[index].Id, Amount: amount))
            .Where(r => r.Amount > 0)
            .OrderByDescending(r => r.Amount)
            .Take(3)
            .Select(r => $"{r.Name} {r.Amount:0}"));
    }

    private static void WriteCsv(string path, GameContent content, List<(long Seed, BalanceResult Result)> results)
    {
        var csv = new StringBuilder();
        csv.Append("seed,minutes,population,buildings,happiness,ascension_progress");
        for (int i = 0; i < content.Resources.Count; i++)
        {
            csv.Append(',').Append(content.Resources[i].Id);
        }

        csv.AppendLine();

        foreach (var (seed, result) in results)
        {
            foreach (var sample in result.Samples)
            {
                csv.Append(seed).Append(',')
                   .Append(Format(sample.Minutes)).Append(',')
                   .Append(Format(sample.Population)).Append(',')
                   .Append(sample.Buildings).Append(',')
                   .Append(Format(sample.Happiness)).Append(',')
                   .Append(Format(sample.AscensionProgress));

                foreach (double amount in sample.Resources)
                {
                    csv.Append(',').Append(Format(amount));
                }

                csv.AppendLine();
            }
        }

        File.WriteAllText(path, csv.ToString());
    }

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string? Argument(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static double? Number(string[] args, string name) =>
        Argument(args, name) is { } text && double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out double value)
            ? value
            : null;
}
