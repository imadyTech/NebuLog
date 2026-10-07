import type { ShopApi } from '../../shared/scenarios.ts'

/** One completed request, as the shop lists it and broadcasts it. */
export interface ShopRequest {
  id: number
  method: string
  path: string
  status: number
  ms: number
  traceId: string
}

/** What a shop call returns: the response body plus what the console needs to find it. */
export interface ShopCallResult<T> {
  body: T | null
  status: number
  ms: number
  traceId: string
  path: string
  method: string
}

/**
 * The base path the shop's own API lives under.
 *
 * In production the page is served from `/apps/shop/` by the NebuLog host's reverse proxy, so a
 * root-relative `/api/...` would miss the proxy prefix entirely and hit the host instead. Deriving
 * it from the document's own location keeps one build working behind the proxy and in `vite dev`.
 */
export function apiBase(pathname: string = window.location.pathname): string {
  const marker = '/apps/shop'
  const index = pathname.indexOf(marker)
  return index === -1 ? '' : pathname.slice(0, index + marker.length)
}

/** The header the NebuLog host requires on cookie-authenticated state-changing requests. */
const CSRF_HEADER = 'X-NebuLog-Csrf'

/** Calls the shop API and measures what the console will need to correlate it. */
export async function callShop<T>(
  method: 'GET' | 'POST',
  path: string,
  body?: unknown,
): Promise<ShopCallResult<T>> {
  const url = `${apiBase()}${path}`
  const started = performance.now()

  const response = await fetch(url, {
    method,
    credentials: 'same-origin',
    headers: {
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
      // Sent on every request: the proxy sits behind the host's cookie session, and the host
      // rejects state-changing cookie requests that arrive without it.
      [CSRF_HEADER]: '1',
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  const ms = Math.round(performance.now() - started)
  const traceId = response.headers.get('X-Trace-Id') ?? ''

  let parsed: T | null = null
  try {
    parsed = (await response.json()) as T
  } catch {
    // A 204, or an error page that is not JSON. The status and trace are what matter here.
    parsed = null
  }

  return { body: parsed, status: response.status, ms, traceId, path, method }
}

/** Builds the path for one of the two equivalent implementations. */
export function apiPath(api: ShopApi, suffix: string): string {
  return `/api/${api}${suffix}`
}
