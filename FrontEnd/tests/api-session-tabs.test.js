import test from 'node:test'
import assert from 'node:assert/strict'
import { createSession } from '../src/services/api/session.js'
import { createAuthStorage, authStorage } from '../src/services/api/authStorage.js'
import { createApiClient } from '../src/services/api/apiClient.js'

const tokens = (id, token) => ({ accessToken: token, refreshToken: `refresh-${token}`, user: { id } })
const response = data => new Response(JSON.stringify({ success: true, data }))
function tabs() {
  const values = new Map()
  const storage = { getItem: k => values.get(k) ?? null, setItem: (k, v) => values.set(k, v), removeItem: k => values.delete(k) }
  const events = new EventTarget()
  const a = createSession(storage, createAuthStorage(storage), events)
  a.set(tokens(1, 'A'))
  const b = createSession(storage, createAuthStorage(storage), events)
  const changed = () => events.dispatchEvent(Object.assign(new Event('storage'), { key: 'smartfarm.api.session', storageArea: storage }))
  return { a, b, storage, changed }
}

test('requests use the displayed session token, never another auth storage token', async () => {
  const { a } = tabs()
  authStorage.setToken('other-account')
  const api = createApiClient({ baseUrl: 'https://example.test', session: a, fetch: async (_url, options) => {
    assert.equal(options.headers.Authorization, 'Bearer A')
    return response(1)
  } })
  assert.equal(await api.get('/api/farms'), 1)
})

test('an account switch signs out the stale tab without erasing the new account', () => {
  const { a, b, storage, changed } = tabs()
  const identity = a.identity()
  let notified = false
  const stop = a.subscribe(value => { notified = value === null })
  b.set(tokens(2, 'B'))
  changed()
  assert.equal(a.get(), null)
  assert.notEqual(a.identity(), identity)
  assert.equal(notified, true)
  assert.equal(createSession(storage, createAuthStorage(storage)).get().user.id, 2)
  stop()
})

test('same-login refresh updates both tokens without changing tab identity', () => {
  const { a, b } = tabs()
  const identity = a.identity()
  b.set(tokens(1, 'rotated'), { preserveIdentity: true })
  assert.deepEqual(a.get(), tokens(1, 'rotated'))
  assert.equal(a.identity(), identity)
})

test('same-account new login invalidates old in-flight responses', async () => {
  const { a, b } = tabs()
  let finish
  const api = createApiClient({ baseUrl: 'https://example.test', session: a, fetch: () => new Promise(resolve => { finish = resolve }) })
  const pending = api.get('/api/farms')
  while (!finish) await new Promise(resolve => setTimeout(resolve, 0))
  b.set(tokens(1, 'new-login'))
  finish(response(['private']))
  await assert.rejects(pending, { status: 401 })
  assert.equal(a.get(), null)
})

test('logout completing after another tab logs in cannot erase that login', async () => {
  const { a, b, storage } = tabs()
  let finish
  const api = createApiClient({ baseUrl: 'https://example.test', session: a, fetch: () => new Promise(resolve => { finish = resolve }) })
  const pending = api.logout()
  while (!finish) await new Promise(resolve => setTimeout(resolve, 0))
  b.set(tokens(2, 'B'))
  finish(response(null))
  await pending
  assert.equal(createSession(storage, createAuthStorage(storage)).get().user.id, 2)
})

test('late refresh response cannot resurrect the old account or overwrite the new one', async () => {
  const { a, b, storage } = tabs()
  let finish
  const api = createApiClient({ baseUrl: 'https://example.test', session: a, fetch: () => new Promise(resolve => { finish = resolve }) })
  const pending = api.refresh()
  while (!finish) await new Promise(resolve => setTimeout(resolve, 0))
  b.set(tokens(2, 'B'))
  finish(response(tokens(1, 'rotated')))
  await assert.rejects(pending, { status: 401 })
  assert.equal(a.get(), null)
  assert.equal(createSession(storage, createAuthStorage(storage)).get().user.id, 2)
})
