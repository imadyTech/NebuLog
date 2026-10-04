# NebuLog

Real-time log streaming for .NET, built on OpenTelemetry and SignalR.

> **v2 is a ground-up rewrite** (branch `v2`). The original 2019–2022 implementation remains
> available on `master` and in the git history.

## Status

Under active development. See the process log and architecture decision records in the
companion `docs` folder.

## Architecture (target)

- **Applications** log through the standard `ILogger` API. The official OpenTelemetry .NET SDK
  exports either over OTLP/HTTP or through `NebuLogExporter` (SignalR, low latency).
- **NebuLog.Server** is an embeddable ASP.NET Core library: OTLP/HTTP receiver, ingestion
  pipeline, SignalR hub and dashboard APIs.
- **Dashboard**: React + TypeScript, served from the same origin as the API.

## Repository layout

| Path | Purpose |
|---|---|
| `src/NebuLog.Contracts` | Wire contracts (netstandard2.1 / net10.0) |
| `src/NebuLog.OpenTelemetry` | OpenTelemetry SDK extensions (exporter, redaction processor) |
| `src/NebuLog.Server` | Embeddable server library |
| `src/NebuLog.Server.Host` | Stand-alone host |
| `web/dashboard` | React dashboard |
| `tests/` | Automated tests |

## Build

```bash
cd web/dashboard && npm ci && npm run build && cd ../..
dotnet build NebuLog.slnx
dotnet test NebuLog.slnx
```

Requires the .NET 10 SDK and Node.js 22+.

## Licence

Apache-2.0
