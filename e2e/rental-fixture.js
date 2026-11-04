import { execFileSync } from 'node:child_process'

export function makeRentalDue(id) {
  const connection = process.env.TEST_POSTGRES_CONNECTION ?? ''
  if (process.env.E2E_API_URL !== 'http://127.0.0.1:5189' || !Number.isSafeInteger(id) || id <= 0 ||
      !/^Host=127\.0\.0\.1;Port=\d+;Database=postgres;Username=auth_test;/.test(connection) || !process.env.E2E_PSQL) {
    throw new Error('Rental time fixture requires the disposable browser database.')
  }
  const settings = Object.fromEntries(connection.split(';').filter(Boolean).map(part => {
    const index = part.indexOf('=')
    return [part.slice(0, index), part.slice(index + 1)]
  }))
  // After asserting next-day creation and early activation rejection, make this test contract due.
  const changed = execFileSync(process.env.E2E_PSQL, ['-X', '-t', '-A', '-q', '-v', 'ON_ERROR_STOP=1', '-c',
    `UPDATE rental_contracts SET start_at = now() - interval '1 minute' WHERE id = ${id} AND status = 'DRAFT' RETURNING id;`], {
    env: { ...process.env, PGHOST: settings.Host, PGPORT: settings.Port, PGDATABASE: settings.Database,
      PGUSER: settings.Username, PGPASSWORD: settings.Password }, stdio: 'pipe',
  }).toString().trim()
  if (changed !== String(id)) throw new Error('Expected exactly one draft test rental to become due.')
}
