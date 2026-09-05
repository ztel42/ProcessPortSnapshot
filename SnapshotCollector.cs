using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace ProcessPortSnapshot;

public static class SnapshotCollector
{
    public static Snapshot Capture()
    {
        var processes = CaptureProcesses();
        var byPid = processes.GroupBy(p => p.Pid).ToDictionary(g => g.Key, g => g.First());
        var listeners = CaptureListeners(byPid);

        return new Snapshot(
            DateTimeOffset.Now,
            Environment.MachineName,
            RuntimeInformation.OSDescription,
            processes.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Pid).ToList(),
            listeners.OrderBy(l => l.Port).ThenBy(l => l.Protocol, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static List<ProcessInfo> CaptureProcesses()
    {
        var list = new List<ProcessInfo>();
        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                string? path = null;
                try { path = proc.MainModule?.FileName; } catch { }

                if (path is null && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    try
                    {
                        var exe = $"/proc/{proc.Id}/exe";
                        path = new FileInfo(exe).LinkTarget;
                    }
                    catch { }
                }

                list.Add(new ProcessInfo(
                    proc.Id,
                    string.IsNullOrWhiteSpace(proc.ProcessName) ? "(unknown)" : proc.ProcessName,
                    path,
                    null));
            }
            catch
            {
            }
            finally
            {
                proc.Dispose();
            }
        }

        return list;
    }

    private static List<ListeningEndpoint> CaptureListeners(IReadOnlyDictionary<int, ProcessInfo> byPid)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return CaptureLinuxListeners(byPid);
        }

        return CaptureManagedListeners();
    }

    private static List<ListeningEndpoint> CaptureManagedListeners()
    {
        var results = new List<ListeningEndpoint>();
        var props = IPGlobalProperties.GetIPGlobalProperties();

        foreach (var ep in props.GetActiveTcpListeners())
        {
            results.Add(new ListeningEndpoint("TCP", FormatAddress(ep.Address), ep.Port, null, null, null));
        }

        foreach (var ep in props.GetActiveUdpListeners())
        {
            results.Add(new ListeningEndpoint("UDP", FormatAddress(ep.Address), ep.Port, null, null, null));
        }

        return results;
    }

    private static List<ListeningEndpoint> CaptureLinuxListeners(IReadOnlyDictionary<int, ProcessInfo> byPid)
    {
        var inodeToPid = BuildInodeToPidMap();
        var results = new List<ListeningEndpoint>();
        results.AddRange(ParseLinuxProcNet("/proc/net/tcp", "TCP", inodeToPid, byPid, isIpv6: false));
        results.AddRange(ParseLinuxProcNet("/proc/net/tcp6", "TCP", inodeToPid, byPid, isIpv6: true));
        results.AddRange(ParseLinuxProcNet("/proc/net/udp", "UDP", inodeToPid, byPid, isIpv6: false));
        results.AddRange(ParseLinuxProcNet("/proc/net/udp6", "UDP", inodeToPid, byPid, isIpv6: true));
        return results;
    }

    public static Dictionary<long, int> BuildInodeToPidMap(string procRoot = "/proc")
    {
        var map = new Dictionary<long, int>();
        IEnumerable<string> dirs;
        try { dirs = Directory.EnumerateDirectories(procRoot); }
        catch { return map; }

        foreach (var dir in dirs)
        {
            if (!int.TryParse(Path.GetFileName(dir), out var pid))
            {
                continue;
            }

            var fdDir = Path.Combine(dir, "fd");
            if (!Directory.Exists(fdDir))
            {
                continue;
            }

            IEnumerable<string> fds;
            try { fds = Directory.EnumerateFileSystemEntries(fdDir); }
            catch { continue; }

            foreach (var fd in fds)
            {
                string? target = null;
                try
                {
                    target = new FileInfo(fd).LinkTarget;
                }
                catch { }

                if (target is null ||
                    !target.StartsWith("socket:[", StringComparison.Ordinal) ||
                    !target.EndsWith(']'))
                {
                    continue;
                }

                if (long.TryParse(target.AsSpan(8, target.Length - 9), out var inode))
                {
                    map.TryAdd(inode, pid);
                }
            }
        }

        return map;
    }

    public static IEnumerable<ListeningEndpoint> ParseLinuxProcNet(
        string path,
        string protocol,
        IReadOnlyDictionary<long, int> inodeToPid,
        IReadOnlyDictionary<int, ProcessInfo> byPid,
        bool isIpv6)
    {
        if (!File.Exists(path))
        {
            yield break;
        }

        string[] lines;
        try { lines = File.ReadAllLines(path); }
        catch { yield break; }

        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 10)
            {
                continue;
            }

            if (protocol == "TCP" && !parts[3].Equals("0A", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var local = parts[1];
            var colon = local.LastIndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var addrHex = local[..colon];
            var portHex = local[(colon + 1)..];
            if (!int.TryParse(portHex, System.Globalization.NumberStyles.HexNumber, null, out var port))
            {
                continue;
            }

            if (!long.TryParse(parts[9], out var inode))
            {
                continue;
            }

            int? pid = inodeToPid.TryGetValue(inode, out var mapped) ? mapped : null;
            string? name = null;
            string? procPath = null;
            if (pid is int p && byPid.TryGetValue(p, out var info))
            {
                name = info.Name;
                procPath = info.Path;
            }

            yield return new ListeningEndpoint(
                protocol,
                DecodeLinuxAddress(addrHex, isIpv6),
                port,
                pid,
                name,
                procPath);
        }
    }

    public static string DecodeLinuxAddress(string hex, bool isIpv6)
    {
        try
        {
            if (!isIpv6)
            {
                var value = Convert.ToUInt32(hex, 16);
                return new IPAddress(BitConverter.GetBytes(value)).ToString();
            }

            var raw = Convert.FromHexString(hex);
            var reordered = new byte[16];
            for (var i = 0; i < 4; i++)
            {
                reordered[i * 4 + 0] = raw[i * 4 + 3];
                reordered[i * 4 + 1] = raw[i * 4 + 2];
                reordered[i * 4 + 2] = raw[i * 4 + 1];
                reordered[i * 4 + 3] = raw[i * 4 + 0];
            }

            return new IPAddress(reordered).ToString();
        }
        catch
        {
            return hex;
        }
    }

    private static string FormatAddress(IPAddress address)
    {
        if (IPAddress.Any.Equals(address)) return "0.0.0.0";
        if (IPAddress.IPv6Any.Equals(address)) return "::";
        return address.ToString();
    }
}
