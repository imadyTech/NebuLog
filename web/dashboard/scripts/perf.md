# Measuring dashboard performance

WO-0006 §6 asks for a buffer of 100,000 entries with 1,000 more arriving every second, scrolling at
50 fps or better. There are three ways to check that, from cheapest to most faithful.

## 1. Headless, on every test run

`npm test` includes `src/store/logStore.bench.test.ts`, which exercises the real `LogStore` at full
capacity and fails the build if the per-frame merge regresses. It measures the data path only — no
DOM — so it is fast, deterministic and good at catching an accidental full-buffer scan.

```
npm test
```

Reported on 2026-10-05 (Windows 11, .NET host machine, Node 24):

| Measurement | Result |
|---|---|
| Merge 1,000 entries into a full 100,000 buffer | median **0.46 ms**, worst 7.09 ms over 60 frames |
| 10,000 random row reads by index | **1.03 ms** |
| Full re-filter of 100,000 entries | **6.56 ms** |

The last number matters for a design decision: §2.4 says a full recompute above 50 ms must move to a
Web Worker. At 6.56 ms it does not, so the filter stays on the main thread and the dashboard keeps
one less moving part.

## 2. In a browser, with the dev-only harness

`src/features/perf/PerfHarness.tsx` is routed at `/perf` under `npm run dev` only — `App` guards it
with `import.meta.env.DEV`, so it is tree-shaken out of production builds (verify with
`grep -c "Fill to" ../../src/NebuLog.Server.Host/wwwroot/assets/*.js`, which must print `0`).

```
npm run dev
# open http://localhost:5173/perf
# click "Fill to 100,000", then "Stream 1,000/s"
```

The harness drives the real `LogStore` and the real `LogTable`, with a synthetic producer standing
in for the hub. It shows a live frame rate, the worst frame in each one-second window, the buffered
count and — in Chromium — the JS heap.

**Caveat:** a headless or backgrounded browser throttles `requestAnimationFrame`, sometimes to 1 Hz.
If the harness reports `fps 1`, the browser is throttling, not the dashboard. Measure in a focused,
visible window, or use the per-step measurement below, which does not depend on `rAF`.

Measured 2026-10-05 in the in-app browser with 100,000 buffered and 1,000/s streaming, by setting
`scrollTop`, awaiting React's commit and forcing layout, 150 times:

| Measurement | Result |
|---|---|
| Scroll step: scroll → React commit → layout | median **4.8 ms**, p95 6.7 ms, worst 15.4 ms |
| Implied frame rate (median / worst) | **208 fps** / 65 fps |
| Rows in the DOM | **51**, representing 100,000 |
| JS heap | 57 MB |

Even the worst single step stays inside a 16.7 ms frame, so the ≥ 50 fps criterion holds with room
to spare. The 51-rows figure is the point of the exercise: v1 put every row in the DOM, which is why
it stalled past ten thousand.

## 3. Against a real producer

The most faithful check, once WO-0007 provides the demo producer with its burst mode: point it at a
running host, sign in, and watch the live view rather than the harness. Record with the browser's
Performance panel — look for long tasks over 50 ms and for dropped frames during a scroll.

What to watch for, in order of usefulness:

1. **Long tasks.** A merge that scans the whole buffer shows up as a periodic ~100 ms task.
2. **DOM node count.** It must stay flat as the buffer fills. Growth means virtualisation broke.
3. **Heap growth over time.** The ring is fixed-size, so a steadily climbing heap means something is
   retaining entries — a subscription that was never removed, or a filter result that never drops
   evicted indexes.
