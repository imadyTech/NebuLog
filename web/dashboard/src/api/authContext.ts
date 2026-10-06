import { createContext } from 'react'
import type { CurrentUser } from './contracts'

/** What {@link AuthProvider} exposes to the tree below it. */
export interface AuthState {
  user: CurrentUser | null
  loading: boolean
  signIn: (email: string, password: string) => Promise<void>
  signInAsDemo: () => Promise<void>
  signOut: () => Promise<void>
}

export const AuthContext = createContext<AuthState | null>(null)
