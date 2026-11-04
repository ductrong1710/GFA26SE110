import { expect } from '@playwright/test'

export async function runMission(page, farm, uav, gateway, sensor, completed = false, gatewayKey) {
  await page.goto('/app/missions/create')
  const next = () => page.getByRole('button', { name: 'Next', exact: true }).click()
  await page.getByLabel('Mission name').fill('Orchard moisture collection')
  await page.getByRole('radio', { name: new RegExp(farm.name) }).check()
  await next()
  await page.getByLabel('Mission zone').selectOption(String(sensor.zoneId))
  await page.getByRole('checkbox', { name: 'Select ' + sensor.deviceCode }).check()
  await next()
  await page.getByRole('button', { name: 'Auto-group Sensors' }).click()
  await next()
  await page.getByRole('button', { name: 'Generate Route from Points' }).click()
  await next()
  await next()
  await page.getByRole('radio', { name: new RegExp(uav.code) }).check()
  await next()
  await page.getByRole('radio', { name: new RegExp(gateway.code) }).check()
  await next()
  const day = new Date(Date.now() + 86400000).toISOString().slice(0, 10)
  await page.getByLabel('Scheduled time (Vietnam, optional)').fill(day + 'T10:00')
  await page.getByLabel('Scheduled end (Vietnam)').fill(day + 'T11:00')
  await page.getByLabel('Mission purpose').fill('Check orchard irrigation coverage')
  await page.getByRole('button', { name: 'Save Mission', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Orchard moisture collection' })).toBeVisible()
  await expect(page.getByText(/Check orchard irrigation coverage/)).toBeVisible()
  await expect(page.locator('p').filter({ hasText: 'Mission zone' })).toContainText('South orchard')
  async function record(status, note) {
    await page.getByRole('button', { name: 'Update Mission Status' }).click()
    await page.getByLabel('Reported status').selectOption(status)
    await page.getByRole('textbox', { name: 'Operational note', exact: true }).fill(note)
    if (status === 'COMPLETED') await page.getByRole('checkbox', { name: 'Confirm observed flight completion with unavailable collection outcomes' }).check()
    await page.getByRole('button', { name: 'Save Status Report' }).click()
    await expect(page.locator('.monitor-meta [data-status="' + status + '"]')).toBeVisible()
  }
  await record('RUNNING', 'Owner observed takeoff')
  await expect(page.getByText('No live position stream is connected.')).toBeVisible()
  if (completed) {
    const missionId = Number(new URL(page.url()).pathname.split('/').at(-1))
    const sendPosition = async (values) => {
      const response = await page.request.post(process.env.E2E_API_URL + '/api/device/telemetry', {
        headers: { 'X-Gateway-Code': gateway.code, 'X-Api-Key': gatewayKey },
        data: { missionId, uavId: uav.id, gatewayId: gateway.id, recordedAt: new Date().toISOString(), ...values },
      })
      expect(response.status()).toBe(201)
    }
    const history = page.getByRole('table', { name: 'Mission telemetry history' })
    await sendPosition({ latitude: 10.501, longitude: 105.701, altitudeM: 15, batteryPercent: 73, currentWaypointNo: 1, flightStatus: 'AUTO' })
    await expect(history.getByRole('cell', { name: 'AUTO', exact: true })).toBeVisible({ timeout: 20000 })
    await expect(page.getByRole('meter', { name: 'Battery', exact: true })).toHaveAttribute('aria-valuenow', '73')
    await expect(page.locator('svg title').filter({ hasText: 'Current UAV location · Recorded telemetry' })).toHaveCount(1)
    await expect(page.locator('svg title').filter({ hasText: 'Waypoint 1 · Current waypoint' })).toHaveCount(1)
    await sendPosition({ latitude: 10.502, longitude: 105.702, flightStatus: 'HOVER' })
    const latest = history.getByRole('row').filter({ hasText: 'HOVER' })
    await expect(latest.getByRole('cell', { name: '10.502', exact: true })).toBeVisible({ timeout: 20000 })
    await expect(latest.getByRole('cell', { name: 'Unavailable', exact: true })).toHaveCount(3)
    await expect(page.getByRole('img', { name: 'Battery: Unknown battery level', exact: true })).toBeVisible()
    await expect(history.getByRole('row')).toHaveCount(3)
    await record('COMPLETED', 'Observed safe landing; collection outcome unavailable.')
    await page.reload()
    await expect(page.locator('.monitor-meta [data-status="COMPLETED"]')).toBeVisible()
    await expect(page.getByText('MANUAL_COMPLETION', { exact: true })).toBeVisible()
    await expect(page.getByText('Sensor Targets 0/1', { exact: true })).toBeVisible()
    return
  }
  await record('FAILED', 'Dock connector intermittently loses power.')
  await page.reload()
  await expect(page.locator('.mission-error').filter({ hasText: 'Dock connector intermittently loses power.' })).toBeVisible()
  await page.getByRole('link', { name: 'Report device incident' }).click()
  await expect(page.getByText(/Linked mission #\d+: Orchard moisture collection/)).toBeVisible()
}
