import { authStorage } from './authStorage.js'

export function createSession(storage, auth = authStorage, events = globalThis.window) {
  const key = 'smartfarm.api.session'
  let value = null
  let sessionId = null
  let savedText = null
  try {
    const saved = JSON.parse(storage?.getItem(key) ?? 'null')
    if (saved?.refreshToken) {
      const accessToken = saved.accessToken || auth.getToken()
      const user = saved.user || auth.getUser()
      if (accessToken && user) value = { accessToken, refreshToken: saved.refreshToken, user }
      sessionId = saved.sessionId || saved.refreshToken
    }
  } catch { /* Invalid or unavailable browser storage starts signed out. */ }
  if (value) {
    auth.setToken(value.accessToken)
    auth.setUser(value.user)
    savedText = JSON.stringify({ ...value, sessionId })
    try { storage?.setItem(key, savedText) } catch { /* Session remains usable in memory. */ }
  } else {
    auth.clearAuth()
    try { storage?.removeItem(key) } catch { /* Không dùng lại session đã hỏng. */ }
  }
  const listeners = new Set()
  let revision = 0
  let identity = 0
  function synchronize() {
    if (!storage) return
    try {
      const text = storage.getItem(key)
      if (text === savedText) return
      savedText = text
      const saved = JSON.parse(text ?? 'null')
      // A different login signs this tab out; it must never inherit another account's credentials.
      if (value && saved?.sessionId === sessionId && saved.user?.id === value.user.id && saved.accessToken && saved.refreshToken) {
        value = { accessToken: saved.accessToken, refreshToken: saved.refreshToken, user: saved.user }
      } else {
        value = null; sessionId = null; identity++
      }
      revision++
      listeners.forEach(listener => listener(value))
    } catch { /* Blocked storage leaves the in-memory session usable. */ }
  }
  const onStorage = event => { if ((!event.storageArea || event.storageArea === storage) && (event.key === key || event.key === null)) synchronize() }
  return {
    get() { synchronize(); return value },
    revision() { synchronize(); return revision },
    identity() { synchronize(); return identity },
    set(next, { preserveIdentity = false } = {}) {
      if (!preserveIdentity || !value || !next || value.user.id !== next.user.id) {
        identity++
        sessionId = next?.refreshToken || next?.accessToken || null
      }
      value = next; revision++
      if (next) {
        auth.setToken(next.accessToken)
        auth.setUser(next.user)
      } else auth.clearAuth()
      savedText = next ? JSON.stringify({ ...next, sessionId }) : null
      try {
        if (next) storage?.setItem(key, savedText)
        else storage?.removeItem(key)
      } catch { /* Session remains usable in memory. */ }
      listeners.forEach(listener => listener(value))
    },
    subscribe(listener) {
      if (!listeners.size) events?.addEventListener('storage', onStorage)
      listeners.add(listener)
      return () => {
        listeners.delete(listener)
        if (!listeners.size) events?.removeEventListener('storage', onStorage)
      }
    },
  }
}

let storage
try { storage = globalThis.localStorage } catch { /* Browser may deny storage. */ }
export const session = createSession(storage)
