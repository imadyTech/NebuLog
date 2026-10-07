import type { NebuLogEntry } from '../api/contracts'
import type { DemoRequestMessage } from '../../../shared/demoChannel'
import { summariseTrace } from './traceSummary'
import styles from './TracePanel.module.css'

interface Props {
  /** Requests the shop window has reported, newest first. */
  requests: readonly DemoRequestMessage[]
  /** The console's buffer, used to summarise what arrived for each trace. */
  entries: readonly NebuLogEntry[]
  /** The trace the console is currently filtered to, if any. */
  activeTrace: string
  onSelect: (traceId: string) => void
  onClear: () => void
}

/** The side panel listing the visitor's own requests and what each one produced. */
export default function TracePanel({ requests, entries, activeTrace, onSelect, onClear }: Props) {
  if (requests.length === 0) {
    return null
  }

  return (
    <section className={styles.panel} aria-labelledby="your-trace-heading">
      <div className={styles.head}>
        <h2 id="your-trace-heading" className={styles.title}>
          Your trace
        </h2>
        {activeTrace ? (
          <button type="button" className={styles.clear} onClick={onClear}>
            Show all
          </button>
        ) : null}
      </div>

      <ul className={styles.list}>
        {requests.map((request) => {
          const summary = summariseTrace(request.traceId, entries)
          const active = request.traceId === activeTrace

          return (
            <li key={request.traceId} className={active ? styles.itemActive : styles.item}>
              <button type="button" className={styles.select} onClick={() => onSelect(request.traceId)}>
                <span className={styles.request}>
                  <span className={styles.method}>{request.method}</span>
                  <span className={styles.path}>{request.path}</span>
                  <span className={styles.status}>{request.status}</span>
                </span>
                <span className={styles.trace}>{request.traceId.slice(0, 16)}…</span>
                <span className={styles.meta}>
                  {summary.count === 0
                    ? 'waiting for entries…'
                    : `${summary.count} entries · ${summary.services.length} service${
                        summary.services.length === 1 ? '' : 's'
                      } · ${summary.spanMs} ms`}
                </span>
                {summary.services.length > 0 ? (
                  <span className={styles.services}>{summary.services.join(', ')}</span>
                ) : null}
              </button>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
