export class ApiError extends Error {
  constructor(message, status = 0, errors = []) { super(message); this.name = 'ApiError'; this.status = status; this.errors = errors }
}

export function createApiClient({ baseUrl, session, fetch: fetcher = globalThis.fetch }) {
  let refreshing = null
  let restoring = null
  let closing = false
  let signingOut = null
  const expired = () => new ApiError('Your session has expired. Please sign in again.', 401)
  async function send(path, { method = 'GET', body, query, signal, auth = true, retry = true, bearer } = {}) {
    const before = session.get()
    const revision = session.revision()
    const identity = session.identity()
    const url = new URL(baseUrl.replace(/\/$/, '') + path)
    Object.entries(query ?? {}).forEach(([key, value]) => { if (value !== undefined && value !== null && value !== '') url.searchParams.set(key, String(value)) })
    const headers = { Accept: 'application/json' }
    if (body !== undefined) headers['Content-Type'] = 'application/json'
    if (auth && before?.accessToken) headers.Authorization = `Bearer ${before.accessToken}`
    if (bearer) headers.Authorization = `Bearer ${bearer}`
    let response
    try { response = await fetcher(url.toString(), { method, headers, body: body === undefined ? undefined : JSON.stringify(body), signal }) }
    catch (error) { if (error.name === 'AbortError') throw error; throw new ApiError('Unable to reach the server. Check your connection and try again.') }
    if (auth && session.identity() !== identity) throw expired()
    if (response.status === 401 && auth && retry && before?.refreshToken && !closing) {
      const current = session.get()
      if (!current || current.user.id !== before.user.id) throw expired()
      // A slower 401 may arrive after another request already rotated the tokens.
      if (current.accessToken === before.accessToken) await refresh()
      return send(path, { method, body, query, signal, auth, retry: false })
    }
    if (response.status === 401 && auth && !retry && session.revision() === revision && !closing) session.set(null)
    if (response.status === 204) return null
    let payload
    try { payload = await response.json() } catch { throw new ApiError('The server returned an unreadable response. Please try again.', response.status) }
    if (auth && session.identity() !== identity) throw expired()
    if (!response.ok || payload.success === false) {
      const validation = [400, 422].includes(response.status) && Array.isArray(payload.errors)
        ? [...new Set(payload.errors.map(error => error?.message).filter(message => typeof message === 'string' && message.trim()))].join(' ')
        : ''
      const message = response.status >= 500 ? 'The server could not complete the request. Please try again.'
        : validation || payload.message || ({ 401: 'Please sign in again.', 403: 'You do not have access to this resource.', 404: 'This resource was not found.', 409: 'This change conflicts with the current record.' }[response.status]) || 'The request could not be completed.'
      throw new ApiError(message, response.status, payload.errors ?? [])
    }
    return payload.pagination ? { items: payload.data ?? [], pagination: payload.pagination } : payload.data
  }
  async function refresh() {
    const identity = session.identity()
    if (refreshing?.identity === identity) return refreshing.promise
    const before = session.get()
    if (!before?.refreshToken || closing) throw expired()
    const operation = { identity }
    operation.promise = (async () => {
      try {
        const result = await send('/api/auth/refresh', { method: 'POST', body: { refreshToken: before.refreshToken }, auth: false, retry: false })
        if (session.identity() !== identity || session.get()?.accessToken !== before.accessToken) throw expired()
        // A sign-out in progress consumes rotated tokens only to revoke them.
        if (!closing) session.set(result, { preserveIdentity: true })
        return result
      } catch (error) { if (session.identity() === identity && session.get()?.accessToken === before.accessToken) session.set(null); throw error }
      finally { if (refreshing === operation) refreshing = null }
    })()
    refreshing = operation
    return operation.promise
  }
  return {
    request: send,
    get: (path, query, signal) => send(path, { query, signal }),
    post: (path, body) => send(path, { method: 'POST', body }),
    put: (path, body) => send(path, { method: 'PUT', body }),
    patch: (path, body) => send(path, { method: 'PATCH', body }),
    refresh,
    async login(email, password) {
      const revision = session.revision()
      const result = await send('/api/auth/login', { method: 'POST', body: { email, password }, auth: false })
      if (revision !== session.revision() || closing) throw expired()
      session.set(result)
      return result.user
    },
    async restore() {
      if (!session.get()) return null
      const identity = session.identity()
      if (restoring?.identity === identity) return restoring.promise
      const operation = { identity }
      operation.promise = (async () => {
        try {
          const user = await send('/api/auth/me')
          if (!session.get() || session.identity() !== identity) throw expired()
          session.set({ ...session.get(), user }, { preserveIdentity: true })
          return user
        } finally { if (restoring === operation) restoring = null }
      })()
      restoring = operation
      return operation.promise
    },
    logout() {
      if (signingOut) return signingOut
      closing = true
      const before = session.get()
      signingOut = (async () => {
        try {
          let tokens = refreshing?.identity === session.identity() ? await refreshing.promise : before
          if (!tokens?.refreshToken) return
          const revoke = value => send('/api/auth/logout', { method: 'POST', body: { refreshToken: value.refreshToken }, bearer: value.accessToken, auth: false, retry: false })
          try { await revoke(tokens) }
          catch (error) {
            if (error.status !== 401) throw error
            tokens = await send('/api/auth/refresh', { method: 'POST', body: { refreshToken: tokens.refreshToken }, auth: false, retry: false })
            await revoke(tokens)
          }
        } catch { /* Signing out always clears the local session, including offline. */ }
        finally { session.set(null); closing = false }
      })().finally(() => { signingOut = null })
      return signingOut
    },
  }
}
