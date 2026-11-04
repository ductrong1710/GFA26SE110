import { test, expect } from '@playwright/test'

test('owner reads synchronized measurements and compares sensors while admin maintains threshold history', async ({ browser, page, request }) => {
  const base = process.env.E2E_API_URL
  if (base !== 'http://127.0.0.1:5189' || !process.env.TEST_POSTGRES_CONNECTION) throw new Error('Use the disposable PostgreSQL browser harness.')
  let token
  async function post(path, data) {
    const response = await request.post(base + path, { data, headers: token ? { Authorization: 'Bearer ' + token } : {} })
    expect(response.ok(), path + ' ' + response.status()).toBeTruthy()
    return (await response.json()).data
  }
  const adminPassword = '123456'
  token = (await post('/api/auth/login', { email: 'admin@sep.local', password: adminPassword })).accessToken
  const password = process.env.SEED_ADMIN_PASSWORD
  const owner = await post('/api/users', { email: 'monitoring-owner@example.com', fullName: 'Monitoring owner', password, roles: ['FarmOwner'] })
  const farm = await post('/api/farms', { name: 'Sensor comparison farm' })
  await post('/api/farms/' + farm.id + '/members', { userId: owner.id })
  const zone = await post('/api/farms/' + farm.id + '/zones', { name: 'Sensor comparison zone' })
  const type = await post('/api/sensor-types', { code: 'BROWSER-TEMP', name: 'Orchard temperature', unit: 'C' })
  const devices = []
  for (const [code, value] of [['SOUTH-1', 21], ['SOUTH-2', 27]]) {
    const node = await post('/api/sensor-nodes', { zoneId: zone.id, deviceCode: code, name: code + ' thermometer', status: 'ONLINE' })
    await post('/api/sensor-nodes/' + node.id + '/channels', { sensorTypeId: type.id, channelCode: 'temperature', name: 'Temperature' })
    devices.push({ id: node.id, code, value })
  }
  const gateway = await post('/api/gateways', { code: 'MONITOR-GATEWAY', name: 'Monitoring gateway', gatewayType: 'ESP32', status: 'ONLINE' })
  const credential = await post('/api/gateways/' + gateway.id + '/credential')
  const measuredAt = new Date(Date.now() - 180000).toISOString()
  const response = await request.post(base + '/api/device/gateways/' + gateway.id + '/sync', {
    headers: { 'X-Gateway-Code': gateway.code, 'X-Api-Key': credential.apiKey },
    data: { batchKey: 'browser-temperature', records: devices.map(d => ({ sensorNodeCode: d.code, channelCode: 'temperature',
      sourceRecordKey: d.code + '-reading', measuredAt, collectedAt: measuredAt, value: d.value })) },
  })
  expect(response.ok()).toBeTruthy()
  async function login(tab, email, secret) {
    await tab.goto('/dang-nhap')
    await tab.getByLabel('Email', { exact: true }).fill(email)
    await tab.locator('input[name=password]').fill(secret)
    await tab.locator('button[type=submit]').click()
    await expect(tab.getByRole('button', { name: /^Account:/ })).toBeVisible()
  }
  await login(page, owner.email, password)
  await page.goto('/app/sensors/' + devices[0].id)
  await expect(page.getByRole('table', { name: 'Channel history' }).getByRole('cell', { name: '21', exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Configure Threshold' })).toHaveCount(0)
  await page.goto('/app/sensor-data/compare-zones')
  await page.getByRole('combobox', { name: 'Sensor Type' }).selectOption(String(type.id))
  const comparison = page.getByRole('table', { name: 'Sensor comparison summary' })
  for (const d of devices) {
    const row = comparison.getByRole('row').filter({ hasText: d.code })
    await expect(row.getByRole('cell', { name: String(d.value), exact: true })).toHaveCount(3)
    await expect(row.getByRole('cell', { name: '1', exact: true })).toBeVisible()
  }
  const admin = await browser.newPage({ baseURL: 'http://127.0.0.1:5179' })
  try {
    await login(admin, 'admin@sep.local', adminPassword)
    await admin.goto('/app/sensors/' + devices[0].id)
    await admin.getByRole('button', { name: 'Configure Threshold' }).click()
    await admin.getByLabel('Maximum', { exact: true }).fill('35')
    const saved = admin.waitForResponse(res => res.request().method() === 'PUT' && res.url().endsWith('/threshold'))
    await admin.getByRole('button', { name: 'Save Configuration' }).click()
    expect((await saved).ok()).toBeTruthy()
    await admin.reload()
    await admin.getByRole('button', { name: 'Threshold history' }).click()
    await expect(admin.getByRole('table', { name: 'Recorded threshold settings' }).getByRole('cell', { name: '35', exact: true })).toBeVisible()
  } finally { await admin.close() }
})
