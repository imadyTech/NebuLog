# Changelog

All notable changes to this project are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[semantic versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.3] — 2026-10-29

Dependency maintenance. No functional change, and no vulnerability was reachable in 2.1.2 — all
three supply-chain checks were clean before and after.

### Changed

- All eleven ASP.NET Core packages are now on **10.0.14**. Dependabot had raised four of them; the
  remaining seven were brought up in one commit rather than waiting for separate pull requests.
  Mixed patch versions let NuGet resolve transitive dependencies in combinations nobody chose, and
  the October SignalR advisories landed on `SignalR.Protocols.MessagePack` — which was one of the
  seven lagging behind.
- `react-router` 8.5.0, `@tanstack/react-virtual` 3.14.22, `vite` 8.3.5 (console and browser
  sample), `oxlint` 1.89.0, `Grpc.Tools` 2.85.0, and the usual dev-tooling bumps.

### Not changed, deliberately

- `Microsoft.Extensions.DependencyModel` still resolves to 10.0.10 as a transitive dependency of the
  test host. It has no known advisory and never reaches a production image. Pinning it explicitly
  would add a permanent maintenance entry for the sake of a tidy version list; the scanner will
  raise it if it ever matters.

## [2.1.2] — 2026-10-23

Dependency maintenance. No functional change, and nothing here fixes a vulnerability that was
reachable in 2.1.1 — see the note below.

### Changed

- OpenTelemetry 1.19.1 → **1.20.1** across the SDK, the OTLP exporter, the hosting extensions and
  both instrumentation packages. This is the one upgrade worth watching: NebuLog builds on SDK
  internals (`BaseExporter<LogRecord>`, `BaseProcessor<LogRecord>`, `LoggerProviderBuilder`), so a
  minor version is not automatically safe. All 251 tests pass, including the compatibility test that
  drives the stock OTLP exporter end to end.
- React and React DOM 19.2.8 → 19.3.1 in the console and the shop; `@tanstack/react-virtual`
  3.14.15 → 3.14.21; Playwright 1.64.1; `Scalar.AspNetCore` 2.18.0 → 2.18.2; plus the usual
  dev-tooling bumps.
- Dependabot now covers all four npm manifests, so `web/shop`, `tests/smoke` and `samples/browser`
  received their first updates since they were added.

### Note on the October 2026 ASP.NET Core advisories

