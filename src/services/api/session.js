export function createSession(storage) {
  const key = 'smartfarm.api.session'
  let value = null
  try {
    const saved = JSON.parse(storage?.getItem(key) ?? 'null')
    if (saved?.accessToken && saved?.refreshToken && saved?.user) value = saved
  } catch { /* Invalid or unavailable browser storage starts signed out. */ }
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
      try { if (next) storage?.setItem(key, JSON.stringify(next)); else storage?.removeItem(key) } catch { /* Session remains usable in memory. */ }
      listeners.forEach(listener => listener(value))
    },
    subscribe(listener) { listeners.add(listener); return () => listeners.delete(listener) },
  }
}

let storage
try { storage = globalThis.sessionStorage } catch { /* Browser may deny storage. */ }
export const session = createSession(storage)
