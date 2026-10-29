import { authStorage } from './authStorage.js'

export function createSession(storage, auth = authStorage, events = globalThis.window) {
  const key = 'smartfarm.api.session'
  let value = null
  let sessionId = null
  let savedText = null
  let saved
  try {
    saved = JSON.parse(storage?.getItem(key) ?? 'null')
    if (saved?.refreshToken) {
      const accessToken = auth.getToken() || saved.accessToken
      const user = auth.getUser() || saved.user
      if (accessToken && user && (!saved.userId || saved.userId === user.id)) {
        value = { accessToken, refreshToken: saved.refreshToken, user }
        sessionId = saved.sessionId || saved.refreshToken
      }
    } else auth.clearAuth()
  } catch { /* Invalid or unavailable browser storage starts signed out. */ }
  if (value) {
    auth.setToken(value.accessToken)
    auth.setUser(value.user)
    savedText = JSON.stringify({ refreshToken: value.refreshToken, sessionId, userId: value.user.id })
    try { storage?.setItem(key, savedText) } catch { /* Session remains usable in memory. */ }
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
      const accessToken = auth.getToken()
      const user = auth.getUser()
      if (value && saved?.sessionId === sessionId && saved.userId === value.user.id &&
          user?.id === value.user.id && accessToken && saved.refreshToken) {
        value = { accessToken, refreshToken: saved.refreshToken, user }
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
      savedText = next ? JSON.stringify({ refreshToken: next.refreshToken, sessionId, userId: next.user.id }) : null
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
