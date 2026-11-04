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
  const type = await post('/api/sensor-types', { code: 'BROWSER-TEMP', name: 'Orchard temperature', unit: 'C' })
  const devices = []
  for (const [code, value, zoneName] of [['SOUTH-1', 21, 'South orchard'], ['NORTH-1', 27, 'North orchard']]) {
    const zone = await post('/api/farms/' + farm.id + '/zones', { name: zoneName })
    const node = await post('/api/sensor-nodes', { zoneId: zone.id, deviceCode: code, name: code + ' thermometer', status: 'ONLINE' })
    await post('/api/sensor-nodes/' + node.id + '/channels', { sensorTypeId: type.id, channelCode: 'temperature', name: 'Temperature' })
    devices.push({ id: node.id, code, value, zoneName })
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
    const signedIn = tab.waitForResponse(res => res.request().method() === 'POST' && res.url().endsWith('/api/auth/login'))
    await tab.locator('button[type=submit]').click()
    expect((await signedIn).ok()).toBeTruthy()
    await expect(tab.getByRole('button', { name: /^Account:/ })).toBeVisible({ timeout: 15000 })
  }
  await login(page, owner.email, password)
  await page.goto('/app/sensors/' + devices[0].id)
  await expect(page.getByRole('table', { name: 'Sensor channels and latest readings' }).getByRole('cell', { name: '21', exact: true })).toBeVisible()
  await expect(page.getByRole('table', { name: 'Channel history' }).getByRole('cell', { name: '21', exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Configure Threshold' })).toHaveCount(0)
  await page.goto('/app/sensor-data/compare-zones')
  await page.getByRole('combobox', { name: 'Sensor Type' }).selectOption(String(type.id))
  const comparison = page.getByRole('table', { name: 'Sensor comparison summary' })
  for (const d of devices) {
    const zone = page.getByRole('table', { name: 'Zone comparison summary' }).getByRole('row').filter({ hasText: d.zoneName })
    await expect(zone.getByRole('cell', { name: String(d.value), exact: true })).toHaveCount(3)
    await expect(zone.getByRole('cell', { name: '1', exact: true })).toBeVisible()
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
    const at = new Date().toISOString()
    const abnormal = await request.post(base + '/api/device/gateways/' + gateway.id + '/sync', {
      headers: { 'X-Gateway-Code': gateway.code, 'X-Api-Key': credential.apiKey },
      data: { batchKey: 'above-threshold', records: [{ sensorNodeCode: devices[0].code, channelCode: 'temperature',
        sourceRecordKey: 'hot-orchard', measuredAt: at, collectedAt: at, value: 40 }] },
    })
    expect(abnormal.ok()).toBeTruthy()
    async function openAlert(tab) {
      await tab.goto('/app/alerts')
      await tab.getByRole('table', { name: 'Alert management' }).getByRole('row').filter({ hasText: 'Sensor #' + devices[0].id }).getByRole('button').click()
      return tab.getByRole('dialog', { name: 'Alert details' })
    }
    const ownerAlert = await openAlert(page)
    await expect(ownerAlert.getByRole('button', { name: 'Acknowledge' })).toHaveCount(0)
    const adminAlert = await openAlert(admin)
    await adminAlert.getByRole('button', { name: 'Acknowledge' }).click()
    await expect(adminAlert.getByRole('region', { name: 'Alert history' }).getByText('Acknowledged', { exact: true })).toBeVisible()
    await adminAlert.getByLabel('Handling note').fill('Checked orchard thermometer')
    await adminAlert.getByRole('button', { name: 'Add Handling Note' }).click()
    await expect(adminAlert.getByText('Checked orchard thermometer', { exact: true })).toBeVisible()
    await adminAlert.getByRole('button', { name: 'Close Alert' }).click()
    await expect(adminAlert.getByRole('region', { name: 'Alert history' }).getByText('Closed', { exact: true })).toBeVisible()
    const history = (await openAlert(page)).getByRole('region', { name: 'Alert history' })
    for (const action of ['Alert detected', 'Acknowledged', 'Handling note', 'Closed', 'Checked orchard thermometer']) {
      await expect(history.getByText(action, { exact: true })).toBeVisible()
    }
  } finally { await admin.close() }
})
