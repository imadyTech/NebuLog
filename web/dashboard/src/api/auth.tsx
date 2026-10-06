import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { ApiError, api } from './client'
import { type CurrentUser } from './contracts'
import { AuthContext, type AuthState } from './authContext'

/** Loads the current session once at start-up and exposes sign-in and sign-out. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let cancelled = false

    api
      .me()
      .then((current) => {
        if (!cancelled) setUser(current)
      })
      .catch((error: unknown) => {
        // A 401 simply means nobody is signed in yet.
        if (!(error instanceof ApiError) || error.status !== 401) {
          console.error('Could not read the current session', error)
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [])

  const signIn = useCallback(async (email: string, password: string) => {
    setUser(await api.login(email, password))
  }, [])

  const signInAsDemo = useCallback(async () => {
    setUser(await api.loginAsDemo())
  }, [])

  const signOut = useCallback(async () => {
    await api.logout()
    setUser(null)
  }, [])

  const value = useMemo<AuthState>(
    () => ({ user, loading, signIn, signInAsDemo, signOut }),
    [user, loading, signIn, signInAsDemo, signOut],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}
