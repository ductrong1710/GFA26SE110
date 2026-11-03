import { expect } from '@playwright/test'

export async function runMission(page, farm, uav, gateway, sensor, completed = false) {
  await page.goto('/app/missions/create')
  const next = () => page.getByRole('button', { name: 'Next', exact: true }).click()
  await page.getByLabel('Mission name').fill('Orchard moisture collection')
  await page.getByRole('radio', { name: new RegExp(farm.name) }).check()
  await next()
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
  await page.getByRole('button', { name: 'Save Mission', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Orchard moisture collection' })).toBeVisible()
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
