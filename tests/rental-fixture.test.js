import test from 'node:test'
import assert from 'node:assert/strict'
import { makeRentalDue } from '../e2e/rental-fixture.js'

test('rental timing fixture rejects invalid identities and non-test databases before spawning SQL', () => {
  const keys = ['E2E_API_URL', 'TEST_POSTGRES_CONNECTION', 'E2E_PSQL']
  const previous = Object.fromEntries(keys.map(key => [key, process.env[key]]))
  try {
    process.env.E2E_API_URL = 'http://127.0.0.1:5189'
    process.env.E2E_PSQL = 'must-not-execute'
    process.env.TEST_POSTGRES_CONNECTION = 'Host=127.0.0.1;Port=12345;Database=postgres;Username=auth_test;Password=test;'
    for (const id of [-1, 0, 1.5, '1; DELETE FROM rental_contracts']) assert.throws(() => makeRentalDue(id), /disposable/)
    process.env.TEST_POSTGRES_CONNECTION = 'Host=127.0.0.1;Port=5432;Database=app;Username=postgres;'
    assert.throws(() => makeRentalDue(1), /disposable/)
  } finally {
    for (const key of keys) {
      if (previous[key] === undefined) delete process.env[key]
      else process.env[key] = previous[key]
    }
  }
})
