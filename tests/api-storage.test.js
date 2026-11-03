import test from 'node:test'
import assert from 'node:assert/strict'
import { createAuthStorage } from '../src/services/api/authStorage.js'
import { createSession } from '../src/services/api/session.js'

const tokens = (token = 'first') => ({ accessToken: token, refreshToken: `refresh-${token}`, user: { id: 1, fullName: 'Test', email: 'test@example.com', roles: ['FarmOwner'] } })

test('session serializes to storage and restores the saved session', () => {
  const values = new Map()
  const storage = {
    getItem: key => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: key => values.delete(key),
  }
  const first = createSession(storage, createAuthStorage(storage))
  first.set(tokens())
  assert.deepEqual(JSON.parse(values.get('smartfarm.api.session')), { refreshToken: 'refresh-first', sessionId: 'refresh-first', userId: 1 })
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
  assert.deepEqual(JSON.parse(values.get('smartfarm.api.session')), { refreshToken: 'refresh-first', sessionId: 'refresh-first', userId: 1 })
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
