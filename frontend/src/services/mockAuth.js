import { mockAuthEnabled } from '../config/auth'
import { mockAccounts } from '../data/mock/accounts'
import { isHumanRole } from '../config/roles'

const sessionKey = 'smartfarm-mock-session'
const legacySessionKey = 'smartfarm-mock-user-id'

// DEMO ONLY: localStorage identifies a public fixture, not an authenticated identity.
// Replace this service and AuthProvider session handling with backend/JWT auth later.
// Neither the stored role nor frontend permission checks provide real security.
function removeLegacySession() {
  for (const key of [sessionKey, legacySessionKey]) {
    try { window.sessionStorage.removeItem(key) } catch { /* Storage may be unavailable. */ }
  }
}

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
    const persistent = window.localStorage.getItem(sessionKey)
    // Migrate previous tab-scoped demos once, without overriding a persistent session.
    const stored = persistent ?? window.sessionStorage.getItem(sessionKey)
    const session = stored !== null ? JSON.parse(stored) : { id: Number(window.sessionStorage.getItem(legacySessionKey)) }
    const account = mockAccounts.find((item) => item.id === session?.id)
    if (!account) return null
    if (session.activeRole !== undefined && !isHumanRole(session.activeRole)) return null
    const user = toUser(account, isHumanRole(session.activeRole) ? session.activeRole : account.activeRole)
    saveMockActiveRole(user.id, user.activeRole)
    return user
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
    window.localStorage.setItem(sessionKey, JSON.stringify({ id: userId, activeRole }))
    removeLegacySession()
  } catch { /* In-memory demo login/switching still works if storage is unavailable. */ }
}

export function clearMockSession() {
  for (const key of [sessionKey, legacySessionKey]) {
    try { window.localStorage.removeItem(key) } catch { /* The provider still clears its in-memory session. */ }
  }
  removeLegacySession()
}
