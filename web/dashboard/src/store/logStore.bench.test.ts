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

  it('recomputes the whole filter over a full buffer fast enough to stay on the main thread', () => {
    const store = new LogStore(defaultCapacity)
    fill(store, defaultCapacity, 1000)

    const started = performance.now()
    store.setFilter({ ...emptyFilter, minSeverity: SeverityNumbers.warn, search: 'worker 3' })
    const elapsed = performance.now() - started

    console.log(`full re-filter of ${defaultCapacity.toLocaleString()} entries: ${elapsed.toFixed(2)} ms`)

    // WO-0006 §2.4: a full recompute above 50 ms would have to move to a Web Worker.
    expect(elapsed).toBeLessThan(50)
    expect(store.filteredCount).toBeGreaterThan(0)
  })
})
