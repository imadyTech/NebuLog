import type {
  ApiKeyDto,
  ConnectedClientInfo,
  CreatedApiKeyDto,
  CurrentUser,
  LiveSummaryDto,
  NebuLogEntry,
  ServerInfo,
  StatSnapshot,
} from './contracts'

/**
 * The header the server requires on cookie-authenticated, state-changing requests.
 * A browser cannot attach a custom header cross-site without a CORS preflight, which is what
 * makes it a sufficient CSRF defence alongside the cookie's SameSite=Strict.
 */
const CSRF_HEADER = 'X-NebuLog-Csrf'

/** Raised for any non-2xx response, carrying the status so callers can branch on it. */
export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const method = init.method ?? 'GET'
  const headers = new Headers(init.headers)

  if (method !== 'GET' && method !== 'HEAD') {
    headers.set(CSRF_HEADER, '1')
    if (init.body !== undefined) {
      headers.set('Content-Type', 'application/json')
    }
  }

  const response = await fetch(path, {
    ...init,
    headers,
    credentials: 'same-origin',
  })

  if (!response.ok) {
    throw new ApiError(response.status, `${method} ${path} failed with ${response.status}`)
  }

  if (response.status === 204 || response.headers.get('Content-Length') === '0') {
    return undefined as T
  }

  const text = await response.text()
  return (text.length === 0 ? undefined : JSON.parse(text)) as T
}

export const api = {
  login: (email: string, password: string) =>
    request<CurrentUser>('/api/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),

  loginAsDemo: () => request<CurrentUser>('/api/auth/demo', { method: 'POST' }),

  logout: () => request<void>('/api/auth/logout', { method: 'POST' }),

  me: () => request<CurrentUser>('/api/auth/me'),

  summary: () => request<LiveSummaryDto>('/api/summary'),

  clients: () => request<ConnectedClientInfo[]>('/api/clients'),

  stats: () => request<StatSnapshot[]>('/api/custom-stats'),

  info: () => request<ServerInfo>('/api/info'),

  logs: (query: { afterId?: number; minSeverity?: number; service?: string; search?: string; limit?: number }) => {
    const search = new URLSearchParams()
    if (query.afterId !== undefined) search.set('afterId', String(query.afterId))
    if (query.minSeverity) search.set('minSeverity', String(query.minSeverity))
    if (query.service) search.set('service', query.service)
    if (query.search) search.set('search', query.search)
    if (query.limit !== undefined) search.set('limit', String(query.limit))

    const suffix = search.size > 0 ? `?${search}` : ''
    return request<NebuLogEntry[]>(`/api/logs${suffix}`)
  },

  listKeys: () => request<ApiKeyDto[]>('/api/keys'),

  createKey: (name: string, serviceName?: string) =>
    request<CreatedApiKeyDto>('/api/keys', {
      method: 'POST',
      body: JSON.stringify({ name, serviceName: serviceName || null }),
    }),

  revokeKey: (id: string) => request<void>(`/api/keys/${id}`, { method: 'DELETE' }),
}
