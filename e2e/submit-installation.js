import { installAndActivate } from './install-and-activate'
import { expect } from '@playwright/test'

export async function submitInstallation(browser, request, adminPage, consultationId) {
  const api = process.env.E2E_API_URL
  const password = process.env.SEED_ADMIN_PASSWORD
  const login = await request.post(api + '/api/auth/login', { data: { email: 'admin@sep.local', password: '123456' } })
  expect(login.ok()).toBeTruthy()
  const { accessToken } = (await login.json()).data
  async function seed(path, data) {
    const response = await request.post(api + path, { data, headers: { Authorization: 'Bearer ' + accessToken } })
    expect(response.ok(), path).toBeTruthy()
    return (await response.json()).data
  }
  const engineer = await seed('/api/users', { email: 'survey-engineer@example.com', fullName: 'Survey Engineer', password, roles: ['FarmEngineer'] })
  const owner = await seed('/api/users', { email: 'survey-owner@example.com', fullName: 'Survey Owner', password, roles: ['FarmOwner'] })
  const farm = await seed('/api/farms', { name: 'Consultation orchard', latitude: 10.5, longitude: 105.7 })
  await seed(`/api/farms/${farm.id}/members`, { userId: owner.id })

  const context = await browser.newContext({ baseURL: 'http://127.0.0.1:5179' })
  try {
    const page = await context.newPage()
    await page.goto('/dang-nhap')
    await page.getByLabel('Email', { exact: true }).fill(engineer.email)
    await page.locator('input[name=password]').fill(password)
    await page.locator('button[type=submit]').click()
    await page.getByRole('link', { name: 'Consultations', exact: true }).click()
    await page.getByRole('button', { name: 'Claim request #' + consultationId }).click()
    await page.getByRole('link', { name: 'Open #' + consultationId }).click()
    await page.getByLabel('Notes', { exact: true }).fill('Reached customer by phone')
    await page.getByLabel('Contact result').fill('Confirmed orchard visit')
    await page.getByLabel('Next action').fill('Survey irrigation zones')
    await page.getByRole('button', { name: 'Save follow-up' }).click()
    await expect(page.getByText('Follow-up recorded.')).toBeVisible()

    await adminPage.goto('/app/consultations/' + consultationId)
    await adminPage.getByRole('combobox', { name: 'Customer farm' }).selectOption(String(farm.id))
    await adminPage.getByRole('combobox', { name: 'Farm owner' }).selectOption(String(owner.id))
    await adminPage.getByRole('button', { name: 'Confirm customer farm' }).click()
    await expect(adminPage.getByText('Linked farm #' + farm.id + ' · Owner #' + owner.id)).toBeVisible()

    await page.getByRole('button', { name: 'Record survey' }).click()
    const survey = page.getByRole('form', { name: 'Survey revision' })
    await survey.getByLabel('Farm name').fill(farm.name)
    await survey.getByLabel('Farm location').fill('Đồng Tháp')
    await survey.getByLabel('Monitoring needs').fill('Measure moisture around roots')
    await survey.getByLabel('Zone name').fill('South orchard')
    await survey.getByLabel('Proposed installation location').fill('Beside irrigation pump')
    await survey.getByRole('button', { name: 'Save survey revision' }).click()
    await expect(page.getByText('Survey revision 1 saved.')).toBeVisible()
    await page.getByRole('button', { name: 'Prepare proposal' }).click()
    const proposal = page.getByRole('form', { name: 'Device proposal' })
    await proposal.getByRole('combobox', { name: 'Survey revision' }).selectOption({ label: 'Revision 1: ' + farm.name })
    await proposal.getByRole('combobox', { name: 'Survey zone' }).selectOption({ label: 'South orchard' })
    await proposal.getByLabel('Rental duration (months)').fill('6')
    const installationDate = new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 10)
    await proposal.getByLabel('Requested installation time').fill(installationDate + 'T09:00')
    await proposal.getByLabel('Device type').fill('Monitoring UAV')
    await proposal.getByLabel('Installation location').fill('Beside irrigation pump')
    await proposal.getByRole('button', { name: 'Add proposed device' }).click()
    const second = proposal.getByRole('group', { name: 'Device 2', exact: true })
    await second.getByRole('combobox', { name: 'Survey zone' }).selectOption({ label: 'South orchard' })
    await second.getByLabel('Device type').fill('Mobile gateway')
    await second.getByLabel('Installation location').fill('Orchard dock')
    await proposal.getByRole('button', { name: 'Save proposal revision' }).click()
    await page.getByLabel('Confirmation evidence or reference').fill('Customer accepted survey proposal')
    await page.getByRole('button', { name: 'Record customer plan confirmation' }).click()
    await expect(page.getByText('Customer plan confirmation recorded.')).toBeVisible()
    await page.getByLabel('Confirmation evidence or reference').fill('External deposit receipt R-114')
    await page.getByRole('button', { name: 'Record external deposit confirmation' }).click()
    await page.getByRole('button', { name: 'Submit installation request' }).click()
    const receipt = await page.getByText(/Installation request #\d+ submitted for administrator review/).innerText()
    await installAndActivate(browser, adminPage, seed, Number(receipt.match(/#(\d+)/)[1]), owner, password, farm)
  } finally { await context.close() }
}
