import { describe, expect, it } from 'vitest'
import type { NebuLogEntry } from '../api/contracts'
import { SeverityNumbers } from '../api/severity'
import { LogStore, defaultCapacity, emptyFilter } from './logStore'

/**
 * Budget for one frame's merge. The display gives ~16.7 ms per frame; the store must take a small
 * slice of that so layout, paint and the virtualiser still fit. Generous enough not to be flaky on
 * a loaded CI machine, tight enough to catch an accidental O(n) pass over the whole buffer.
 */
const FRAME_BUDGET_MS = 8

const severities = [
  SeverityNumbers.trace,
  SeverityNumbers.debug,
  SeverityNumbers.info,
  SeverityNumbers.warn,
  SeverityNumbers.error,
]

function batch(firstId: number, size: number): NebuLogEntry[] {
  const entries = new Array<NebuLogEntry>(size)
  for (let i = 0; i < size; i++) {
    const id = firstId + i
    entries[i] = {
      id,
      timestampUnixMs: 1_700_000_000_000 + id,
      observedUnixMs: 1_700_000_000_000 + id,
      severityNumber: severities[id % severities.length]!,
      severityText: null,
      body: `order ${id} processed by worker ${id % 8}`,
      serviceName: `service-${id % 4}`,
      serviceInstanceId: null,
      scopeName: 'Orders.Checkout',
      traceId: null,
      spanId: null,
      eventName: null,
      attributes: {},
      exception: null,
      source: 'SignalR',
    }
  }

  return entries
}

/** Fills the store to capacity the way the hub would: in frame-sized batches. */
function fill(store: LogStore, total: number, perBatch: number): void {
  for (let id = 1; id <= total; id += perBatch) {
    store.append(batch(id, Math.min(perBatch, total - id + 1)))
  }
}

describe('LogStore performance', () => {
  it('merges a frame of 1,000 entries into a full buffer well inside the frame budget', () => {
    const store = new LogStore(defaultCapacity)
    store.setFilter({ ...emptyFilter, minSeverity: SeverityNumbers.debug })
    fill(store, defaultCapacity, 1000)

    expect(store.size).toBe(defaultCapacity)

    // Measure steady state: the ring is full, so every append also evicts.
    const samples: number[] = []
    let nextId = defaultCapacity + 1
    for (let frame = 0; frame < 60; frame++) {
      const entries = batch(nextId, 1000)
      nextId += 1000

      const started = performance.now()
      store.append(entries)
      samples.push(performance.now() - started)
    }

    samples.sort((a, b) => a - b)
    const median = samples[Math.floor(samples.length / 2)]!
    const worst = samples[samples.length - 1]!

    console.log(
      `append 1,000 into a full ${defaultCapacity.toLocaleString()} buffer: ` +
        `median ${median.toFixed(2)} ms, worst ${worst.toFixed(2)} ms over ${samples.length} frames`,
    )

    expect(median).toBeLessThan(FRAME_BUDGET_MS)
  })

  it('reads rows by index in constant time, which is what the virtualiser needs', () => {
    const store = new LogStore(defaultCapacity)
    fill(store, defaultCapacity, 1000)

    // A viewport is ~40 rows; time a scroll's worth of random reads.
    const started = performance.now()
    let seen = 0
    for (let i = 0; i < 10_000; i++) {
      const index = (i * 7919) % store.filteredCount
      if (store.filteredAt(index) !== undefined) {
        seen++
      }
    }

    const elapsed = performance.now() - started
    console.log(`10,000 random row reads: ${elapsed.toFixed(2)} ms`)

    expect(seen).toBe(10_000)
    expect(elapsed).toBeLessThan(FRAME_BUDGET_MS * 4)
  })

  it('recomputes the whole filter by examining each entry exactly once', () => {
    // The machine-independent half, and the one that runs everywhere. A full recompute is O(n);
    // the failure worth catching is someone making it O(n**2) — a nested scan, a per-entry
    // allocation that triggers a rehash.
    //
    // This counts work instead of timing it. Two earlier versions of this test took the ratio of
    // two wall-clock measurements, and both were wrong in the same way: the denominator is only
    // 1-2 ms on a shared runner, so the ratio amplified the runner's noise rather than cancelling
    // the machine out. It failed six Dependabot PRs that touched nothing in this file (11.40,
    // 9.83 and 8.10 against a threshold of 8), and taking a median of five samples did not help,
    // because the problem was never an occasional spike. A count has no variance: a linear pass
    // examines each entry once on every machine, every run.
    const small = examinationsForFullFilter(25_000)
    const large = examinationsForFullFilter(100_000)

    console.log(`full re-filter examined ${small} entries at 25,000 and ${large} at 100,000`)

    expect(small).toBe(25_000)
    expect(large).toBe(100_000)
  })

  it('recomputes the whole filter fast enough to stay on the main thread', () => {
    // The absolute budget from WO-0006 §2.4: above this a full recompute would have to move to a
    // Web Worker. It is a statement about the user's device, so it is asserted on a developer
    // machine and only reported on a shared CI runner, where the number measures the runner rather
    // than the code — 7 ms here, 75 ms on a GitHub-hosted runner for identical code (WO-0012 §9).
    // The linearity check above is what guards the algorithm on every machine.
    const elapsed = medianFullFilter(defaultCapacity)
    console.log(`full re-filter of ${defaultCapacity.toLocaleString()} entries: ${elapsed.toFixed(2)} ms`)

    // Read through globalThis rather than `process`: this is browser-targeted code whose tsconfig
    // deliberately does not include Node's types, and widening that just to read one variable
    // would let browser modules reach for Node APIs too.
    const onCi = Boolean(
      (globalThis as { process?: { env?: Record<string, string | undefined> } }).process?.env?.CI,
    )
    const budget = onCi ? 500 : 50
    expect(elapsed).toBeLessThan(budget)
  })
})

/**
 * Entries the filter examined during one full recompute over a store of the given size.
 *
 * Deterministic: no clock, no warm-up, no tolerance. This is what the linearity assertion uses.
 */
function examinationsForFullFilter(size: number): number {
  const store = new LogStore(size)
  fill(store, size, 1000)

  const before = store.entriesExamined
  store.setFilter({ ...emptyFilter, minSeverity: SeverityNumbers.warn, search: 'worker 3' })

  if (store.filteredCount === 0) {
    throw new Error('the filter matched nothing, so this measured the wrong thing')
  }

  return store.entriesExamined - before
}

/**
 * Median time of one full re-filter over a store of the given size, in milliseconds.
 *
 * Only used for the absolute budget below, which is a statement about a developer's machine. The
 * complexity claim is made by counting, not by timing.
 */
function medianFullFilter(size: number, runs = 5): number {
  const samples: number[] = []

  // One extra pass, discarded: it pays for JIT compilation and the first allocations.
  for (let run = 0; run <= runs; run++) {
    const store = new LogStore(size)
    fill(store, size, 1000)

    const started = performance.now()
    store.setFilter({ ...emptyFilter, minSeverity: SeverityNumbers.warn, search: 'worker 3' })
    const elapsed = performance.now() - started

    if (store.filteredCount === 0) {
      throw new Error('the filter matched nothing, so this measured the wrong thing')
    }

    if (run > 0) {
      samples.push(elapsed)
    }
  }

  samples.sort((a, b) => a - b)
  return samples[Math.floor(samples.length / 2)]!
}
