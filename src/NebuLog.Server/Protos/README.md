# Vendored protocol buffer definitions

These files are copied verbatim from upstream so that NebuLog can accept OTLP/HTTP without taking a
dependency on the OpenTelemetry collector packages. Their original Apache-2.0 licence headers are
kept intact; do not edit them. To update, re-copy from the same paths at a newer tag and rerun the
tests.

| Path | Source | Version |
|---|---|---|
| `opentelemetry/proto/common/v1/common.proto` | [open-telemetry/opentelemetry-proto](https://github.com/open-telemetry/opentelemetry-proto) | `v1.11.1` (2026-09-29) |
| `opentelemetry/proto/resource/v1/resource.proto` | same | `v1.11.1` |
| `opentelemetry/proto/logs/v1/logs.proto` | same | `v1.11.1` |
| `opentelemetry/proto/collector/logs/v1/logs_service.proto` | same | `v1.11.1` |
| `google/rpc/status.proto` | [googleapis/googleapis](https://github.com/googleapis/googleapis) | `master` as of 2026-10-05 |

Only message types are generated (`GrpcServices="None"`); NebuLog serves OTLP over plain HTTP and
does not host a gRPC service. The generated C# keeps the upstream `csharp_namespace`
(`OpenTelemetry.Proto.*`), which does not collide with the `OpenTelemetry` SDK packages because this
project does not reference them.
