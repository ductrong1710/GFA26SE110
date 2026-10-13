import { useEffect, useState } from 'react'
import { AuthContext } from './AuthContext'
import { mockAuthEnabled } from '../config/auth'
import { getActiveRole, isHumanRole, ROLE_LABELS } from '../config/roles'
import { getAlertScope, hasAnyPermission, hasPermission } from '../config/permissions'
import { clearMockSession, getMockSession, loginWithMockAccount, saveMockActiveRole } from '../services/mockAuth'
import { api } from '../services/api'
import { session } from '../services/api/session'
import { normalizeUser } from '../services/api/roles'
import { hasBackendPermission } from '../config/backendPermissions'

export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => mockAuthEnabled ? getMockSession() : null)
  const [restoring, setRestoring] = useState(() => !mockAuthEnabled && Boolean(session.get()))
  useEffect(() => {
    if (mockAuthEnabled) return
    let mounted = true
    const identity = session.identity()
    const unsubscribe = session.subscribe(value => {
      if (mounted) setUser(previous => value ? normalizeUser(value.user, previous?.id === value.user.id ? previous.activeRole : null) : null)
    })
    api.restore().then(value => { if (mounted && session.identity() === identity) setUser(value ? normalizeUser(value) : null) })
      .catch(() => { if (mounted && session.identity() === identity) { session.set(null); setUser(null) } })
      .finally(() => { if (mounted) setRestoring(false) })
    return () => { mounted = false; unsubscribe() }
  }, [])
  const demoRoleSwitchEnabled = mockAuthEnabled && user?.isMock === true
  const activeRole = getActiveRole(user, { allowDemoRole: demoRoleSwitchEnabled })

  const login = async (email, password) => {
    const nextUser = mockAuthEnabled ? loginWithMockAccount(email, password) : normalizeUser(await api.login(email, password))
    setUser(nextUser)
    return nextUser
  }

  const logout = async () => {
    if (!user?.isMock) await api.logout()
    clearMockSession()
    setUser(null)
  }

  // Normal multi-role switching can only activate an assigned role.
  const switchRole = (role) => {
    if (!user || !isHumanRole(role) || !user.roles.includes(role)) return false
    if (user.isMock) saveMockActiveRole(user.id, role)
    setUser({ ...user, activeRole: role })
    return true
  }

  // DEMO ONLY: never use this override with a real authenticated user.
  const switchDemoRole = (role) => {
    if (!demoRoleSwitchEnabled || !isHumanRole(role)) return false
    saveMockActiveRole(user.id, role)
    setUser({ ...user, activeRole: role })
    return true
  }

  return <AuthContext.Provider value={{
    user,
    isAuthenticated: Boolean(user),
    restoring,
    activeRole,
    activeRoleLabel: ROLE_LABELS[activeRole] ?? 'Unknown role',
    can: (permission) => user?.isMock ? hasPermission(activeRole, permission) : hasBackendPermission(activeRole, permission),
    canAny: (permissions) => user?.isMock ? hasAnyPermission(activeRole, permissions) : permissions.some(permission => hasBackendPermission(activeRole, permission)),
    alertScope: getAlertScope(activeRole),
    login, logout, switchRole, switchDemoRole, demoRoleSwitchEnabled,
  }}>{children}</AuthContext.Provider>
}