A batch of advisories was published on 2026-10-13, including two rated Critical in Kestrel
([CVE-2026-88092](https://github.com/dotnet/announcements/issues/472), request smuggling;
[CVE-2026-70188](https://github.com/dotnet/announcements/issues/462), security feature bypass). All
are fixed in 10.0.13.

Kestrel ships in the shared framework, not as a NuGet package, so `dotnet list package
--vulnerable` does not report it — checking the runtime inside the image is what answers the
question. The 2.1.1 image was verified to carry `Microsoft.AspNetCore.App 10.0.13`, so it already
contained these fixes; 2.1.2 is built on the same or newer runtime.

## [2.1.1] — 2026-10-14

### Security

- Updated the ASP.NET Core packages from 10.0.12 to 10.0.13, which carries the fixes for two
  SignalR advisories published after 2.1.0:
  - [GHSA-86xp-94pg-8r6r](https://github.com/advisories/GHSA-86xp-94pg-8r6r) (moderate, CVSS 5.3) —
    the MessagePack protocol can consume excessive memory deserializing deeply nested arrays from a
    client. **This one is reachable here**: NebuLog uses the MessagePack protocol, and although the
    hub requires an authenticated principal, the read-only demo account is open to anyone.
  - [GHSA-334f-vvp4-7g6p](https://github.com/advisories/GHSA-334f-vvp4-7g6p) (high, CVSS 7.5) — a
    stateful-reconnect client can block broadcasts indefinitely. NebuLog does not enable stateful
    reconnect, so this path is not reachable; the package was updated because it is in the
    dependency graph, not because the server was exposed.

### Changed

- Dependabot updates merged before the above: `Scalar.AspNetCore` 2.17.13 → 2.17.14,
  `Microsoft.Testing.Extensions.CodeCoverage` 18.11.2 → 18.12.0.

## [2.1.0] — 2026-10-07

A public face for the project: a landing page, guided demos, and a small application to generate
real traffic for them.

### Added

- **Landing page** at `/`, open to anonymous visitors, with live ingest figures from a new public
  endpoint and an architecture overview. Figures quoted on the page come from the v2.0.0 evaluation
  and are kept in one file with their sources noted.
- **Guided demos** at `/demo`: eight scenarios, each opening the demo shop in one window and a
  pre-filtered console in the other.
- **NebuShop**, a deliberately small demo application in two services:
  - `nebulog-shop-orders` serves the same five operations through **both** a Minimal API and an MVC
    pipeline, so the console can show what each one logs for an identical request.
  - `nebulog-shop-payments` is called by the orders service over HTTP, which is what gives a single
    checkout one TraceId across two services.
  - Both run with no published ports; the server reverse-proxies them at `/apps/shop` for signed-in
    viewers only, with its own rate limit.
- **Console additions**: the active scenario's hint bar, removable filter chips with an
  "N of M in buffer" count, filtering by TraceId, a panel summarising the visitor's own requests,
  and two-way synchronisation between the filters and the URL, so a view can be shared as a link.
- **`GET /api/public/summary`** and **`GET /api/public/site-config`**: anonymous, output-cached for
  five seconds, separately rate limited. The summary returns totals, a 60-second rate curve, a
  service *count* and an uptime — never log content, and never service names.
- Design tokens shared by the console, the shop and the site pages, with self-hosted fonts so the
  content security policy can keep `font-src 'self'`.

### Changed

- Signing in now lands on `/demo` rather than the console; the console moved to `/dashboard`.
- The login page leads with the read-only demo visitor and offers staff sign-in second.
- `returnUrl` is honoured only for same-origin paths, so the sign-in page cannot be used as an open
  redirect.
- The contrast check in CI now covers the shared site tokens as well as the console theme.

### Fixed

- The shop's assets were requested relative to the document, which resolved to the wrong directory
  when the page was opened without a trailing slash and left it blank. They are now referenced by
  the full proxied path, with a test that reads the built HTML.
- The hint bar's buttons used a border that measured 2.70:1 against the bar's tint, under the 3:1
  WCAG 1.4.11 asks of a control boundary.

## [2.0.0] — 2026-10-05

A complete rewrite on .NET 10 around OpenTelemetry. v1's code is not carried forward; it is archived
at the [`v1-final`](https://github.com/imadyTech/NebuLog/tree/v1-final) tag.

### Added

- **OpenTelemetry-native ingest.** Applications log through the standard `ILogger` and the official
  OTel SDK exports. Two receivers — `NebuLogExporter` over SignalR with MessagePack, and OTLP/HTTP
  at `POST /v1/logs` in protobuf or JSON, gzip optional — converge on one `ILogIngestor`.
- **Authentication and authorisation from the start.** Cookie sessions for people, hashed API keys
  for programs, selected per request by a policy scheme. Roles: Viewer, Operator, Admin, Producer.
  No registration endpoint; accounts are seeded from configuration.
- **A React console** holding entries in a fixed-capacity ring outside React state, committing
  batches per animation frame, maintaining filter results incrementally and virtualising the table:
  about 51 rows in the DOM for a 100,000-entry buffer.
- **`RedactionProcessor`**, masking sensitive attribute values before anything leaves the process.
- **Production hygiene**: read-only root filesystem, non-root user, a single writable volume,
  immutable image tags, ProblemDetails errors, health probes, rate limiting and forwarded-header
  handling that trusts only configured networks.
- 280 automated tests, including a compatibility test that drives the stock OTLP exporter end to end
  with nothing but a URL.

### Changed

- Target framework .NET 6 → .NET 10; central package management; `.slnx` solution format.
- Tests run on Microsoft.Testing.Platform, as .NET 10 no longer supports VSTest.

### Fixed

- **Cross-site scripting.** v1 inserted log bodies into the page as HTML, so any producer could run
  script in the dashboard. All content is now rendered as text, enforced by two CI checks.
- **The hub was open to anyone.** It now requires an authenticated principal.
- **The project would not build from a clean clone.**
- **57 dependency advisories** (8 critical, 12 high, 37 moderate) are gone with the rewrite.

## 1.x — 2018 to 2022

Developed as *MyLogger* from December 2018, renamed **NebuLog** in August 2019, and released as the
`imady.NebuLog` NuGet packages. Logs travelled over SignalR to an ASP.NET Core MVC dashboard, with
collectors for MVC, WPF, Unity and Blazor WebAssembly hosts, custom live statistics (2020) and Unity
3D monitoring (2022). The repository was included in the GitHub Arctic Code Vault in 2020. The final
state is archived at [`v1-final`](https://github.com/imadyTech/NebuLog/tree/v1-final); the author's
contemporary write-ups are linked from the README.

[2.1.3]: https://github.com/imadyTech/NebuLog/compare/v2.1.2...v2.1.3
[2.1.2]: https://github.com/imadyTech/NebuLog/compare/v2.1.1...v2.1.2
[2.1.1]: https://github.com/imadyTech/NebuLog/compare/v2.1.0...v2.1.1
[2.1.0]: https://github.com/imadyTech/NebuLog/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/imadyTech/NebuLog/releases/tag/v2.0.0
