import { BrowserRouter, Navigate, NavLink, Outlet, Route, Routes, useLocation } from 'react-router'
import styles from './App.module.css'
import { AuthProvider } from './api/auth'
import { hasRole, useAuth } from './api/useAuth'
import { Roles } from './api/contracts'
import KeysPage from './features/keys/KeysPage'
import LivePage from './features/live/LivePage'
import LoginPage from './features/login/LoginPage'
import PerfHarness from './features/perf/PerfHarness'
import DemoPage from './site/DemoPage'
import LandingPage from './site/LandingPage'
import { safeReturnUrl } from './site/returnUrl'

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          {/* Development only: the performance harness from WO-0006 §2.5. Tree-shaken out of
              production builds, where import.meta.env.DEV is statically false. */}
          {import.meta.env.DEV && <Route path="/perf" element={<PerfHarness />} />}
          <Route path="/" element={<HomeRoute />} />
          <Route path="/login" element={<LoginRoute />} />
          <Route element={<ProtectedShell />}>
            <Route path="demo" element={<DemoPage />} />
            <Route path="dashboard" element={<LivePage />} />
            <Route path="keys" element={<AdminRoute />} />
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}

/**
 * The root is the public landing page, even for a signed-in visitor who typed the address — it is
 * the page the blog and the repository link to, and silently redirecting it away would break those
 * links. Signed-in visitors get a way through in the landing page's own navigation.
 */
function HomeRoute() {
  return <LandingPage />
}

/** Sends an already-signed-in visitor on to where they were heading. */
function LoginRoute() {
  const { user, loading } = useAuth()
  const location = useLocation()

  if (loading) return <Loading />
  if (user === null) return <LoginPage />

  const returnUrl = new URLSearchParams(location.search).get('returnUrl')
  return <Navigate to={safeReturnUrl(returnUrl) ?? '/demo'} replace />
}

/** Everything inside requires a session; without one the visitor is sent to sign in and back. */
function ProtectedShell() {
  const { user, loading, signOut } = useAuth()
  const location = useLocation()

  if (loading) return <Loading />
  if (user === null) {
    const returnUrl = encodeURIComponent(`${location.pathname}${location.search}`)
    return <Navigate to={`/login?returnUrl=${returnUrl}`} replace />
  }

  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <NavLink to="/" className={styles.brand}>
          NebuLog
        </NavLink>

        <nav className={styles.nav}>
          <NavLink to="/demo" className={({ isActive }) => (isActive ? styles.linkActive : styles.link)}>
            Guided demos
          </NavLink>
          <NavLink to="/dashboard" className={({ isActive }) => (isActive ? styles.linkActive : styles.link)}>
            Live console
          </NavLink>
          <a className={styles.link} href="/scalar/v1">
            API reference
          </a>
          <a className={styles.link} href="https://github.com/imadyTech/NebuLog">
            GitHub
          </a>
          {hasRole(user, Roles.admin) && (
            <NavLink to="/keys" className={({ isActive }) => (isActive ? styles.linkActive : styles.link)}>
              API keys
            </NavLink>
          )}
        </nav>

        <span className={styles.user}>
          {user.email}
          <span className={styles.roles}>{user.roles.join(', ')}</span>
        </span>

        <button type="button" className={styles.signOut} onClick={() => void signOut()}>
          Sign out
        </button>
      </header>

      <main className={styles.body}>
        <Outlet />
      </main>
    </div>
  )
}

/** Key management is for administrators; anyone else is sent back to the guided demos. */
function AdminRoute() {
  const { user } = useAuth()
  return hasRole(user, Roles.admin) ? <KeysPage /> : <Navigate to="/demo" replace />
}

function Loading() {
  return (
    <div className={styles.loading} role="status">
      Loading…
    </div>
  )
}
