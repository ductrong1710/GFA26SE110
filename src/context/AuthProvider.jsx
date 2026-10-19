import { useEffect, useState } from 'react'
import { AuthContext } from './AuthContext'
import { getActiveRole, isHumanRole, ROLE_LABELS } from '../config/roles'
import { getAlertScope } from '../config/permissions'
import { api } from '../services/api'
import { session } from '../services/api/session'
import { normalizeUser } from '../services/api/roles'
import { hasBackendPermission } from '../config/backendPermissions'

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [restoring, setRestoring] = useState(() => Boolean(session.get()))
  useEffect(() => {
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
  const activeRole = getActiveRole(user)

  const login = async (email, password) => {
    const nextUser = normalizeUser(await api.login({ email, password }))
    setUser(nextUser)
    return nextUser
  }

  const logout = async () => {
    await api.logout()
    setUser(null)
  }

  // Normal multi-role switching can only activate an assigned role.
  const switchRole = (role) => {
    if (!user || !isHumanRole(role) || !user.roles.includes(role)) return false
    setUser({ ...user, activeRole: role })
    return true
  }

  return <AuthContext.Provider value={{
    user,
    isAuthenticated: Boolean(user),
    restoring,
    activeRole,
    activeRoleLabel: ROLE_LABELS[activeRole] ?? 'Unknown role',
    can: permission => hasBackendPermission(activeRole, permission),
    canAny: permissions => permissions.some(permission => hasBackendPermission(activeRole, permission)),
    alertScope: getAlertScope(activeRole),
    login, logout, switchRole,
  }}>{children}</AuthContext.Provider>
}
