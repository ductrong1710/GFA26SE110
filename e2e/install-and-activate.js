import { verifyMaintenance } from './verify-maintenance'
import { runMission } from './run-mission'
import { repairDevice } from './repair-device'
import { vietnamDateTimeInput } from '../src/utils/display.js'
import { expect } from '@playwright/test'

export async function installAndActivate(browser, admin, seed, requestId, owner, password, farm) {
  const tech = await seed('/api/users', { email: 'installation-tech@example.com', fullName: 'Installation Tech', password, roles: ['Technician'] })
  const uav = await seed('/api/uavs', { code: 'ORCHARD-UAV', name: 'Orchard UAV', status: 'ONLINE' })
  const gateway = await seed('/api/gateways', { code: 'ORCHARD-GW', name: 'Orchard gateway', gatewayType: 'ESP32', status: 'ONLINE' })
  const zone = await seed(`/api/farms/${farm.id}/zones`, { name: 'South orchard' })
  const sensor = await seed('/api/sensor-nodes', { zoneId: zone.id, name: 'Orchard moisture', deviceCode: 'ORCHARD-SOIL', latitude: 10.501, longitude: 105.701, status: 'ONLINE' })
  await admin.getByRole('link', { name: 'Installation requests', exact: true }).click()
  await admin.getByRole('button', { name: 'Open request #' + requestId, exact: true }).click()
  await admin.getByRole('combobox', { name: 'Technician' }).selectOption(String(tech.id))
  const date = new Date(Date.now() + 2 * 86400000).toISOString().slice(0, 10)
  await admin.getByLabel('Appointment').fill(date + 'T12:00')
  await admin.getByLabel('Assignment note').fill('Install at the agreed orchard site')
  await admin.getByRole('button', { name: 'Create installation job' }).click()
  const receipt = await admin.getByText(/Installation job #\d+ created/).innerText()
  const jobId = Number(receipt.match(/#(\d+)/)[1])
  async function openJob(page) {
    await page.goto('/app/installation-jobs')
    await page.getByRole('button', { name: 'Open job #' + jobId, exact: true }).click()
  }
  await openJob(admin)
  await admin.getByRole('combobox', { name: 'Device 1 for Monitoring UAV' }).selectOption('uav:' + uav.id)
  await admin.getByRole('combobox', { name: 'Device 1 for Mobile gateway' }).selectOption('gateway:' + gateway.id)
  await admin.getByLabel('Device allocation note').fill('Asset serial verified')
  const allocation = admin.waitForResponse(r => r.request().method() === 'PUT' && r.url().endsWith('/devices'))
  await admin.getByRole('button', { name: 'Save allocated devices' }).click()
  expect((await allocation).ok()).toBeTruthy()
  const employee = await browser.newContext({ baseURL: 'http://127.0.0.1:5179' })
  const customer = await browser.newContext({ baseURL: 'http://127.0.0.1:5179' })
  async function login(context, email) {
    const page = await context.newPage()
    await page.goto('/dang-nhap')
    await page.getByLabel('Email', { exact: true }).fill(email)
    await page.locator('input[name=password]').fill(password)
    await page.locator('button[type=submit]').click()
    await expect(page).toHaveURL(/\/app/)
    await openJob(page)
    return page
  }
  try {
    const work = await login(employee, tech.email)
    await work.getByRole('button', { name: 'Start installation' }).click()
    await work.getByRole('button', { name: 'Prepare installation report' }).click()
    for (const device of [uav, gateway]) {
      await work.getByLabel('Installation location for ' + device.code).fill('Orchard dock')
      for (const label of ['Accessories', 'Connectivity', 'Operation'])
        await work.getByRole('combobox', { name: label + ' for ' + device.code }).selectOption('true')
      await work.getByLabel('Test evidence for ' + device.code).fill('Connectivity and operating checks passed at the dock')
    }
    await work.getByLabel('Handover and installation notes').fill('Delivered and demonstrated to owner')
    const image = { name: 'signed.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=', 'base64') }
    for (const label of ['Signed contract', 'Signed installation handover']) {
      const group = work.getByRole('group', { name: label, exact: true })
      await group.getByLabel('Add evidence images').setInputFiles(image)
      await expect(group.getByAltText('Evidence image 1')).toBeVisible()
    }
    await work.getByRole('button', { name: 'Submit installation report' }).click()
    await expect(work.getByRole('button', { name: 'View report revision 1' })).toBeVisible()
    const ownerPage = await login(customer, owner.email)
    await ownerPage.getByRole('button', { name: 'View report revision 1' }).click()
    await ownerPage.getByLabel('Decision note').fill('Received the installed equipment')
    await ownerPage.getByRole('button', { name: 'Confirm handover' }).click()
    await expect(ownerPage.getByText('Installation decision recorded.')).toBeVisible()
    await openJob(admin)
    await admin.getByRole('button', { name: 'View report revision 1' }).click()
    await admin.getByLabel('Decision note').fill('Verified signed handover and report')
    await admin.getByRole('button', { name: 'Approve installation' }).click()
    await admin.getByRole('link', { name: 'Open rental contracts' }).click()
    const contractButton = admin.getByRole('button', { name: /^Open contract #/ }).first()
    const contractId = Number((await contractButton.innerText()).match(/#(\d+)/)[1])
    const button = 'Open contract #' + contractId
    await admin.getByRole('button', { name: button, exact: true }).click()
    await admin.getByLabel('Decision note').fill('Agreed rental period has started')
    const region = admin.getByRole('region', { name: 'Rental contract #' + contractId, exact: true })
    await expect(region.getByText('Installation job', { exact: true }).locator('xpath=following-sibling::dd[1]')).toHaveText('#' + jobId)
    const tomorrow = new Date(Date.now() + 7 * 3600000 + 86400000).toISOString().slice(0, 10)
    const expectedStart = new Date(tomorrow + 'T00:00:00+07:00')
    const displayedStart = new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(expectedStart)
    await expect(region.getByText('Rental start', { exact: true }).locator('xpath=following-sibling::dd[1]')).toHaveText(displayedStart)
    await admin.getByRole('button', { name: 'Record contract action' }).click()
    await expect(admin.getByRole('alert').filter({ hasText: /Activation requires/ }).first()).toBeVisible()
    await admin.getByLabel('Contract action').selectOption('confirm-start')
    await admin.getByLabel('Agreed rental start').fill(vietnamDateTimeInput(new Date()))
    await admin.getByLabel('Decision note').fill('Customer agreed to begin at handover today')
    await admin.getByRole('button', { name: 'Record contract action' }).click()
    await expect(admin.getByText('Rental contract updated.')).toBeVisible()
    const contract = ownerPage.getByRole('region', { name: 'Rental contract #' + contractId, exact: true })
    await expect(async () => {
      await ownerPage.goto('/app/rental-contracts')
      await ownerPage.getByRole('button', { name: button, exact: true }).click()
      await expect(contract.getByText(/^active$/i)).toBeVisible({ timeout: 1000 })
    }).toPass({ timeout: 20000 })
    await expect(contract.getByText(uav.code + ' · ' + uav.name)).toBeVisible()
    await runMission(ownerPage, farm, uav, gateway, sensor, true)
    await runMission(ownerPage, farm, uav, gateway, sensor)
    await repairDevice(ownerPage, admin, work, contractId, uav, tech.id)
    await verifyMaintenance(admin, work, contractId, uav, tech.id)
  } finally { await employee.close(); await customer.close() }
}
