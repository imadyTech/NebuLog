# NebuLog dashboard

React + TypeScript + Vite. `npm run dev` proxies `/api`, `/health` and `/hubs` to the
backend (`NEBULOG_BACKEND`, default `http://localhost:5080`). `npm run build` writes the
bundle into `src/NebuLog.Server.Host/wwwroot`, which the host serves on the same origin.

## Scripts

| Command | What it does |
|---|---|
| `npm run dev` | Dev server on 5173, proxying to the backend. Also routes `/perf` (see below). |
| `npm run build` | Type-checks and emits the bundle into the host's `wwwroot`. |
| `npm test` | Vitest: unit, component and performance tests. |
| `npm run lint` | oxlint, plus the raw-HTML and colour-contrast checks below. |

## Layout

```
src/
  api/        fetch wrapper (same-origin credentials + CSRF header), contracts, auth context
  hub/        the single SignalR connection, reconnect and gap-filling
  store/      LogStore: ring buffer with an incrementally maintained filter
  features/   login, live, summary, clients, stats, commands, keys, perf
  components/ shared hooks
```

## Why it stays fast

The log buffer is a plain array of plain objects, not React state. React subscribes only to a
version number, so a thousand entries arriving in one frame cost one re-render rather than a
thousand reconciliations. Arrivals are merged once per animation frame, the filter result is
extended incrementally rather than recomputed, and the table virtualises to roughly fifty rows in
the DOM regardless of buffer size. Measurements and how to reproduce them are in
[`scripts/perf.md`](scripts/perf.md).

## Two checks that run with the lint

- **`scripts/check-no-raw-html.mjs`** fails the build on `dangerouslySetInnerHTML`, `innerHTML`,
  `outerHTML` or `insertAdjacentHTML` anywhere in `src/`. Log bodies and attributes are whatever a
  producer sent; v1 injected them as HTML, which let any producer script the dashboard. Everything
  here renders as text and React escapes it.
- **`scripts/check-contrast.mjs`** parses the theme tokens out of `src/index.css` and verifies every
  text pair at 4.5:1 and every interactive border at 3:1, in both light and dark. It reads the real
  stylesheet, so it cannot drift from it.
