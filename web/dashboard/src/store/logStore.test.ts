import { describe, expect, it } from 'vitest'
import type { NebuLogEntry } from '../api/contracts'
import { SeverityNumbers } from '../api/severity'
import { LogStore, emptyFilter, matches, type LogFilter } from './logStore'

function entry(id: number, overrides: Partial<NebuLogEntry> = {}): NebuLogEntry {
  return {
    id,
    timestampUnixMs: 1_700_000_000_000 + id,
    observedUnixMs: 1_700_000_000_000 + id,
    severityNumber: SeverityNumbers.info,
    severityText: 'Information',
    body: `entry ${id}`,
    serviceName: 'orders',
    serviceInstanceId: null,
    scopeName: 'Orders.Checkout',
    traceId: null,
    spanId: null,
    eventName: null,
    attributes: {},
    exception: null,
    source: 'SignalR',
    ...overrides,
  }
}

function filter(overrides: Partial<LogFilter> = {}): LogFilter {
  return { ...emptyFilter, ...overrides }
}

/** Reference implementation: filter the whole buffer from scratch. */
function bruteForce(store: LogStore, predicate: LogFilter): NebuLogEntry[] {
  const result: NebuLogEntry[] = []
  for (let i = 0; i < store.size; i++) {
    const candidate = store.at(i)
    if (candidate !== undefined && matches(candidate, predicate)) {
      result.push(candidate)
    }
  }

  return result
}

