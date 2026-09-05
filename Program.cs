using ProcessPortSnapshot;

var format = "table";
string? outPath = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--json":
            format = "json";
            break;
        case "--csv":
            format = "csv";
            break;
        case "--out":
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine("Missing path after --out");
                return 1;
            }
            outPath = args[++i];
            break;
        case "--help":
        case "-h":
            PrintHelp();
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            PrintHelp();
            return 1;
    }
}

var snapshot = SnapshotCollector.Capture();
var text = format switch
{
    "json" => Exporters.ToJson(snapshot),
    "csv" => Exporters.ToCsv(snapshot),
    _ => Exporters.ToConsoleTable(snapshot),
};

if (outPath is not null)
{
    File.WriteAllText(outPath, text);
    Console.WriteLine($"Wrote {snapshot.Listeners.Count} listeners and {snapshot.Processes.Count} processes to {outPath}");
}
else
{
    Console.Write(text);
}

return 0;

static void PrintHelp()
{
    Console.WriteLine("""
ProcessPortSnapshot — one-shot process + listening-ports inventory

Usage:
  dotnet run --project ProcessPortSnapshot.csproj -- [--json|--csv] [--out path]

Options:
  --json     Emit JSON
  --csv      Emit CSV (processes + listeners)
  --out PATH Write to file instead of stdout
  -h, --help Show help
""");
}
