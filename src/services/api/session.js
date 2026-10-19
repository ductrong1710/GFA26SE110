import { authStorage } from './authStorage.js'

export function createSession(storage, auth = authStorage) {
  const key = 'smartfarm.api.session'
  let value = null
  try {
    const saved = JSON.parse(storage?.getItem(key) ?? 'null')
    if (saved?.refreshToken) {
      const accessToken = auth.getToken() || saved.accessToken
      const user = auth.getUser() || saved.user
      if (accessToken && user) value = { accessToken, refreshToken: saved.refreshToken, user }
    }
  } catch { /* Invalid or unavailable browser storage starts signed out. */ }
  if (value) {
    auth.setToken(value.accessToken)
    auth.setUser(value.user)
    try { storage?.setItem(key, JSON.stringify({ refreshToken: value.refreshToken })) } catch { /* Session remains usable in memory. */ }
  } else {
    auth.clearAuth()
    try { storage?.removeItem(key) } catch { /* Không dùng lại session đã hỏng. */ }
  }
  const listeners = new Set()
  let revision = 0
  let identity = 0
  return {
    get: () => value,
    revision: () => revision,
    identity: () => identity,
    set(next, { preserveIdentity = false } = {}) {
      if (!preserveIdentity || !value || !next) identity++
      value = next; revision++
      try {
        if (next) storage?.setItem(key, JSON.stringify({ refreshToken: next.refreshToken }))
        else storage?.removeItem(key)
      } catch { /* Session remains usable in memory. */ }
      if (next) {
        auth.setToken(next.accessToken)
        auth.setUser(next.user)
      } else auth.clearAuth()
      listeners.forEach(listener => listener(value))
    },
    subscribe(listener) { listeners.add(listener); return () => listeners.delete(listener) },
  }
}

let storage
try { storage = globalThis.localStorage } catch { /* Browser may deny storage. */ }
export const session = createSession(storage)
