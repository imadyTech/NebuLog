import { useState } from 'react'
import type { NebuLogEntry } from '../../api/contracts'
import { severityBand, severityShortName } from '../../api/severity'
import styles from './EntryDrawer.module.css'

interface EntryDrawerProps {
  entry: NebuLogEntry | null
  onClose: () => void
}

/** Full detail for one entry. Everything is rendered as text; nothing is injected as HTML. */
export default function EntryDrawer({ entry, onClose }: EntryDrawerProps) {
  if (entry === null) {
    return null
  }

  const attributes = Object.entries(entry.attributes)

  return (
    <aside className={styles.drawer} aria-label="Log entry detail">
      <header className={styles.header}>
        <span className={`${styles.badge} ${styles[`level${severityBand(entry.severityNumber)}`]}`}>
          {severityShortName(entry.severityNumber)}
        </span>
        <span className={styles.timestamp}>{new Date(entry.timestampUnixMs).toISOString()}</span>
        <button type="button" className={styles.close} onClick={onClose} aria-label="Close detail">
          ×
        </button>
      </header>

      <div className={styles.body}>
        <Section title="Message">
          <p className={styles.message}>{entry.body}</p>
        </Section>

        <Section title="Origin">
          <dl className={styles.pairs}>
            <Pair label="Service" value={entry.serviceName} />
            {entry.serviceInstanceId !== null && <Pair label="Instance" value={entry.serviceInstanceId} />}
            <Pair label="Category" value={entry.scopeName} />
            {entry.eventName !== null && <Pair label="Event" value={entry.eventName} />}
            <Pair label="Source" value={entry.source} />
            <Pair label="Observed" value={new Date(entry.observedUnixMs).toISOString()} />
          </dl>
        </Section>

        {(entry.traceId !== null || entry.spanId !== null) && (
          <Section title="Correlation">
            <dl className={styles.pairs}>
              {entry.traceId !== null && <Pair label="Trace id" value={entry.traceId} copyable />}
              {entry.spanId !== null && <Pair label="Span id" value={entry.spanId} copyable />}
            </dl>
          </Section>
        )}

        {attributes.length > 0 && (
          <Section title={`Attributes (${attributes.length})`}>
            <dl className={styles.pairs}>
              {attributes.map(([key, value]) => (
                <Pair key={key} label={key} value={value} />
              ))}
            </dl>
          </Section>
        )}

        {entry.exception !== null && (
          <Section title="Exception">
            <dl className={styles.pairs}>
              <Pair label="Type" value={entry.exception.type} />
              <Pair label="Message" value={entry.exception.message} />
            </dl>
            {entry.exception.stackTrace !== null && <pre className={styles.stack}>{entry.exception.stackTrace}</pre>}
          </Section>
        )}
      </div>
    </aside>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className={styles.section}>
      <h3 className={styles.sectionTitle}>{title}</h3>
      {children}
    </section>
  )
}

function Pair({ label, value, copyable = false }: { label: string; value: string; copyable?: boolean }) {
  const [copied, setCopied] = useState(false)

  async function copy() {
    try {
      await navigator.clipboard.writeText(value)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch {
      // Clipboard access can be refused; the value is selectable either way.
    }
  }

  return (
    <>
      <dt className={styles.key}>{label}</dt>
      <dd className={styles.value}>
        <span className={styles.valueText}>{value}</span>
        {copyable && (
          <button type="button" className={styles.copy} onClick={() => void copy()}>
            {copied ? 'Copied' : 'Copy'}
          </button>
        )}
      </dd>
    </>
  )
}
