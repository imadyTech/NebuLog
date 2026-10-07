import type { NebuLogEntry } from '../api/contracts'

/** What arrived for one trace: which services, how many entries, over what span. */
export interface TraceSummary {
  traceId: string
  services: string[]
  count: number
  spanMs: number
}

/**
 * Summarises the entries belonging to one trace.
 *
 * Kept out of the component so it can be tested directly: the span is the part worth asserting on,
 * because an empty trace and a single-entry trace both have to come out as zero rather than NaN.
 */
export function summariseTrace(traceId: string, entries: readonly NebuLogEntry[]): TraceSummary {
  const matching = entries.filter((entry) => entry.traceId === traceId)

  if (matching.length === 0) {
    return { traceId, services: [], count: 0, spanMs: 0 }
  }

  let earliest = Number.POSITIVE_INFINITY
  let latest = Number.NEGATIVE_INFINITY
  const services = new Set<string>()

  for (const entry of matching) {
    services.add(entry.serviceName)
    earliest = Math.min(earliest, entry.timestampUnixMs)
    latest = Math.max(latest, entry.timestampUnixMs)
  }

  return {
    traceId,
    services: [...services].sort(),
    count: matching.length,
    spanMs: latest - earliest,
  }
}
