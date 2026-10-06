import { useState, type FormEvent } from 'react'
import { Link, useSearchParams } from 'react-router'
import { useAuth } from '../../api/useAuth'
import { ApiError } from '../../api/client'
import styles from './LoginPage.module.css'

/**
 * Sign-in. The demo visitor is the primary path and staff sign-in the secondary one, because
 * almost everyone arriving here came from the landing page to look, not to administer anything.
 */
export default function LoginPage() {
  const { signIn, signInAsDemo } = useAuth()
  const [params] = useSearchParams()
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

  const returnUrl = params.get('returnUrl')

  return (
    <main className={styles.page}>
      <header className={styles.topBar}>
        <Link to="/" className={styles.brandLink}>
          NebuLog
        </Link>
        <Link to="/" className={styles.back}>
          Back to overview
        </Link>
      </header>

      <div className={styles.columns}>
        <section className={styles.demoCard}>
          <p className={styles.eyebrow}>Recommended</p>
          <h1 className={styles.demoTitle}>Continue as demo visitor</h1>
          <ul className={styles.points}>
            <li>Read-only: watch, filter and inspect every entry.</li>
            <li>Run guided scenarios that generate real traffic.</li>
            <li>Synthetic data only, cleaned up periodically.</li>
          </ul>
          <button type="button" className={styles.demoButton} disabled={busy} onClick={() => void run(signInAsDemo)}>
            Continue as demo visitor
          </button>
          {returnUrl ? <p className={styles.returnNote}>You will be taken back to where you were.</p> : null}
        </section>

        <section className={styles.staffCard}>
          <h2 className={styles.staffTitle}>Staff sign-in</h2>
          <p className={styles.staffLead}>
            Operators can send commands to producers; administrators manage API keys.
          </p>

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

            {error ? (
              <p className={styles.error} role="alert">
                {error}
              </p>
            ) : null}

            <button type="submit" className={styles.submit} disabled={busy}>
              {busy ? 'Signing in…' : 'Sign in'}
            </button>
          </form>

          <p className={styles.note}>
            Accounts are seeded from configuration. There is no self-registration.
          </p>
        </section>
      </div>

      <footer className={styles.footer}>
        <a href="/#how">How it works</a>
        <a href="https://github.com/imadyTech/NebuLog">GitHub</a>
        <a href="/scalar/v1">API reference</a>
      </footer>
    </main>
  )
}

function describe(caught: unknown): string {
  if (caught instanceof ApiError) {
    if (caught.status === 401) return 'That email and password did not match.'
    if (caught.status === 423) return 'Too many failed attempts. Try again in a few minutes.'
    if (caught.status === 404) return 'The demo account is not enabled on this server.'
    if (caught.status === 429) return 'Too many sign-in attempts. Please wait a moment.'
  }

  return 'Could not sign in. Please try again.'
}
