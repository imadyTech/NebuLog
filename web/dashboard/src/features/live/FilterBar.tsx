import { severityFilterOptions } from '../../api/severity'
import type { ConnectionStatus } from '../../hub/connection'
import styles from './FilterBar.module.css'

interface FilterBarProps {
  status: ConnectionStatus
  paused: boolean
  minSeverity: number
  onMinSeverity: (value: number) => void
  services: ReadonlySet<string>
  knownServices: readonly string[]
  onServices: (value: ReadonlySet<string>) => void
  scope: string
  onScope: (value: string) => void
  search: string
  onSearch: (value: string) => void
  shown: number
  buffered: number
  onTogglePause: () => void
  onClear: () => void
  onExport: () => void
}

const statusLabels: Record<ConnectionStatus, string> = {
  connecting: 'Connecting',
  connected: 'Connected',
  reconnecting: 'Reconnecting',
  disconnected: 'Disconnected',
}

/** Filters and stream controls. Filtering happens on the client buffer; it never refetches. */
export default function FilterBar(props: FilterBarProps) {
  const {
    status,
    paused,
    minSeverity,
    onMinSeverity,
    services,
    knownServices,
    onServices,
    scope,
    onScope,
    search,
    onSearch,
    shown,
    buffered,
    onTogglePause,
    onClear,
    onExport,
  } = props

  function toggleService(service: string) {
    const next = new Set(services)
    if (!next.delete(service)) {
      next.add(service)
    }

    onServices(next)
  }

  return (
    <div className={styles.bar}>
      <span className={`${styles.status} ${styles[status]}`} role="status">
        <span className={styles.dot} aria-hidden="true" />
        {statusLabels[status]}
      </span>

      <label className={styles.field}>
        <span className={styles.fieldLabel}>Level</span>
        <select
          className={styles.select}
          value={minSeverity}
          onChange={(event) => onMinSeverity(Number(event.target.value))}
        >
          {severityFilterOptions.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      </label>

      <label className={styles.field}>
        <span className={styles.fieldLabel}>Category</span>
        <input
          className={styles.input}
          value={scope}
          placeholder="contains…"
          onChange={(event) => onScope(event.target.value)}
        />
      </label>

      <label className={styles.grow}>
        <span className={styles.fieldLabel}>Search</span>
        <input
          className={styles.input}
          type="search"
          value={search}
          placeholder="message contains…"
          onChange={(event) => onSearch(event.target.value)}
        />
      </label>

      {knownServices.length > 0 && (
        <div className={styles.services} role="group" aria-label="Filter by service">
          {knownServices.map((service) => (
            <button
              key={service}
              type="button"
              className={`${styles.chip} ${services.has(service) ? styles.chipOn : ''}`}
              aria-pressed={services.has(service)}
              onClick={() => toggleService(service)}
            >
              {service}
            </button>
          ))}
        </div>
      )}

      <span className={styles.counts}>
        {shown.toLocaleString()} / {buffered.toLocaleString()}
      </span>

      <div className={styles.actions}>
        <button type="button" className={styles.button} onClick={onTogglePause} aria-pressed={paused}>
          {paused ? 'Resume' : 'Pause'}
        </button>
        <button type="button" className={styles.button} onClick={onClear}>
          Clear
        </button>
        <button type="button" className={styles.button} onClick={onExport} disabled={shown === 0}>
          Export
        </button>
      </div>
    </div>
  )
}
