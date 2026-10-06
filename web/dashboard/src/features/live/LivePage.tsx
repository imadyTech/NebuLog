import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react'
import { hasRole, useAuth } from '../../api/useAuth'
import { api } from '../../api/client'
import {
  Roles,
  type ConnectedClientInfo,
  type LiveSummaryDto,
  type NebuLogEntry,
  type StatSnapshot,
} from '../../api/contracts'
import { useDebounced } from '../../components/useDebounced'
import { NebuLogHubClient, type ConnectionStatus } from '../../hub/connection'
import { LogStore, defaultCapacity, type LogFilter } from '../../store/logStore'
import ClientsPanel from '../clients/ClientsPanel'
import CommandPanel from '../commands/CommandPanel'
import StatsPanel from '../stats/StatsPanel'
import SummaryPanel from '../summary/SummaryPanel'
import EntryDrawer from './EntryDrawer'
import FilterBar from './FilterBar'
import LogTable from './LogTable'
import styles from './LivePage.module.css'

/** The live log view: table, filters, detail drawer and the side panels. */
export default function LivePage() {
  const { user } = useAuth()
  const store = useMemo(() => new LogStore(defaultCapacity), [])
  const version = useSyncExternalStore(store.subscribe, store.getVersion)

  const [status, setStatus] = useState<ConnectionStatus>('connecting')
  const [summary, setSummary] = useState<LiveSummaryDto | null>(null)
  const [clients, setClients] = useState<ConnectedClientInfo[]>([])
  const [stats, setStats] = useState<StatSnapshot[]>([])
  const [selected, setSelected] = useState<NebuLogEntry | null>(null)
  const [selectedProducer, setSelectedProducer] = useState<string | null>(null)
  const [paused, setPaused] = useState(false)

  const [minSeverity, setMinSeverity] = useState(0)
  const [services, setServices] = useState<ReadonlySet<string>>(new Set())
  const [scope, setScope] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const search = useDebounced(searchInput, 200)

  const hubRef = useRef<NebuLogHubClient | null>(null)

  useEffect(() => {
    const hub = new NebuLogHubClient(store, {
      onLogs: () => {},
      onSummary: setSummary,
      onClients: setClients,
      onStatDefined: (definition) =>
        setStats((current) =>
          current.some((stat) => stat.definition.id === definition.id)
            ? current.map((stat) => (stat.definition.id === definition.id ? { ...stat, definition } : stat))
            : [...current, { definition, latest: null }],
        ),
      onStatUpdated: (update) =>
        setStats((current) =>
          current.map((stat) => (stat.definition.id === update.id ? { ...stat, latest: update } : stat)),
        ),
      onStatus: setStatus,
    })

    hubRef.current = hub
    void hub.start()

    // Seed the panels so the dashboard is populated before the first push arrives.
    void api.summary().then(setSummary).catch(noop)
    void api.clients().then(setClients).catch(noop)
    void api.stats().then(setStats).catch(noop)

    return () => {
      hubRef.current = null
      void hub.stop()
    }
  }, [store])

  const filter = useMemo<LogFilter>(
    () => ({ minSeverity, services, scope, search }),
    [minSeverity, services, scope, search],
  )

  store.setFilter(filter)

  const togglePause = useCallback(() => {
    setPaused((current) => {
      hubRef.current?.setPaused(!current)
      return !current
    })
  }, [])

  const exportJson = useCallback(() => {
    const blob = new Blob([JSON.stringify(store.filteredEntries(), null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `nebulog-${new Date().toISOString().replace(/[:.]/g, '-')}.json`
    anchor.click()
    URL.revokeObjectURL(url)
  }, [store])

  const sendCommand = useCallback(
    (connectionId: string, name: string, args: Record<string, string>) =>
      hubRef.current?.sendCommand(connectionId, name, args) ?? Promise.reject(new Error('Not connected.')),
    [],
  )

  const producers = useMemo(() => clients.filter((client) => client.kind === 'producer'), [clients])
  const knownServices = summary?.services ?? []
  const canCommand = hasRole(user, Roles.operator)

  return (
    <div className={styles.layout}>
      <div className={styles.main}>
        <FilterBar
          status={status}
          paused={paused}
          minSeverity={minSeverity}
          onMinSeverity={setMinSeverity}
          services={services}
          knownServices={knownServices}
          onServices={setServices}
          scope={scope}
          onScope={setScope}
          search={searchInput}
          onSearch={setSearchInput}
          shown={store.filteredCount}
          buffered={store.size}
          onTogglePause={togglePause}
          onClear={() => {
            store.clear()
            setSelected(null)
          }}
          onExport={exportJson}
        />

        <div className={styles.content}>
          <LogTable store={store} version={version} selectedId={selected?.id ?? null} onSelect={setSelected} />
          {selected !== null && <EntryDrawer entry={selected} onClose={() => setSelected(null)} />}
        </div>
      </div>

      <aside className={styles.side}>
        <SummaryPanel summary={summary} buffered={store.size} />
        <StatsPanel stats={stats} />
        <ClientsPanel
          clients={clients}
          selectedId={selectedProducer}
          onSelect={canCommand ? setSelectedProducer : undefined}
        />
        {canCommand && (
          <CommandPanel
            producers={producers}
            selectedId={selectedProducer}
            onSelect={setSelectedProducer}
            send={sendCommand}
          />
        )}
      </aside>
    </div>
  )
}

function noop() {}
