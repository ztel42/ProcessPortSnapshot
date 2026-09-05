using ProcessPortSnapshot;
using Xunit;

public class CollectorTests
{
    [Fact]
    public void DecodeLinuxAddress_IPv4_Loopback()
    {
        // 0100007F little-endian = 127.0.0.1
        Assert.Equal("127.0.0.1", SnapshotCollector.DecodeLinuxAddress("0100007F", isIpv6: false));
    }

    [Fact]
    public void DecodeLinuxAddress_IPv4_Any()
    {
        Assert.Equal("0.0.0.0", SnapshotCollector.DecodeLinuxAddress("00000000", isIpv6: false));
    }

    [Fact]
    public void ParseLinuxProcNet_FindsListeningTcp()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var tcp = Path.Combine(dir, "tcp");
            File.WriteAllText(tcp, """
  sl  local_address rem_address   st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode
   0: 0100007F:1F90 00000000:0000 0A 00000000:00000000 00:00000000 00000000     0        0 12345 1 0000000000000000 100 0 0 10 0
   1: 0100007F:1F91 00000000:0000 01 00000000:00000000 00:00000000 00000000     0        0 12346 1 0000000000000000 100 0 0 10 0
""");

            var inodeToPid = new Dictionary<long, int> { [12345] = 42 };
            var byPid = new Dictionary<int, ProcessInfo>
            {
                [42] = new ProcessInfo(42, "demo", "/usr/bin/demo", null),
            };

            var listeners = SnapshotCollector.ParseLinuxProcNet(tcp, "TCP", inodeToPid, byPid, isIpv6: false).ToList();
            Assert.Single(listeners);
            Assert.Equal(8080, listeners[0].Port);
            Assert.Equal("127.0.0.1", listeners[0].LocalAddress);
            Assert.Equal(42, listeners[0].Pid);
            Assert.Equal("demo", listeners[0].ProcessName);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Exporters_JsonAndCsv_ContainCounts()
    {
        var snap = new Snapshot(
            DateTimeOffset.Parse("2026-09-04T12:00:00Z"),
            "host",
            "test-os",
            [new ProcessInfo(1, "init", "/sbin/init", null)],
            [new ListeningEndpoint("TCP", "0.0.0.0", 22, 1, "init", "/sbin/init")]);

        var json = Exporters.ToJson(snap);
        Assert.Contains("init", json);
        Assert.Contains("22", json);

        var csv = Exporters.ToCsv(snap);
        Assert.Contains("process,", csv);
        Assert.Contains("listener,", csv);
        Assert.Contains("22", csv);
    }

    [Fact]
    public void Capture_ReturnsProcessesAndDoesNotThrow()
    {
        var snap = SnapshotCollector.Capture();
        Assert.True(snap.Processes.Count > 0);
        Assert.False(string.IsNullOrWhiteSpace(snap.HostName));
    }
}
