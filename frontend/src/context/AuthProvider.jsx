import { useState } from 'react'
import { AuthContext } from './AuthContext'
import { mockAuthEnabled } from '../config/auth'
import { getActiveRole, isHumanRole, ROLE_LABELS } from '../config/roles'
import { getAlertScope, hasAnyPermission, hasPermission } from '../config/permissions'
import { clearMockSession, getMockSession, loginWithMockAccount, saveMockActiveRole } from '../services/mockAuth'

export function AuthProvider({ children }) {
  const [user, setUser] = useState(getMockSession)
  const demoRoleSwitchEnabled = mockAuthEnabled && user?.isMock === true
  const activeRole = getActiveRole(user, { allowDemoRole: demoRoleSwitchEnabled })

  const login = (email, password) => {
    const nextUser = loginWithMockAccount(email, password)
    setUser(nextUser)
    return nextUser
  }

  const logout = () => {
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
    activeRole,
    activeRoleLabel: ROLE_LABELS[activeRole] ?? 'Unknown role',
    can: (permission) => hasPermission(activeRole, permission),
    canAny: (permissions) => hasAnyPermission(activeRole, permissions),
    alertScope: getAlertScope(activeRole),
    login, logout, switchRole, switchDemoRole, demoRoleSwitchEnabled,
  }}>{children}</AuthContext.Provider>
}
