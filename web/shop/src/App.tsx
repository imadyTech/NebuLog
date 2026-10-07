import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { Scenario, ShopApi } from '../../shared/scenarios.ts'
import {
  DEMO_CHANNEL,
  focusTraceMessage,
  requestMessage,
} from '../../shared/demoChannel.ts'
import styles from './App.module.css'
import { apiPath, callShop, type ShopRequest } from './shopClient.ts'
import { readShopUrl } from './urlState.ts'

interface Product {
  sku: string
  name: string
  priceNzd: number
}

const PRODUCTS: readonly Product[] = [
  { sku: 'MER-0101', name: 'Merino throw', priceNzd: 189.0 },
  { sku: 'RIM-0207', name: 'Rimu board', priceNzd: 94.5 },
  { sku: 'FLX-0312', name: 'Flax basket', priceNzd: 62.0 },
]

export default function App() {
  const initial = useMemo(() => readShopUrl(window.location.search), [])
  const [api, setApi] = useState<ShopApi>(initial.api)
  const [basket, setBasket] = useState<Record<string, number>>({})
  const [requests, setRequests] = useState<ShopRequest[]>([])
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)
  const nextId = useRef(1)

  const channel = useMemo(
    () => (typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel(DEMO_CHANNEL)),
    [],
  )

  useEffect(() => () => channel?.close(), [channel])

  const record = useCallback(
    (result: { method: string; path: string; status: number; ms: number; traceId: string }) => {
      const entry: ShopRequest = { id: nextId.current++, ...result }
      setRequests((current) => [entry, ...current].slice(0, 20))

      channel?.postMessage(
        requestMessage({
          traceId: result.traceId,
          method: result.method,
          path: result.path,
          status: result.status,
          ms: result.ms,
          scenario: initial.scenario?.id ?? null,
        }),
      )
    },
    [channel, initial.scenario],
  )

  const run = useCallback(
    async (work: () => Promise<{ method: string; path: string; status: number; ms: number; traceId: string }>) => {
      setBusy(true)
      setNotice(null)
      try {
        record(await work())
      } catch {
        setNotice('The request could not be sent. The shop service may still be starting.')
      } finally {
        setBusy(false)
      }
    },
    [record],
  )

  const openOrder = useCallback(
    (id: number) => run(() => callShop('GET', apiPath(api, `/orders/${id}`))),
    [api, run],
  )

  const checkout = useCallback(() => {
    const lines = Object.entries(basket).map(([sku, quantity]) => ({ sku, quantity }))
    return run(() =>
      callShop('POST', apiPath(api, '/checkout'), {
        lines: lines.length > 0 ? lines : [{ sku: PRODUCTS[0].sku, quantity: 1 }],
      }),
    )
  }, [api, basket, run])

  const invalidOrder = useCallback(
    () => run(() => callShop('POST', apiPath(api, '/orders'), { sku: PRODUCTS[0].sku, quantity: 0 })),
    [api, run],
  )

  const signIn = useCallback(
    () =>
      run(() =>
        // A fabricated credential, shown on the page so there is no doubt what was sent and what
        // the console then displays in its place.
        callShop('POST', apiPath(api, '/signin'), { username: 'demo-visitor', password: 'hunter2' }),
      ),
    [api, run],
  )

  const broken = useCallback(() => run(() => callShop('POST', apiPath(api, '/broken'))), [api, run])

  const burst = useCallback(
    () =>
      run(async () => {
        const result = await callShop<{ secondsRemaining?: number }>('POST', '/api/burst')
        if (result.status === 409) {
          setNotice(
            `A burst is already running${
              result.body?.secondsRemaining ? `; ${result.body.secondsRemaining} s left` : ''
            }.`,
          )
        }
        return result
      }),
    [run],
  )

  // The scenario the card opened this window for, run once the page is ready.
  const started = useRef(false)
  useEffect(() => {
    if (started.current || !initial.scenario) {
      return
    }

    started.current = true
    const action = initial.scenario.action

    // Deferred by one microtask so the first render commits before the request flips `busy`.
    // Firing it synchronously here starts a second render pass for no benefit — the work this
    // effect does is talk to an external system, not compute state.
    queueMicrotask(() => {
      switch (action.kind) {
        case 'open-order':
          void openOrder(action.orderId)
          break
        case 'checkout':
          void checkout()
          break
        case 'invalid-order':
          void invalidOrder()
          break
        case 'signin':
          void signIn()
          break
        case 'broken':
          void broken()
          break
        case 'burst':
          void burst()
          break
        case 'none':
          break
      }
    })
  }, [broken, burst, checkout, initial.scenario, invalidOrder, openOrder, signIn])

  const showInConsole = useCallback(
    (traceId: string) => {
      channel?.postMessage(focusTraceMessage(traceId))
      window.opener?.focus()
    },
    [channel],
  )

  const total = Object.entries(basket).reduce((sum, [sku, quantity]) => {
    const product = PRODUCTS.find((candidate) => candidate.sku === sku)
    return sum + (product ? product.priceNzd * quantity : 0)
  }, 0)

  return (
    <div className={styles.shop}>
      <header className={styles.header}>
        <div>
          <h1 className={styles.title}>NebuShop</h1>
          <p className={styles.subtitle}>demo application · every button is one request</p>
        </div>
        <div className={styles.apiToggle} role="group" aria-label="Backend implementation">
          {(['minimal', 'mvc'] as const).map((candidate) => (
            <button
              key={candidate}
              type="button"
              aria-pressed={api === candidate}
              className={styles.apiButton}
              onClick={() => setApi(candidate)}
            >
              {candidate === 'minimal' ? 'Minimal API' : 'MVC'}
            </button>
          ))}
        </div>
      </header>

      {initial.scenario ? <ScenarioBanner scenario={initial.scenario} /> : null}
      {notice ? (
        <p className={styles.notice} role="status">
          {notice}
        </p>
      ) : null}

      <main className={styles.main}>
        <section aria-labelledby="catalogue-heading">
          <h2 id="catalogue-heading" className={styles.sectionTitle}>
            Catalogue
          </h2>
          <ul className={styles.products}>
            {PRODUCTS.map((product) => (
              <li key={product.sku} className={styles.product}>
                <div className={styles.productArt} aria-hidden="true" />
                <h3 className={styles.productName}>{product.name}</h3>
                <p className={styles.sku}>{product.sku}</p>
                <p className={styles.price}>${product.priceNzd.toFixed(2)}</p>
                <button
                  type="button"
                  className={styles.secondary}
                  onClick={() =>
                    setBasket((current) => ({ ...current, [product.sku]: (current[product.sku] ?? 0) + 1 }))
                  }
                >
                  Add to basket
                </button>
              </li>
            ))}
          </ul>
        </section>

        <aside className={styles.side}>
          <section aria-labelledby="basket-heading" className={styles.panel}>
            <h2 id="basket-heading" className={styles.sectionTitle}>
              Basket
            </h2>
            {Object.keys(basket).length === 0 ? (
              <p className={styles.empty}>Empty — checkout will use one Merino throw.</p>
            ) : (
              <ul className={styles.basket}>
                {Object.entries(basket).map(([sku, quantity]) => (
                  <li key={sku}>
                    <span className={styles.mono}>{sku}</span>
                    <span>×{quantity}</span>
                  </li>
                ))}
              </ul>
            )}
            <p className={styles.total}>${(total || PRODUCTS[0].priceNzd).toFixed(2)}</p>
            <button type="button" className={styles.primary} disabled={busy} onClick={() => void checkout()}>
              Checkout
            </button>
            <p className={styles.hint}>Checkout calls the payments service over HTTP.</p>
          </section>

          <section aria-labelledby="actions-heading" className={styles.panel}>
            <h2 id="actions-heading" className={styles.sectionTitle}>
              Scenario actions
            </h2>
            <div className={styles.actions}>
              <button type="button" className={styles.secondary} disabled={busy} onClick={() => void openOrder(42)}>
                Open order #42
              </button>
              <button type="button" className={styles.secondary} disabled={busy} onClick={() => void invalidOrder()}>
                Order with quantity 0
              </button>
              <button type="button" className={styles.secondary} disabled={busy} onClick={() => void signIn()}>
                Sign in with a password
              </button>
              <button type="button" className={styles.secondary} disabled={busy} onClick={() => void broken()}>
                Broken button
              </button>
              <button type="button" className={styles.secondary} disabled={busy} onClick={() => void burst()}>
                Burst 1,000 logs/s
              </button>
            </div>
          </section>
        </aside>
      </main>

      <section aria-labelledby="requests-heading" className={styles.requests}>
        <h2 id="requests-heading" className={styles.sectionTitle}>
          Your requests
        </h2>
        {requests.length === 0 ? (
          <p className={styles.empty}>Nothing yet. Every button above sends exactly one request.</p>
        ) : (
          <table className={styles.table}>
            <thead>
              <tr>
                <th scope="col">Method</th>
                <th scope="col">Path</th>
                <th scope="col">Status</th>
                <th scope="col">Time</th>
                <th scope="col">TraceId</th>
                <th scope="col"> </th>
              </tr>
            </thead>
            <tbody>
              {requests.map((request) => (
                <tr key={request.id}>
                  <td className={styles.mono}>{request.method}</td>
                  <td className={styles.mono}>{request.path}</td>
                  <td className={styles.mono}>{request.status}</td>
                  <td className={styles.mono}>{request.ms} ms</td>
                  <td className={styles.mono}>{request.traceId.slice(0, 16) || '—'}</td>
                  <td>
                    {request.traceId ? (
                      <button type="button" className={styles.link} onClick={() => showInConsole(request.traceId)}>
                        Show in console
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  )
}

function ScenarioBanner({ scenario }: { scenario: Scenario }) {
  return (
    <p className={styles.scenario} role="status">
      <span className={styles.scenarioNumber}>{scenario.number}</span>
      <span>
        <strong>{scenario.title}</strong> — {scenario.watchFor}
      </span>
    </p>
  )
}
