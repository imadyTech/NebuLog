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

## Identity and API keys

Two kinds of caller are distinguished. **People** sign in with email and password and get a session
cookie; **programs** send an API key in `X-Api-Key`. A single policy scheme picks between them per
request, so both share one pipeline.

Roles are `Viewer`, `Operator`, `Admin` (people) and `Producer` (API keys only). There is no
registration endpoint: accounts are seeded from configuration, and keys are issued by an
administrator.

| Setting | Purpose |
|---|---|
| `ConnectionStrings__Identity` | SQLite database. Production: `Data Source=/data/nebulog.db` |
| `NebuLog__Admin__Email` / `NebuLog__Admin__Password` | Seeded once if the account is absent |
| `NebuLog__Demo__Enabled` | Offers a read-only `demo@nebulog.local` sign-in (default `true`) |
| `NebuLog__DemoProducer__ApiKey` | Seeds a producer key; only its hash is stored |
| `NebuLog__DataProtection__KeysPath` | Key ring directory. Production: `/data/keys` |

Only hashes are stored: an API key's clear text is shown once, at creation, and is not recoverable.

### Trade-off: migrating at startup

The application applies EF Core migrations and seeds its roles and accounts while starting, from a
hosted service. That is deliberate for this deployment — one container, one SQLite file — where a
separate migration step would add operational work for no benefit. It would be the wrong choice for
a multi-instance deployment, where two instances could race to migrate the same database; that
would call for a separate migration job and a startup that only verifies the schema.

All seeding is idempotent and never modifies an existing account, so restarting the container
cannot reset a password that was changed afterwards.

### Writable paths

The container runs with a read-only root filesystem and one writable volume at `/data`. Everything
written at runtime — the SQLite database and the Data Protection key ring — is placed under the
configured paths, which is covered by `WritablePathsTests`.

## Licence

Apache-2.0
