function createAuthStorage(storage) {
  let token = null
  let user = null

  return {
    getToken() {
      if (!storage) return token
      try { return storage.getItem('smartfarm.access_token') } catch { return token }
    },
    setToken(value) {
      token = value || null
      try {
        if (token) storage?.setItem('smartfarm.access_token', token)
        else storage?.removeItem('smartfarm.access_token')
      } catch { /* Vẫn giữ dữ liệu trong bộ nhớ nếu storage bị chặn. */ }
    },
    getUser() {
      if (!storage) return user
      let saved
      try {
        saved = storage.getItem('smartfarm.user_info')
      } catch { return user }
      if (saved === null) return null
      try { return JSON.parse(saved) } catch { return null }
    },
    setUser(value) {
      user = value ?? null
      try {
        if (user) storage?.setItem('smartfarm.user_info', JSON.stringify(user))
        else storage?.removeItem('smartfarm.user_info')
      } catch { /* Vẫn giữ dữ liệu trong bộ nhớ nếu storage bị chặn. */ }
    },
    clearAuth() {
      token = null
      user = null
      try { storage?.removeItem('smartfarm.access_token') } catch { /* Bỏ qua lỗi storage của trình duyệt. */ }
      try { storage?.removeItem('smartfarm.user_info') } catch { /* Bỏ qua lỗi storage của trình duyệt. */ }
    },
  }
}

let storage
try { storage = globalThis.localStorage } catch { /* Trình duyệt có thể chặn localStorage. */ }

export const authStorage = createAuthStorage(storage)
export const { getToken, setToken, getUser, setUser, clearAuth } = authStorage

export { createAuthStorage }
