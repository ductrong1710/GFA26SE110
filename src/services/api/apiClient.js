import axios, { AxiosError } from 'axios'
import { authStorage } from './authStorage.js'
import { showToast } from '../../utils/toast.js'

export class ApiError extends Error {
  constructor(message, status = 0, errors = []) { super(message); this.name = 'ApiError'; this.status = status; this.errors = errors }
}

function createFetchAdapter(fetcher) {
  return async config => {
    const controller = new AbortController()
    let timedOut = false
    const abortFromSignal = () => controller.abort()
    if (config.signal?.aborted) controller.abort()
    else config.signal?.addEventListener('abort', abortFromSignal, { once: true })
    const timeoutId = config.timeout > 0 ? setTimeout(() => {
      timedOut = true
      controller.abort()
    }, config.timeout) : null
    const cleanup = () => {
      if (timeoutId !== null) clearTimeout(timeoutId)
      config.signal?.removeEventListener('abort', abortFromSignal)
    }

    try {
      const response = await fetcher(axios.getUri(config), {
        method: config.method.toUpperCase(),
        headers: config.headers.toJSON(),
        body: config.data,
        signal: controller.signal,
      })
      const data = response.status === 204 ? null : await response.json()
      const result = { data, status: response.status, statusText: response.statusText, headers: {}, config, request: null }
      if (!config.validateStatus || config.validateStatus(response.status)) return result
      throw new AxiosError(`Request failed with status code ${response.status}`, undefined, config, null, result)
    } catch (error) {
      if (error instanceof AxiosError) throw error
      if (timedOut) throw new AxiosError('timeout exceeded', AxiosError.ECONNABORTED, config)
      if (controller.signal.aborted) throw new axios.CanceledError('canceled', config)
      throw AxiosError.from(error, AxiosError.ERR_NETWORK, config)
    } finally {
      cleanup()
    }
  }
}

export function createApiClient({ baseUrl, session, fetch: fetcher, timeout = 10_000 }) {
  let refreshing = null
  let restoring = null
  let closing = false
  let signingOut = null
  const expired = () => new ApiError('Your session has expired. Please sign in again.', 401)
  const client = axios.create({
    baseURL: baseUrl,
    timeout,
    headers: { Accept: 'application/json' },
    ...(fetcher ? { adapter: createFetchAdapter(fetcher) } : {}),
  })

  client.interceptors.request.use(config => {
    const before = session.get()
    config._sessionIdentity = session.identity()
    config._sessionRevision = session.revision()
    config._sessionBefore = before
    const token = authStorage.getToken() || before?.accessToken
    if (config._auth !== false && token) config.headers.set('Authorization', `Bearer ${token}`)
    if (config._bearer) config.headers.set('Authorization', `Bearer ${config._bearer}`)
    return config
  })

  client.interceptors.response.use(response => {
    const { config, data, status } = response
    if (config._auth !== false && session.identity() !== config._sessionIdentity) throw expired()
    if (status === 204) {
      if (config._toastEnabled) showToast('success', config._toastMessage)
      return null
    }
    if (data?.success === false) {
      const error = new ApiError(data.message || 'The request could not be completed.', status, data.errors ?? [])
      if (config._toastEnabled) showToast('error', error.message)
      throw error
    }
    if (config._toastEnabled) showToast('success', config._toastMessage)
    return data?.pagination ? { items: data.data ?? [], pagination: data.pagination } : data?.data
  }, async error => {
    const config = error.config
    if (axios.isCancel(error)) throw new DOMException('The operation was aborted.', 'AbortError')
    if (!error.response) {
      const requestError = ['ECONNABORTED', 'ETIMEDOUT'].includes(error.code)
        ? new ApiError('The request timed out. Please try again.')
        : new ApiError('Unable to reach the server. Check your connection and try again.')
      if (config?._toastEnabled) showToast('error', requestError.message)
      throw requestError
    }

    const { status, data: payload } = error.response
    if (status === 401 && config?._auth !== false && config._retryCount === 0 && config._sessionBefore?.refreshToken && !closing) {
      const current = session.get()
      if (!current || current.user.id !== config._sessionBefore.user.id || session.identity() !== config._sessionIdentity) throw expired()
      config._retryCount = 1
      if (current.accessToken === config._sessionBefore.accessToken) await refresh()
      return client.request(config)
    }
    if (status === 401 && config?._auth !== false && !closing &&
        session.identity() === config._sessionIdentity && session.revision() === config._sessionRevision) {
      authStorage.clearAuth()
      session.set(null)
    }

    const validation = [400, 422].includes(status) && Array.isArray(payload?.errors)
      ? [...new Set(payload.errors.map(item => item?.message).filter(message => typeof message === 'string' && message.trim()))].join(' ')
      : ''
    const message = status >= 500 ? 'The server could not complete the request. Please try again.'
      : validation || payload?.message || ({ 401: 'Please sign in again.', 403: 'You do not have access to this resource.', 404: 'This resource was not found.', 409: 'This change conflicts with the current record.' }[status]) || 'The request could not be completed.'
    if (config?._toastEnabled) showToast('error', message)
    throw new ApiError(message, status, payload?.errors ?? [])
  })

  async function send(path, { method = 'GET', body, query, signal, auth = true, retry = true, bearer, toast = !['GET', 'HEAD'].includes(method.toUpperCase()) && auth, toastMessage = 'Thao tác đã hoàn tất.' } = {}) {
    const params = Object.fromEntries(Object.entries(query ?? {}).filter(([, value]) => value !== undefined && value !== null && value !== ''))
    return client.request({
      url: path,
      method,
      data: body,
      params,
      signal,
      _auth: auth,
      _retryCount: retry ? 0 : 1,
      _bearer: bearer,
      _toastEnabled: toast,
      _toastMessage: toastMessage,
    })
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
    async login(credentials) {
      const revision = session.revision()
      const result = await send('/api/auth/login', { method: 'POST', body: credentials, auth: false, toast: false })
      if (revision !== session.revision() || closing) throw expired()
      session.set(result)
      showToast('success', 'Đăng nhập thành công.')
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
          const revoke = value => send('/api/auth/logout', { method: 'POST', body: { refreshToken: value.refreshToken }, bearer: value.accessToken, auth: false, retry: false, toast: false })
          try { await revoke(tokens) }
          catch (error) {
            if (error.status !== 401) throw error
            tokens = await send('/api/auth/refresh', { method: 'POST', body: { refreshToken: tokens.refreshToken }, auth: false, retry: false })
            await revoke(tokens)
          }
        } catch { /* Signing out always clears the local session, including offline. */ }
        finally { authStorage.clearAuth(); session.set(null); closing = false; showToast('success', 'Đã đăng xuất.') }
      })().finally(() => { signingOut = null })
      return signingOut
    },
  }
}
