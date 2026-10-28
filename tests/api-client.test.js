import test from 'node:test'
import assert from 'node:assert/strict'
import { createApiClient } from '../src/services/api/apiClient.js'
import { authStorage, createAuthStorage } from '../src/services/api/authStorage.js'
import { createSession } from '../src/services/api/session.js'
import { normalizeUser } from '../src/services/api/roles.js'

const response = (data, status = 200) => new Response(status === 204 ? null : JSON.stringify(data), { status })
const tokens = (token = 'first') => ({ accessToken: token, refreshToken: `refresh-${token}`, user: { id: 1, fullName: 'Test', email: 'test@example.com', roles: ['FarmOwner'] } })
const setup = (fetch) => { const session = createSession(); return { session, api: createApiClient({ baseUrl: 'https://api.example', session, fetch }) } }

test('login stores backend tokens and roles; failed login does not create a session', async () => {
  const { api, session } = setup(async (url, options) => {
    assert.equal(url, 'https://api.example/api/auth/login')
    assert.deepEqual(JSON.parse(options.body), { email: 'test@example.com', password: 'password' })
    return response({ success: true, data: tokens() })
  })
  await api.login({ email: 'test@example.com', password: 'password' })
  assert.equal(session.get().accessToken, 'first')
  assert.equal(authStorage.getToken(), 'first')
  assert.deepEqual(authStorage.getUser(), session.get().user)
  assert.deepEqual(normalizeUser(session.get().user).roles, ['FARM_OWNER'])
  const failed = setup(async () => response({ success: false, message: 'Invalid credentials.', errors: [] }, 401))
  await assert.rejects(failed.api.login({ email: 'bad', password: 'bad' }), { status: 401, message: 'Invalid credentials.' })
  assert.equal(failed.session.get(), null)
})

test('client rejects unsuccessful API envelopes even with an HTTP 200 response', async () => {
  const { api } = setup(async () => response({ success: false, message: 'Request rejected.' }))
  await assert.rejects(api.get('/api/farms'), { status: 200, message: 'Request rejected.' })
})

test('client sends bearer, query and JSON; unwraps data, pages and 204', async () => {
  const { api, session } = setup(async (url, options) => {
    assert.equal(options.headers.Authorization, 'Bearer first')
    if (options.method === 'DELETE') return response(null, 204)
    assert.equal(url, 'https://api.example/api/farms?search=a+b&page=2')
    return response({ success: true, data: [{ id: 4 }], pagination: { page: 2, totalPages: 3, totalItems: 5 } })
  })
  session.set(tokens())
  assert.deepEqual(await api.get('/api/farms', { search: 'a b', page: 2 }), { items: [{ id: 4 }], pagination: { page: 2, totalPages: 3, totalItems: 5 } })
  assert.equal(await api.request('/api/farms/4', { method: 'DELETE' }), null)
})

test('concurrent expired requests rotate once and retry with rotated bearer', async () => {
  let refreshes = 0
  const { api, session } = setup(async (url, options) => {
    if (url.endsWith('/refresh')) { refreshes++; await new Promise(resolve => setTimeout(resolve, 10)); return response({ success: true, data: tokens('second') }) }
    return options.headers.Authorization === 'Bearer first' ? response({ message: 'Expired' }, 401) : response({ success: true, data: 42 })
  })
  session.set(tokens())
  assert.deepEqual(await Promise.all([api.get('/api/a'), api.get('/api/b')]), [42, 42])
  assert.equal(refreshes, 1)
  assert.equal(session.get().refreshToken, 'refresh-second')
})

test('failed refresh clears session and notifies auth subscribers', async () => {
  const { api, session } = setup(async () => response({ message: 'Expired' }, 401))
  session.set(tokens()); let cleared = false
  session.subscribe(value => { cleared = value === null })
  await assert.rejects(api.get('/api/farms'), { status: 401 })
  assert.equal(session.get(), null); assert.equal(cleared, true)
  assert.equal(authStorage.getToken(), null)
  assert.equal(authStorage.getUser(), null)
})

test('unauthorized response without a refresh token clears saved auth', async () => {
  const { api, session } = setup(async () => response({ message: 'Expired' }, 401))
  session.set({ ...tokens(), refreshToken: null })
  await assert.rejects(api.get('/api/farms'), { status: 401 })
  assert.equal(session.get(), null)
  assert.equal(authStorage.getToken(), null)
  assert.equal(authStorage.getUser(), null)
})

test('original request is retried at most once', async () => {
  let requests = 0
  const { api, session } = setup(async url => {
    if (url.endsWith('/refresh')) return response({ success: true, data: tokens('second') })
    requests++; return response({ message: 'Denied' }, 401)
  })
  session.set(tokens())
  await assert.rejects(api.get('/api/farms'), { status: 401 })
  assert.equal(requests, 2); assert.equal(session.get(), null)
})

test('logout sends refresh body and bearer then clears local state even on failure', async () => {
  const { api, session } = setup(async (url, options) => {
    assert.equal(url, 'https://api.example/api/auth/logout')
    assert.equal(options.headers.Authorization, 'Bearer first')
    assert.deepEqual(JSON.parse(options.body), { refreshToken: 'refresh-first' })
    throw new TypeError('offline')
  })
  session.set(tokens()); await api.logout(); assert.equal(session.get(), null)
})

