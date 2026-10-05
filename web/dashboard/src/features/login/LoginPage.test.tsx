import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from '../../api/auth'
import { useAuth } from '../../api/useAuth'
import type { CurrentUser } from '../../api/contracts'
import LoginPage from './LoginPage'

const viewer: CurrentUser = { email: 'viewer@nebulog.local', roles: ['Viewer'] }

/** Minimal fetch double: maps `METHOD /path` to a status and body. */
function mockFetch(routes: Record<string, { status: number; body?: unknown }>) {
  const calls: { url: string; init: RequestInit }[] = []

  const fetchMock = vi.fn(async (input: RequestInfo | URL, init: RequestInit = {}) => {
    const url = String(input)
    calls.push({ url, init })

    const route = routes[`${init.method ?? 'GET'} ${url}`] ?? { status: 404 }
    const body = route.body === undefined ? '' : JSON.stringify(route.body)

    return new Response(body, {
      status: route.status,
      headers: { 'Content-Type': 'application/json' },
    })
  })

  vi.stubGlobal('fetch', fetchMock)
  return calls
}

function Harness() {
  const { user } = useAuth()
  return user === null ? <LoginPage /> : <p>Signed in as {user.email}</p>
}

function renderLogin() {
  return render(
    <AuthProvider>
      <Harness />
    </AuthProvider>,
  )
}

describe('LoginPage', () => {
  beforeEach(() => {
    // No existing session: /api/auth/me answers 401.
    mockFetch({ 'GET /api/auth/me': { status: 401 } })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('signs in with email and password', async () => {
    const calls = mockFetch({
      'GET /api/auth/me': { status: 401 },
      'POST /api/auth/login': { status: 200, body: viewer },
    })

    renderLogin()
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText('Email'), 'viewer@nebulog.local')
    await user.type(screen.getByLabelText('Password'), 'User!Password#1')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Signed in as viewer@nebulog.local')).toBeInTheDocument()

    // The CSRF header the server requires must be present on the POST.
    const login = calls.find((call) => call.url === '/api/auth/login')
    expect(login).toBeDefined()
    expect(new Headers(login!.init.headers).get('X-NebuLog-Csrf')).toBe('1')
    expect(login!.init.credentials).toBe('same-origin')
  })

  it('reports a rejected password without signing in', async () => {
    mockFetch({
      'GET /api/auth/me': { status: 401 },
      'POST /api/auth/login': { status: 401 },
    })

    renderLogin()
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText('Email'), 'viewer@nebulog.local')
    await user.type(screen.getByLabelText('Password'), 'wrong')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('did not match')
  })

  it('explains a lockout', async () => {
    mockFetch({
      'GET /api/auth/me': { status: 401 },
      'POST /api/auth/login': { status: 423 },
    })

    renderLogin()
    const user = userEvent.setup()

    await user.type(await screen.findByLabelText('Email'), 'viewer@nebulog.local')
    await user.type(screen.getByLabelText('Password'), 'wrong')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Too many failed attempts')
  })

  it('signs in as the demo visitor', async () => {
    mockFetch({
      'GET /api/auth/me': { status: 401 },
      'POST /api/auth/demo': { status: 200, body: { email: 'demo@nebulog.local', roles: ['Viewer'] } },
    })

    renderLogin()
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Continue as demo visitor' }))

    expect(await screen.findByText('Signed in as demo@nebulog.local')).toBeInTheDocument()
  })

  it('says so when the demo account is disabled', async () => {
    mockFetch({
      'GET /api/auth/me': { status: 401 },
      'POST /api/auth/demo': { status: 404 },
    })

    renderLogin()
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Continue as demo visitor' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('not enabled')
  })

  it('restores an existing session without showing the form', async () => {
    mockFetch({ 'GET /api/auth/me': { status: 200, body: viewer } })

    renderLogin()

    await waitFor(() => {
      expect(screen.getByText('Signed in as viewer@nebulog.local')).toBeInTheDocument()
    })
  })
})
