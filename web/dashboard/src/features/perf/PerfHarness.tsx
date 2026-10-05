import { useCallback, useEffect, useRef, useState, useSyncExternalStore } from 'react'
import type { NebuLogEntry } from '../../api/contracts'
import { SeverityNumbers } from '../../api/severity'
import { LogStore, defaultCapacity } from '../../store/logStore'
import LogTable from '../live/LogTable'

/**
 * Development-only performance harness for the acceptance criterion in WO-0006 §6: a buffer of
 * 100,000 entries, 1,000 more arriving every second, scrolling at 50 fps or better.
 *
 * It drives the real {@link LogStore} and the real {@link LogTable}, so what it measures is the
 * production rendering path with a synthetic producer in place of the hub. Reachable only under
 * `npm run dev`; `App` does not route to it in a production build.
 */
const severities = [
  SeverityNumbers.trace,
  SeverityNumbers.debug,
  SeverityNumbers.info,
  SeverityNumbers.warn,
  SeverityNumbers.error,
]

function makeBatch(firstId: number, size: number): NebuLogEntry[] {
  const entries = new Array<NebuLogEntry>(size)
  for (let i = 0; i < size; i++) {
    const id = firstId + i
    entries[i] = {
      id,
      timestampUnixMs: Date.now(),
      observedUnixMs: Date.now(),
      severityNumber: severities[id % severities.length]!,
      severityText: null,
      body: `order ${id} processed by worker ${id % 8} in ${(id % 97) + 3} ms`,
      serviceName: `service-${id % 4}`,
      serviceInstanceId: `instance-${id % 3}`,
      scopeName: 'Orders.Checkout',
      traceId: null,
      spanId: null,
      eventName: null,
      attributes: { orderId: String(id), worker: String(id % 8) },
      exception: null,
      source: 'SignalR',
    }
  }

  return entries
}

export default function PerfHarness() {
  const [store] = useState(() => new LogStore(defaultCapacity))
  const version = useSyncExternalStore(store.subscribe, store.getVersion)
  const [fps, setFps] = useState(0)
  const [worstFrame, setWorstFrame] = useState(0)
  const [streaming, setStreaming] = useState(false)
  const [memory, setMemory] = useState('n/a')
  const nextId = useRef(1)

  const prefill = useCallback(() => {
    for (let i = 0; i < defaultCapacity; i += 1000) {
      store.append(makeBatch(nextId.current, 1000))
      nextId.current += 1000
    }
  }, [store])

  // Synthetic producer: 1,000 entries a second, delivered in ten batches like a real stream.
  useEffect(() => {
    if (!streaming) return

    const timer = setInterval(() => {
      store.append(makeBatch(nextId.current, 100))
      nextId.current += 100
    }, 100)

    return () => clearInterval(timer)
  }, [streaming, store])

  // Frame sampler: reports the rolling frame rate and the worst frame in each window.
  useEffect(() => {
    let frames = 0
    let worst = 0
    let last = performance.now()
    let windowStart = last
    let handle = 0

    const tick = () => {
      const now = performance.now()
      const delta = now - last
      last = now
      frames++
      if (delta > worst) worst = delta

      if (now - windowStart >= 1000) {
        setFps(Math.round((frames * 1000) / (now - windowStart)))
        setWorstFrame(Math.round(worst))
        const used = (performance as { memory?: { usedJSHeapSize: number } }).memory?.usedJSHeapSize
        setMemory(used === undefined ? 'n/a' : `${(used / 1024 / 1024).toFixed(0)} MB`)
        frames = 0
        worst = 0
        windowStart = now
      }

      handle = requestAnimationFrame(tick)
    }

    handle = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(handle)
  }, [])

  return (
    <div style={{ display: 'flex', flexDirection: 'column', height: '100dvh', padding: '0.75rem', gap: '0.75rem' }}>
      <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center', fontFamily: 'var(--mono)' }}>
        <button type="button" onClick={prefill} data-testid="prefill">
          Fill to {defaultCapacity.toLocaleString()}
        </button>
        <button type="button" onClick={() => setStreaming((value) => !value)} data-testid="stream">
          {streaming ? 'Stop' : 'Stream'} 1,000/s
        </button>
        <span data-testid="buffered">buffered {store.size.toLocaleString()}</span>
        <span data-testid="fps">fps {fps}</span>
        <span data-testid="worst">worst frame {worstFrame} ms</span>
        <span data-testid="memory">heap {memory}</span>
      </div>

      <LogTable store={store} version={version} selectedId={null} onSelect={() => {}} />
    </div>
  )
}
