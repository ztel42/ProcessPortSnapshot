namespace ProcessPortSnapshot;

public sealed record ProcessInfo(
    int Pid,
    string Name,
    string? Path,
    string? User);

public sealed record ListeningEndpoint(
    string Protocol,
    string LocalAddress,
    int Port,
    int? Pid,
    string? ProcessName,
    string? ProcessPath);

public sealed record Snapshot(
    DateTimeOffset CapturedAt,
    string HostName,
    string Platform,
    IReadOnlyList<ProcessInfo> Processes,
    IReadOnlyList<ListeningEndpoint> Listeners);
