# ProcessPortSnapshot

One-shot desktop / console inventory of running processes and listening TCP/UDP ports.

Useful for quick host hygiene checks and IR triage: what is listening, and which process owns it.

## Features

- Lists processes (PID, name, path when readable)
- Lists listening endpoints (protocol, address, port, owning PID/name on Linux)
- Console table, JSON, or CSV export
- Works on Linux and Windows (.NET 8)

## Requirements

- .NET 8 SDK

## Build and test

```bash
dotnet test ProcessPortSnapshot.Tests/ProcessPortSnapshot.Tests.csproj
```

## Run

```bash
dotnet run --project ProcessPortSnapshot.csproj
dotnet run --project ProcessPortSnapshot.csproj -- --json --out snapshot.json
dotnet run --project ProcessPortSnapshot.csproj -- --csv --out snapshot.csv
```

On Linux, listener rows include PID/process via `/proc`. On Windows, listeners come from the managed TCP/UDP tables (PID enrichment is best-effort / may be blank without elevated APIs).

## Disclaimer

For systems you own or are authorized to inspect. Not a substitute for EDR.
