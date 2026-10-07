# Changelog

All notable changes to this project are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses
[semantic versioning](https://semver.org/spec/v2.0.0.html).

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

[2.1.0]: https://github.com/imadyTech/NebuLog/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/imadyTech/NebuLog/releases/tag/v2.0.0