test('logout during refresh cannot resurrect the session', async () => {
  let resolveRefresh
  const { api, session } = setup(async url => {
    if (url.endsWith('/refresh')) return new Promise(resolve => { resolveRefresh = resolve })
    if (url.endsWith('/logout')) return response(null, 204)
    return response({ message: 'Expired' }, 401)
  })
  session.set(tokens())
  const pending = api.get('/api/farms')
  const rejected = assert.rejects(pending)
  while (!resolveRefresh) await new Promise(resolve => setTimeout(resolve, 0))
  const logout = api.logout()
  resolveRefresh(response({ success: true, data: tokens('second') }))
  await logout
  await rejected
  assert.equal(session.get(), null)
})

test('logout rotates an expired bearer to revoke the still-valid refresh token', async () => {
  const revocations = []
  const { api, session } = setup(async (url, options) => {
    if (url.endsWith('/refresh')) return response({ success: true, data: tokens('second') })
    if (options.headers.Authorization === 'Bearer first') return response({ message: 'Expired' }, 401)
    revocations.push(JSON.parse(options.body).refreshToken)
    assert.equal(options.headers.Authorization, 'Bearer second')
    return response(null, 204)
  })
  session.set(tokens())
  await api.logout()
  assert.deepEqual(revocations, ['refresh-second'])
  assert.equal(session.get(), null)
})

test('signing out an empty session does not prevent a later backend logout', async () => {
  let revoked = false
  const { api, session } = setup(async () => { revoked = true; return response(null, 204) })
  await api.logout()
  session.set(tokens())
  await api.logout()
  assert.equal(revoked, true)
  assert.equal(session.get(), null)
})

test('a concurrent me profile update does not discard rotated tokens', async () => {
  let finishRefresh
  const { api, session } = setup(async url => url.endsWith('/refresh')
    ? new Promise(resolve => { finishRefresh = resolve })
    : response({ success: true, data: tokens().user }))
  session.set(tokens())
  const refreshing = api.refresh()
  await api.restore()
  finishRefresh(response({ success: true, data: tokens('second') }))
  await refreshing
  assert.equal(session.get().refreshToken, 'refresh-second')
})

test('logout ignores a refresh still running for an older login', async () => {
  let finishOld
  const revoked = []
  const { api, session } = setup(async (url, options) => {
    if (url.endsWith('/refresh')) return new Promise(resolve => { finishOld = resolve })
    if (url.endsWith('/login')) return response({ success: true, data: tokens('new') })
    revoked.push(JSON.parse(options.body).refreshToken)
    return response(null, 204)
  })
  session.set(tokens())
  const oldRefresh = assert.rejects(api.refresh())
  await api.login({ email: 'test@example.com', password: 'password' })
  const logout = api.logout()
  finishOld(response({ success: true, data: tokens('old-rotated') }))
  await Promise.all([oldRefresh, logout])
  assert.deepEqual(revoked, ['refresh-new'])
})

test('structured validation and server errors have safe useful messages', async () => {
  const { api } = setup(async () => response({ message: 'Validation failed.', errors: [{ field: 'name', message: 'Name is required.' }] }, 422))
  await assert.rejects(api.get('/api/farms'), error => error.status === 422 && error.errors[0].field === 'name')
  const server = setup(async () => response({ message: 'SQL secret stack trace' }, 500))
  await assert.rejects(server.api.get('/api/farms'), { status: 500, message: 'The server could not complete the request. Please try again.' })
})

test('validation messages displayed by forms include the backend field guidance', async () => {
  const { api } = setup(async () => response({ message: 'Validation failed.', errors: [
    { field: 'Password', message: 'Password must be at least 12 characters.' },
    { field: 'Email', message: 'Email is required.' },
  ] }, 400))
  await assert.rejects(api.post('/api/users', {}), error => {
    assert.equal(error.status, 400)
    assert.match(error.message, /Password must be at least 12 characters/)
    assert.match(error.message, /Email is required/)
    assert.equal(error.errors.length, 2)
    return true
  })
})

test('role normalization never maps unsupported operator into a human backend role', () => {
  assert.deepEqual(normalizeUser({ roles: ['FarmAdministrator', 'FarmEngineer', 'UavDeviceOperator'] }).roles, ['ADMINISTRATOR', 'AGRICULTURAL_ENGINEER', 'UAV_DEVICE_OPERATOR'])
  assert.deepEqual(normalizeUser({ roles: ['UavDeviceOperator'] }).roles, ['UAV_DEVICE_OPERATOR'])
})

test('refresh preserves a selected assigned role and drops revoked roles', () => {
  const user = { roles: ['FarmAdministrator', 'FarmEngineer'] }
  assert.equal(normalizeUser(user, 'AGRICULTURAL_ENGINEER').activeRole, 'AGRICULTURAL_ENGINEER')
  assert.equal(normalizeUser({ roles: ['FarmOwner'] }, 'ADMINISTRATOR').activeRole, 'FARM_OWNER')
  assert.equal(normalizeUser(user, 'UAV_DEVICE_OPERATOR').activeRole, 'ADMINISTRATOR')
})

