import { useState, type FormEvent } from 'react'
import { useAuth } from '../../api/useAuth'
import { ApiError } from '../../api/client'
import styles from './LoginPage.module.css'

/** Sign-in, plus a one-click read-only demo session for visitors. */
export default function LoginPage() {
  const { signIn, signInAsDemo } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function run(action: () => Promise<void>) {
    setBusy(true)
    setError(null)
    try {
      await action()
    } catch (caught) {
      setError(describe(caught))
    } finally {
      setBusy(false)
    }
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault()
    void run(() => signIn(email, password))
  }

  return (
    <main className={styles.page}>
      <section className={styles.card}>
        <h1 className={styles.title}>NebuLog</h1>
        <p className={styles.tagline}>Real-time log dashboard for .NET, built on OpenTelemetry.</p>

        <form onSubmit={onSubmit} className={styles.form}>
          <label className={styles.label} htmlFor="email">
            Email
          </label>
          <input
            id="email"
            type="email"
            autoComplete="username"
            className={styles.input}
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            required
          />

          <label className={styles.label} htmlFor="password">
            Password
          </label>
          <input
            id="password"
            type="password"
            autoComplete="current-password"
            className={styles.input}
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            required
          />

          {error !== null && (
            <p className={styles.error} role="alert">
              {error}
            </p>
          )}

          <button type="submit" className={styles.primary} disabled={busy}>
            Sign in
          </button>
        </form>

        <div className={styles.divider}>or</div>

        <button type="button" className={styles.secondary} disabled={busy} onClick={() => void run(signInAsDemo)}>
          Continue as demo visitor
        </button>

        <p className={styles.note}>
          This is a technical demonstration project. The demo visitor account is read-only. Source and design notes
          are on{' '}
          <a href="https://github.com/imadyTech/NebuLog" target="_blank" rel="noreferrer">
            GitHub
          </a>
          .
        </p>
      </section>
    </main>
  )
}

function describe(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401) return 'That email and password did not match.'
    if (error.status === 423) return 'Too many failed attempts. Try again in a few minutes.'
    if (error.status === 404) return 'The demo account is not enabled on this server.'
    if (error.status === 429) return 'Too many sign-in attempts. Please wait a moment.'
  }

  return 'Could not sign in. Please try again.'
}
