import { use } from 'react'
import { AuthContext, type AuthState } from './authContext'
import { Roles, type CurrentUser } from './contracts'

/** The current session. Throws when used outside an {@link AuthProvider}. */
export function useAuth(): AuthState {
  const context = use(AuthContext)
  if (context === null) {
    throw new Error('useAuth must be used inside an AuthProvider.')
  }

  return context
}

/** True when the signed-in user holds the role, or a role that implies it. */
export function hasRole(user: CurrentUser | null, role: string): boolean {
  if (user === null) return false
  if (user.roles.includes(role)) return true

  // Admin implies Operator implies Viewer, matching the server's policies.
  if (role === Roles.viewer) return user.roles.includes(Roles.operator) || user.roles.includes(Roles.admin)
  if (role === Roles.operator) return user.roles.includes(Roles.admin)
  return false
}
