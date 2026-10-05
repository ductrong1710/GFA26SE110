import { mockAuthEnabled } from '../config/auth'
import { mockAccounts } from '../data/mock/accounts'
import { isHumanRole } from '../config/roles'

const sessionKey = 'smartfarm-mock-session'
const legacySessionKey = 'smartfarm-mock-user-id'

function toUser(account, activeRole = account.activeRole) {
  return {
    id: account.id,
    fullName: account.fullName,
    email: account.email,
    roles: [...account.roles],
    activeRole,
    isMock: true,
  }
}

export function getMockSession() {
  if (!mockAuthEnabled) return null
  try {
    const stored = window.sessionStorage.getItem(sessionKey)
    const session = stored ? JSON.parse(stored) : { id: Number(window.sessionStorage.getItem(legacySessionKey)) }
    const account = mockAccounts.find((item) => item.id === session?.id)
    if (!account) return null
    if (session.activeRole !== undefined && !isHumanRole(session.activeRole)) return null
    return toUser(account, isHumanRole(session.activeRole) ? session.activeRole : account.activeRole)
  } catch {
    return null
  }
}

export function loginWithMockAccount(email, password) {
  if (!mockAuthEnabled) {
    throw new Error('Đăng nhập thử nghiệm đang tắt. Hệ thống chưa kết nối đăng nhập backend.')
  }

  const account = mockAccounts.find((item) => item.email === email.trim().toLowerCase() && item.password === password)
  if (!account) throw new Error('Email hoặc mật khẩu không đúng.')

  saveMockActiveRole(account.id, account.activeRole)
  return toUser(account)
}

// DEMO ONLY: persist the preview role, never passwords, assigned roles, or permissions.
export function saveMockActiveRole(userId, activeRole) {
  if (!mockAuthEnabled || !isHumanRole(activeRole) || !mockAccounts.some(({ id }) => id === userId)) return
  try {
    window.sessionStorage.setItem(sessionKey, JSON.stringify({ id: userId, activeRole }))
    window.sessionStorage.removeItem(legacySessionKey)
  } catch { /* In-memory demo login/switching still works if storage is unavailable. */ }
}

export function clearMockSession() {
  for (const key of [sessionKey, legacySessionKey]) {
    try { window.sessionStorage.removeItem(key) } catch { /* The provider still clears its in-memory session. */ }
  }
}