test('a delayed me response cannot overwrite a newer login, even for the same account', async () => {
  let completeMe
  const { api, session } = setup(async url => {
    if (url.endsWith('/me')) return new Promise(resolve => { completeMe = resolve })
    return response({ success: true, data: { ...tokens('new'), user: { ...tokens().user, fullName: 'Updated name' } } })
  })
  session.set(tokens())
  const pending = api.restore()
  await api.login({ email: 'test@example.com', password: 'password' })
  completeMe(response({ success: true, data: tokens().user }))
  await assert.rejects(pending)
  assert.equal(session.get().user.fullName, 'Updated name')
  assert.equal(session.get().accessToken, 'new')
})

test('a delayed old-session 401 does not refresh or clear a newer same-account login', async () => {
  let finishOld, refreshes = 0
  const { api, session } = setup(async url => {
    if (url.endsWith('/farms')) return new Promise(resolve => { finishOld = resolve })
    if (url.endsWith('/refresh')) refreshes++
    return response({ success: true, data: tokens('new') })
  })
  session.set(tokens()); const pending = api.get('/api/farms')
  await api.login({ email: 'test@example.com', password: 'password' })
  finishOld(response({ message: 'Expired' }, 401))
  await assert.rejects(pending)
  assert.equal(refreshes, 0); assert.equal(session.get().accessToken, 'new')
})

test('concurrent startup restore validates me once', async () => {
  let calls = 0
  const { api, session } = setup(async () => { calls++; await new Promise(resolve => setTimeout(resolve, 5)); return response({ success: true, data: tokens().user }) })
  session.set(tokens())
  await Promise.all([api.restore(), api.restore()])
  assert.equal(calls, 1)
})

test('a response body arriving after sign-out is not delivered to a protected screen', async () => {
  let finishBody
  const { api, session } = setup(async () => ({ status: 200, ok: true, json: () => new Promise(resolve => { finishBody = resolve }) }))
  session.set(tokens())
  const pending = api.get('/api/users')
  while (!finishBody) await new Promise(resolve => setTimeout(resolve, 0))
  session.set(null)
  finishBody({ success: true, data: [{ id: 2, fullName: 'Private user' }] })
  await assert.rejects(pending, { status: 401 })
})

test('request timeout aborts fetch and reports a timeout error', async () => {
  const session = createSession()
  const api = createApiClient({
    baseUrl: 'https://api.example',
    session,
    timeout: 5,
    fetch: (_url, { signal }) => new Promise((_resolve, reject) => {
      signal.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')), { once: true })
    }),
  })
  await assert.rejects(api.get('/api/farms'), { message: 'The request timed out. Please try again.' })
})

test('session serializes to storage and restores the saved session', () => {
  const values = new Map()
  const storage = {
    getItem: key => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: key => values.delete(key),
  }
  const first = createSession(storage, createAuthStorage(storage))
  first.set(tokens())
  assert.deepEqual(JSON.parse(values.get('smartfarm.api.session')), { ...tokens(), sessionId: 'refresh-first' })
  assert.deepEqual(createSession(storage, createAuthStorage(storage)).get(), tokens())
  first.set(null)
  assert.equal(values.has('smartfarm.api.session'), false)
})

test('session migrates previously stored token and user data to auth storage', () => {
  const values = new Map([['smartfarm.api.session', JSON.stringify(tokens())]])
  const storage = {
    getItem: key => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: key => values.delete(key),
  }
  const auth = createAuthStorage(storage)
  assert.deepEqual(createSession(storage, auth).get(), tokens())
  assert.equal(auth.getToken(), 'first')
  assert.deepEqual(auth.getUser(), tokens().user)
  assert.deepEqual(JSON.parse(values.get('smartfarm.api.session')), { ...tokens(), sessionId: 'refresh-first' })
})

test('auth storage safely serializes user data and clears both auth values', () => {
  const values = new Map()
  const storage = {
    getItem: key => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: key => values.delete(key),
  }
  const savedAuth = createAuthStorage(storage)
  savedAuth.setToken('access-token')
  savedAuth.setUser(tokens().user)
  assert.equal(savedAuth.getToken(), 'access-token')
  assert.deepEqual(savedAuth.getUser(), tokens().user)
  assert.equal(values.get('smartfarm.user_info'), JSON.stringify(tokens().user))
  values.delete('smartfarm.access_token')
  values.delete('smartfarm.user_info')
  assert.equal(savedAuth.getToken(), null)
  assert.equal(savedAuth.getUser(), null)
  savedAuth.setToken('access-token')
  savedAuth.setUser(tokens().user)
  values.set('smartfarm.user_info', '{invalid')
  assert.equal(savedAuth.getUser(), null)
  savedAuth.clearAuth()
  assert.equal(savedAuth.getToken(), null)
  assert.equal(savedAuth.getUser(), null)
  assert.equal(values.size, 0)
})
