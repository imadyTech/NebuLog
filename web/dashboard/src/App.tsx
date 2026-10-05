import { BrowserRouter, Navigate, NavLink, Outlet, Route, Routes } from 'react-router'
import styles from './App.module.css'
import { AuthProvider } from './api/auth'
import { hasRole, useAuth } from './api/useAuth'
import { Roles } from './api/contracts'
import KeysPage from './features/keys/KeysPage'
import LivePage from './features/live/LivePage'
import LoginPage from './features/login/LoginPage'
import PerfHarness from './features/perf/PerfHarness'

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          {/* Development only: the performance harness from WO-0006 §2.5. Tree-shaken out of
              production builds, where import.meta.env.DEV is statically false. */}
          {import.meta.env.DEV && <Route path="/perf" element={<PerfHarness />} />}
          <Route path="/login" element={<LoginRoute />} />
          <Route element={<ProtectedShell />}>
            <Route index element={<LivePage />} />
            <Route path="keys" element={<AdminRoute />} />
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  )
}

/** Sends an already-signed-in visitor straight to the dashboard. */
function LoginRoute() {
  const { user, loading } = useAuth()

  if (loading) return <Loading />
  return user === null ? <LoginPage /> : <Navigate to="/" replace />
}

/** Everything inside requires a session; without one the visitor lands on the login page. */
function ProtectedShell() {
  const { user, loading, signOut } = useAuth()

  if (loading) return <Loading />
  if (user === null) return <Navigate to="/login" replace />

  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <span className={styles.brand}>NebuLog</span>

        <nav className={styles.nav}>
          <NavLink to="/" end className={({ isActive }) => (isActive ? styles.linkActive : styles.link)}>
            Live
          </NavLink>
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

/** Key management is for administrators; anyone else is sent back to the dashboard. */
function AdminRoute() {
  const { user } = useAuth()
  return hasRole(user, Roles.admin) ? <KeysPage /> : <Navigate to="/" replace />
}

function Loading() {
  return (
    <div className={styles.loading} role="status">
      Loading…
    </div>
  )
}