describe('LogStore', () => {
  it('rejects a non-positive capacity', () => {
    expect(() => new LogStore(0)).toThrow(RangeError)
  })

  it('keeps the newest entries once full', () => {
    const store = new LogStore(3)
    store.append([entry(1), entry(2), entry(3), entry(4), entry(5)])

    expect(store.size).toBe(3)
    expect([store.at(0)?.id, store.at(1)?.id, store.at(2)?.id]).toEqual([3, 4, 5])
    expect(store.lastId).toBe(5)
  })

  it('merges successive batches', () => {
    const store = new LogStore(10)
    store.append([entry(1), entry(2)])
    store.append([entry(3)])

    expect(store.size).toBe(3)
    expect(store.filteredCount).toBe(3)
  })

  it('ignores ids it has already seen, so replayed history cannot duplicate rows', () => {
    const store = new LogStore(10)
    store.append([entry(1), entry(2), entry(3)])

    // A reconnect replays an overlapping window.
    store.append([entry(2), entry(3), entry(4), entry(5)])

    expect(store.size).toBe(5)
    expect(store.filteredEntries().map((item) => item.id)).toEqual([1, 2, 3, 4, 5])
  })

  it('leaves no gap when history resumes exactly after the last id', () => {
    const store = new LogStore(10)
    store.append([entry(1), entry(2)])
    store.append([entry(3), entry(4)])

    expect(store.filteredEntries().map((item) => item.id)).toEqual([1, 2, 3, 4])
  })

  it('bumps the version only when something changed', () => {
    const store = new LogStore(5)
    const before = store.getVersion()

    store.append([])
    expect(store.getVersion()).toBe(before)

    store.append([entry(1)])
    expect(store.getVersion()).toBeGreaterThan(before)
  })

  it('notifies subscribers and stops after unsubscribe', () => {
    const store = new LogStore(5)
    let calls = 0
    const unsubscribe = store.subscribe(() => {
      calls++
    })

    store.append([entry(1)])
    expect(calls).toBe(1)

    unsubscribe()
    store.append([entry(2)])
    expect(calls).toBe(1)
  })

  it('clears the buffer', () => {
    const store = new LogStore(5)
    store.append([entry(1), entry(2)])
    store.clear()

    expect(store.size).toBe(0)
    expect(store.filteredCount).toBe(0)
    expect(store.lastId).toBe(0)
  })

  describe('filtering', () => {
    it('filters by minimum severity', () => {
      const store = new LogStore(10)
      store.append([
        entry(1, { severityNumber: SeverityNumbers.debug }),
        entry(2, { severityNumber: SeverityNumbers.warn }),
        entry(3, { severityNumber: SeverityNumbers.error }),
      ])

      store.setFilter(filter({ minSeverity: SeverityNumbers.warn }))
      expect(store.filteredEntries().map((item) => item.id)).toEqual([2, 3])
    })

    it('filters by service, category and body text', () => {
      const store = new LogStore(10)
      store.append([
        entry(1, { serviceName: 'orders', body: 'payment declined' }),
        entry(2, { serviceName: 'shipping', body: 'label printed' }),
        entry(3, { serviceName: 'orders', scopeName: 'Orders.Cart', body: 'cart opened' }),
      ])

      store.setFilter(filter({ services: new Set(['orders']) }))
      expect(store.filteredEntries().map((item) => item.id)).toEqual([1, 3])

      store.setFilter(filter({ scope: 'cart' }))
      expect(store.filteredEntries().map((item) => item.id)).toEqual([3])

      store.setFilter(filter({ search: 'PAYMENT' }))
      expect(store.filteredEntries().map((item) => item.id)).toEqual([1])
    })

    it('applies the filter incrementally to new arrivals', () => {
      const store = new LogStore(100)
      store.setFilter(filter({ minSeverity: SeverityNumbers.error }))

      store.append([entry(1, { severityNumber: SeverityNumbers.info })])
      expect(store.filteredCount).toBe(0)

      store.append([entry(2, { severityNumber: SeverityNumbers.error })])
      expect(store.filteredCount).toBe(1)

      store.append([entry(3, { severityNumber: SeverityNumbers.fatal })])
      expect(store.filteredEntries().map((item) => item.id)).toEqual([2, 3])
    })

    it('gives the same answer incrementally as from scratch', () => {
      const services = ['orders', 'shipping', 'billing']
      const severities = [
        SeverityNumbers.trace,
        SeverityNumbers.debug,
        SeverityNumbers.info,
        SeverityNumbers.warn,
        SeverityNumbers.error,
        SeverityNumbers.fatal,
      ]

      const incremental = new LogStore(500)
      const predicate = filter({ minSeverity: SeverityNumbers.info, services: new Set(['orders', 'billing']) })
      incremental.setFilter(predicate)

      // Append in uneven batches, the way frames arrive.
      let id = 0
      for (let batch = 0; batch < 40; batch++) {
        const entries: NebuLogEntry[] = []
        for (let i = 0; i < (batch % 7) + 1; i++) {
          id++
          entries.push(
            entry(id, {
              serviceName: services[id % services.length]!,
              severityNumber: severities[id % severities.length]!,
              body: id % 3 === 0 ? `payment ${id}` : `event ${id}`,
            }),
          )
        }

        incremental.append(entries)
      }

      expect(incremental.filteredEntries()).toEqual(bruteForce(incremental, predicate))
    })

    it('stays correct after the ring wraps', () => {
      const store = new LogStore(50)
      const predicate = filter({ minSeverity: SeverityNumbers.warn })
      store.setFilter(predicate)

      for (let id = 1; id <= 300; id++) {
        store.append([
          entry(id, {
            severityNumber: id % 2 === 0 ? SeverityNumbers.error : SeverityNumbers.debug,
          }),
        ])
      }

      const incremental = store.filteredEntries()
      expect(incremental).toEqual(bruteForce(store, predicate))

      // Nothing older than the ring may survive in the filter result.
      expect(incremental.every((item) => item.id > 250)).toBe(true)
    })

    it('recomputes when the predicate changes', () => {
      const store = new LogStore(10)
      store.append([
        entry(1, { severityNumber: SeverityNumbers.debug }),
        entry(2, { severityNumber: SeverityNumbers.error }),
      ])

      store.setFilter(filter({ minSeverity: SeverityNumbers.error }))
      expect(store.filteredCount).toBe(1)

      store.setFilter(filter())
      expect(store.filteredCount).toBe(2)
    })

    it('treats an equal filter as a no-op', () => {
      const store = new LogStore(10)
      store.append([entry(1)])
      store.setFilter(filter({ services: new Set(['orders']) }))

      const version = store.getVersion()
      store.setFilter(filter({ services: new Set(['orders']) }))

      expect(store.getVersion()).toBe(version)
    })
  })
})
