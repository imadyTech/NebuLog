import { describe, expect, it } from 'vitest'
import { hasRole } from './useAuth'
import { Roles, type CurrentUser } from './contracts'

function user(...roles: string[]): CurrentUser {
  return { email: 'someone@nebulog.local', roles }
}

describe('hasRole', () => {
  it('denies everything when nobody is signed in', () => {
    for (const role of Object.values(Roles)) {
      expect(hasRole(null, role)).toBe(false)
    }
  })

  it('grants the role a user actually holds', () => {
    expect(hasRole(user(Roles.viewer), Roles.viewer)).toBe(true)
    expect(hasRole(user(Roles.operator), Roles.operator)).toBe(true)
    expect(hasRole(user(Roles.admin), Roles.admin)).toBe(true)
  })

  it('treats the hierarchy the way the server policies do', () => {
    // Admin implies Operator implies Viewer.
    expect(hasRole(user(Roles.admin), Roles.operator)).toBe(true)
    expect(hasRole(user(Roles.admin), Roles.viewer)).toBe(true)
    expect(hasRole(user(Roles.operator), Roles.viewer)).toBe(true)
  })

  it('does not grant upwards', () => {
    expect(hasRole(user(Roles.viewer), Roles.operator)).toBe(false)
    expect(hasRole(user(Roles.viewer), Roles.admin)).toBe(false)
    expect(hasRole(user(Roles.operator), Roles.admin)).toBe(false)
  })

  it('does not let a producer key masquerade as a dashboard user', () => {
    expect(hasRole(user(Roles.producer), Roles.viewer)).toBe(false)
    expect(hasRole(user(Roles.producer), Roles.operator)).toBe(false)
    expect(hasRole(user(Roles.producer), Roles.admin)).toBe(false)
  })
})
