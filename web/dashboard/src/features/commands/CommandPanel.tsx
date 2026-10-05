import { useState } from 'react'
import type { ConnectedClientInfo } from '../../api/contracts'
import { severityFilterOptions } from '../../api/severity'
import styles from './CommandPanel.module.css'

type Outcome = { kind: 'ok' | 'error'; text: string } | null

interface CommandPanelProps {
  producers: readonly ConnectedClientInfo[]
  selectedId: string | null
  onSelect: (connectionId: string) => void
  send: (connectionId: string, name: string, args: Record<string, string>) => Promise<void>
}

/** Sends a command to one connected producer. Visible to operators and administrators only. */
export default function CommandPanel({ producers, selectedId, onSelect, send }: CommandPanelProps) {
  const [level, setLevel] = useState(String(severityFilterOptions[3]!.value))
  const [outcome, setOutcome] = useState<Outcome>(null)
  const [busy, setBusy] = useState(false)

  const target = producers.find((producer) => producer.connectionId === selectedId) ?? null

  async function dispatch(name: string, args: Record<string, string>) {
    if (target === null) {
      return
    }

    setBusy(true)
    setOutcome(null)
    try {
      await send(target.connectionId, name, args)
      setOutcome({ kind: 'ok', text: `Sent ${name} to ${target.serviceName ?? 'producer'}.` })
    } catch (error) {
      setOutcome({ kind: 'error', text: error instanceof Error ? error.message : 'The command was refused.' })
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className={styles.panel}>
      <h2 className={styles.title}>Commands</h2>

      {producers.length === 0 ? (
        <p className={styles.empty}>Connect a producer to send commands.</p>
      ) : (
        <>
          <label className={styles.label} htmlFor="command-target">
            Target
          </label>
          <select
            id="command-target"
            className={styles.select}
            value={selectedId ?? ''}
            onChange={(event) => onSelect(event.target.value)}
          >
            <option value="">Choose a producer…</option>
            {producers.map((producer) => (
              <option key={producer.connectionId} value={producer.connectionId}>
                {producer.serviceName ?? 'unknown'} · {producer.connectionId.slice(0, 8)}
              </option>
            ))}
          </select>

          <div className={styles.actions}>
            <button
              type="button"
              className={styles.button}
              disabled={busy || target === null}
              onClick={() => void dispatch('ping', {})}
            >
              ping
            </button>

            <div className={styles.inline}>
              <select
                className={styles.select}
                value={level}
                onChange={(event) => setLevel(event.target.value)}
                aria-label="Minimum level"
              >
                {severityFilterOptions
                  .filter((option) => option.value > 0)
                  .map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
              </select>
              <button
                type="button"
                className={styles.button}
                disabled={busy || target === null}
                onClick={() => void dispatch('set-min-level', { level })}
              >
                set-min-level
              </button>
            </div>
          </div>

          {outcome !== null && (
            <p className={outcome.kind === 'ok' ? styles.ok : styles.error} role="status">
              {outcome.text}
            </p>
          )}
        </>
      )}
    </section>
  )
}
