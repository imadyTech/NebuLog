import type { ConnectedClientInfo } from '../../api/contracts'
import { useNow } from '../../components/useNow'
import styles from './ClientsPanel.module.css'

/** The producers currently connected to the hub. */
export default function ClientsPanel({
  clients,
  selectedId,
  onSelect,
}: {
  clients: readonly ConnectedClientInfo[]
  selectedId: string | null
  onSelect?: (connectionId: string) => void
}) {
  const now = useNow()
  const producers = clients.filter((client) => client.kind === 'producer')

  return (
    <section className={styles.panel}>
      <h2 className={styles.title}>Producers ({producers.length})</h2>

      {producers.length === 0 ? (
        <p className={styles.empty}>No producers connected.</p>
      ) : (
        <ul className={styles.list}>
          {producers.map((client) => (
            <li key={client.connectionId}>
              <button
                type="button"
                className={`${styles.item} ${client.connectionId === selectedId ? styles.selected : ''}`}
                onClick={() => onSelect?.(client.connectionId)}
                disabled={onSelect === undefined}
              >
                <span className={styles.service}>{client.serviceName ?? 'unknown service'}</span>
                {client.serviceInstanceId !== null && (
                  <span className={styles.instance}>{client.serviceInstanceId}</span>
                )}
                <span className={styles.uptime}>{formatDuration(now - client.connectedUnixMs)}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function formatDuration(millis: number): string {
  const seconds = Math.max(0, Math.floor(millis / 1000))
  if (seconds < 60) return `${seconds}s`

  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes}m`

  const hours = Math.floor(minutes / 60)
  return hours < 24 ? `${hours}h ${minutes % 60}m` : `${Math.floor(hours / 24)}d ${hours % 24}h`
}
