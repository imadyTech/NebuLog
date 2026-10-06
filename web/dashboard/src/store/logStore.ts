import type { NebuLogEntry } from '../api/contracts'

/** The predicate set applied to the buffer. All fields are optional and combine with AND. */
export interface LogFilter {
  minSeverity: number
  services: ReadonlySet<string>
  scope: string
  search: string
  /** Empty shows every trace; otherwise only entries carrying this exact TraceId. */
  traceId: string
}

export const emptyFilter: LogFilter = {
  minSeverity: 0,
  services: new Set<string>(),
  scope: '',
  search: '',
  traceId: '',
}

/** Default ring capacity. The work order calls for 100,000 entries to stay smooth. */
export const defaultCapacity = 100_000

/**
 * A fixed-capacity ring of log entries with an incrementally maintained filter result.
 *
 * The entries are plain objects in a plain array — deliberately *not* React state. React only
 * ever sees a version number, so appending ten thousand entries costs one re-render rather than
 * ten thousand reconciliations. This is the difference between v1's full-table rebuild per entry
 * and v2 staying smooth at a hundred thousand rows.
 */
export class LogStore {
  private entries: (NebuLogEntry | undefined)[]
  private start = 0
  private count = 0
  private filtered: number[] = []
  private filter: LogFilter = emptyFilter
  private nextToFilter = 0
  private totalAppended = 0
  private version = 0
  private readonly listeners = new Set<() => void>()

  readonly capacity: number

  constructor(capacity: number = defaultCapacity) {
    if (capacity < 1) {
      throw new RangeError('capacity must be positive')
    }

    this.capacity = capacity
    this.entries = new Array<NebuLogEntry | undefined>(capacity)
  }

  /** Entries currently held. */
  get size(): number {
    return this.count
  }

  /** How many entries pass the current filter. */
  get filteredCount(): number {
    return this.filtered.length
  }

  /** Bumped on every mutation; this is the only value React subscribes to. */
  getVersion = (): number => this.version

  /** Subscribes to mutations, for `useSyncExternalStore`. */
  subscribe = (listener: () => void): (() => void) => {
    this.listeners.add(listener)
    return () => {
      this.listeners.delete(listener)
    }
  }

  /** The highest id seen, or 0 when empty. Used to resume history after a reconnect. */
  get lastId(): number {
    return this.count === 0 ? 0 : (this.at(this.count - 1)?.id ?? 0)
  }

  /** The entry at an absolute buffer position, oldest first. */
  at(index: number): NebuLogEntry | undefined {
    if (index < 0 || index >= this.count) {
      return undefined
    }

    return this.entries[(this.start + index) % this.capacity]
  }

  /** The i-th entry that passes the filter. */
  filteredAt(index: number): NebuLogEntry | undefined {
    const absolute = this.filtered[index]
    return absolute === undefined ? undefined : this.at(absolute - this.droppedCount)
  }

  /** Every entry passing the filter, oldest first. Used for export. */
  filteredEntries(): NebuLogEntry[] {
    const result: NebuLogEntry[] = []
    for (let i = 0; i < this.filtered.length; i++) {
      const entry = this.filteredAt(i)
      if (entry !== undefined) {
        result.push(entry)
      }
    }

    return result
  }

  /** How many entries have been overwritten since the store was created. */
  private get droppedCount(): number {
    return this.totalAppended - this.count
  }

  /**
   * Appends a batch, dropping the oldest entries once full, and extends the filter result with
   * only the new arrivals.
   *
   * Entries whose id has already been seen are skipped, so replaying history after a reconnect
   * cannot duplicate rows.
   */
  append(batch: readonly NebuLogEntry[]): void {
    if (batch.length === 0) {
      return
    }

    let appended = 0
    for (const entry of batch) {
      if (entry.id !== 0 && entry.id <= this.lastId) {
        continue
      }

      this.entries[(this.start + this.count) % this.capacity] = entry
      if (this.count === this.capacity) {
        this.start = (this.start + 1) % this.capacity
      } else {
        this.count++
      }

      this.totalAppended++
      appended++
    }

    if (appended === 0) {
      return
    }

    this.dropStaleFilterIndexes()
    this.extendFilter()
    this.bump()
  }

  /** Replaces the filter. Unchanged filters are a no-op so callers can set it every render. */
  setFilter(filter: LogFilter): void {
    if (sameFilter(this.filter, filter)) {
      return
    }

    this.filter = filter
    this.recomputeFilter()
    this.bump()
  }

  /** The filter currently applied. */
  getFilter(): LogFilter {
    return this.filter
  }

  /** Empties the buffer, keeping the filter. */
  clear(): void {
    this.entries = new Array<NebuLogEntry | undefined>(this.capacity)
    this.start = 0
    this.count = 0
    this.totalAppended = 0
    this.filtered = []
    this.nextToFilter = 0
    this.bump()
  }

  /** Recomputes the whole filter result. Only needed when the predicate changes. */
  private recomputeFilter(): void {
    this.filtered = []
    this.nextToFilter = this.droppedCount
    this.extendFilter()
  }

  /** Tests only the entries that have arrived since the last pass. */
  private extendFilter(): void {
    const end = this.totalAppended
    for (let absolute = this.nextToFilter; absolute < end; absolute++) {
      const entry = this.at(absolute - this.droppedCount)
      if (entry !== undefined && matches(entry, this.filter)) {
        this.filtered.push(absolute)
      }
    }

    this.nextToFilter = end
  }

  /** Discards filter indexes pointing at entries the ring has already overwritten. */
  private dropStaleFilterIndexes(): void {
    const oldest = this.droppedCount
    if (this.filtered.length === 0 || this.filtered[0]! >= oldest) {
      return
    }

    let firstLive = 0
    while (firstLive < this.filtered.length && this.filtered[firstLive]! < oldest) {
      firstLive++
    }

    this.filtered = this.filtered.slice(firstLive)
  }

  private bump(): void {
    this.version++
    for (const listener of this.listeners) {
      listener()
    }
  }
}

/** True when an entry satisfies every active predicate. */
export function matches(entry: NebuLogEntry, filter: LogFilter): boolean {
  if (entry.severityNumber < filter.minSeverity) {
    return false
  }

  if (filter.services.size > 0 && !filter.services.has(entry.serviceName)) {
    return false
  }

  if (filter.scope.length > 0 && !entry.scopeName.toLowerCase().includes(filter.scope.toLowerCase())) {
    return false
  }

  if (filter.search.length > 0 && !entry.body.toLowerCase().includes(filter.search.toLowerCase())) {
    return false
  }

  // Exact match, not a prefix: a TraceId identifies one request, and a partial match would quietly
  // widen "show only my trace" into "show traces that happen to start the same way".
  if (filter.traceId.length > 0 && entry.traceId !== filter.traceId) {
    return false
  }

  return true
}

function sameFilter(left: LogFilter, right: LogFilter): boolean {
  if (
    left.minSeverity !== right.minSeverity ||
    left.scope !== right.scope ||
    left.search !== right.search ||
    left.traceId !== right.traceId ||
    left.services.size !== right.services.size
  ) {
    return false
  }

  for (const service of left.services) {
    if (!right.services.has(service)) {
      return false
    }
  }

  return true
}
