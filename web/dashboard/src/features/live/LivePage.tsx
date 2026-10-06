import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react'
import { useSearchParams } from 'react-router'
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
import ScenarioBanner from '../../site/ScenarioBanner'
import TracePanel from '../../site/TracePanel'
import FilterChips, { type Chip } from './FilterChips'
import { readConsoleUrl, writeConsoleUrl } from '../../site/consoleUrlState'
import { useDemoChannel } from '../../site/useDemoChannel'
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

  // The query string is the source of truth for the shareable part of the filter, so a console
  // link can be copied and reopened with the same view (WO-0011 §2.7).
  const [searchParams, setSearchParams] = useSearchParams()
  // Read once, from the URL the visitor arrived on. Later changes flow the other way: state is
  // the source of truth and the effect below writes it back.
  const [initialUrl] = useState(() => readConsoleUrl(window.location.search))
  const scenario = initialUrl.scenario

  const [minSeverity, setMinSeverity] = useState(initialUrl.minSeverity)
  const [services, setServices] = useState<ReadonlySet<string>>(initialUrl.services)
  const [traceId, setTraceId] = useState(initialUrl.traceId)
  const [bannerOpen, setBannerOpen] = useState(scenario !== undefined)

  // The shop asked for a trace to be brought to the front. Narrowing happens here rather than in
  // an effect, because the message is an event, not state to synchronise.
  const demo = useDemoChannel(scenario !== undefined || initialUrl.follow, setTraceId)
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
    () => ({ minSeverity, services, scope, search, traceId }),
    [minSeverity, services, scope, search, traceId],
  )

  store.setFilter(filter)

  // Push the shareable part of the filter back into the URL. replace: true keeps the back button
  // meaningful — typing in a filter should not fill the history with intermediate states.
  useEffect(() => {
    const next = writeConsoleUrl({ scenarioId: scenario?.id, services, traceId, minSeverity })
    if (`?${searchParams.toString()}` !== next && !(searchParams.toString() === '' && next === '')) {
      setSearchParams(new URLSearchParams(next), { replace: true })
    }
  }, [minSeverity, scenario?.id, searchParams, services, setSearchParams, traceId])

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

  const chips = useMemo<Chip[]>(() => {
    const result: Chip[] = []

    for (const service of [...services].sort()) {
      result.push({
        key: `service:${service}`,
        label: `service: ${service}`,
        onRemove: () =>
          setServices((current) => new Set([...current].filter((candidate) => candidate !== service))),
      })
    }

    if (minSeverity > 0) {
      result.push({ key: 'level', label: `level ≥ ${severityName(minSeverity)}`, onRemove: () => setMinSeverity(0) })
    }

    if (traceId) {
      result.push({ key: 'trace', label: `trace ${traceId.slice(0, 12)}…`, onRemove: () => setTraceId('') })
    }

    if (scope) {
      result.push({ key: 'scope', label: `scope: ${scope}`, onRemove: () => setScope('') })
    }

    if (search) {
      result.push({ key: 'search', label: `text: ${search}`, onRemove: () => setSearchInput('') })
    }

    return result
  }, [minSeverity, scope, search, services, traceId])

  const producers = useMemo(() => clients.filter((client) => client.kind === 'producer'), [clients])
  const knownServices = summary?.services ?? []
  const canCommand = hasRole(user, Roles.operator)

  return (
    <div className={styles.layout}>
      <div className={styles.main}>
        {scenario && bannerOpen ? (
          <ScenarioBanner
            scenario={scenario}
            popupBlocked={initialUrl.popupBlocked}
            traceFiltered={traceId.length > 0}
            latestTrace={demo.traces[0]?.traceId ?? null}
            onShowOnlyMyTrace={() => setTraceId(demo.traces[0]?.traceId ?? '')}
            onDismiss={() => setBannerOpen(false)}
          />
        ) : null}

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

        <FilterChips chips={chips} shown={store.filteredCount} buffered={store.size} />

        <div className={styles.content}>
          <LogTable store={store} version={version} selectedId={selected?.id ?? null} onSelect={setSelected} />
          {selected !== null && <EntryDrawer entry={selected} onClose={() => setSelected(null)} />}
        </div>
      </div>

      <aside className={styles.side}>
        <TracePanel
          requests={demo.traces}
          entries={store.filteredEntries()}
          activeTrace={traceId}
          onSelect={setTraceId}
          onClear={() => setTraceId('')}
        />
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

/** The names used on the filter chips, matching the severity bands the server reports. */
function severityName(minSeverity: number): string {
  if (minSeverity >= 21) return 'Fatal'
  if (minSeverity >= 17) return 'Error'
  if (minSeverity >= 13) return 'Warn'
  if (minSeverity >= 9) return 'Info'
  if (minSeverity >= 5) return 'Debug'
  return 'Trace'
}
