using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ProcessPortSnapshot;

public static class Exporters
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static string ToJson(Snapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, JsonOptions);

    public static string ToCsv(Snapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("section,protocol,local_address,port,pid,process_name,process_path,host,captured_at,platform");

        foreach (var p in snapshot.Processes)
        {
            sb.Append("process,")
              .Append(',')
              .Append(',')
              .Append(',')
              .Append(p.Pid).Append(',')
              .Append(Csv(p.Name)).Append(',')
              .Append(Csv(p.Path)).Append(',')
              .Append(Csv(snapshot.HostName)).Append(',')
              .Append(Csv(snapshot.CapturedAt.ToString("O", CultureInfo.InvariantCulture))).Append(',')
              .Append(Csv(snapshot.Platform))
              .AppendLine();
        }

        foreach (var l in snapshot.Listeners)
        {
            sb.Append("listener,")
              .Append(Csv(l.Protocol)).Append(',')
              .Append(Csv(l.LocalAddress)).Append(',')
              .Append(l.Port).Append(',')
              .Append(l.Pid?.ToString(CultureInfo.InvariantCulture) ?? "").Append(',')
              .Append(Csv(l.ProcessName)).Append(',')
              .Append(Csv(l.ProcessPath)).Append(',')
              .Append(Csv(snapshot.HostName)).Append(',')
              .Append(Csv(snapshot.CapturedAt.ToString("O", CultureInfo.InvariantCulture))).Append(',')
              .Append(Csv(snapshot.Platform))
              .AppendLine();
        }

        return sb.ToString();
    }

    public static string ToConsoleTable(Snapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Process + listening-ports snapshot");
        sb.AppendLine($"Host: {snapshot.HostName}");
        sb.AppendLine($"When: {snapshot.CapturedAt:u}");
        sb.AppendLine($"OS:   {snapshot.Platform}");
        sb.AppendLine();
        sb.AppendLine($"Processes: {snapshot.Processes.Count}");
        sb.AppendLine($"Listeners: {snapshot.Listeners.Count}");
        sb.AppendLine();
        sb.AppendLine("PROTOCOL  ADDRESS                 PORT   PID     PROCESS");
        sb.AppendLine("--------  ----------------------  -----  ------  ----------------");

        foreach (var l in snapshot.Listeners)
        {
            var addr = l.LocalAddress.Length > 22 ? l.LocalAddress[..19] + "..." : l.LocalAddress;
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,-8}  {1,-22}  {2,5}  {3,-6}  {4}",
                l.Protocol,
                addr,
                l.Port,
                l.Pid?.ToString(CultureInfo.InvariantCulture) ?? "-",
                l.ProcessName ?? "-"));
        }

        return sb.ToString();
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
