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
  async function publishProduct(title, estimatedMonthlyRentVnd) {
    const product = await seed('/api/admin/public-content', { expectedRevision: 0, kind: 'DEVICE', title, body: title, estimatedMonthlyRentVnd })
    await seed(`/api/admin/public-content/${product.id}/publish`, { expectedRevision: product.revision })
    return String(product.id)
  }
  const uavProduct = await publishProduct('Orchard UAV rental', 900000)
  const gatewayProduct = await publishProduct('Mobile gateway rental', 100000)
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

    await page.reload()
    await page.getByRole('button', { name: 'Record survey' }).click()
    const survey = page.getByRole('form', { name: 'Survey revision' })
    await survey.getByLabel('Farm name').fill(farm.name)
    await survey.getByLabel('Farm location').fill('Đồng Tháp')
    await survey.getByLabel('Monitoring needs').fill('Measure moisture around roots')
    await survey.getByLabel('Zone name').fill('South orchard')
    await survey.getByLabel('Proposed installation location').fill('Beside irrigation pump')
    await survey.getByRole('button', { name: 'Save survey revision' }).click()
    await expect(page.getByText('Survey revision 1 saved.')).toBeVisible()
    for (const revision of [1, 2]) {
      await page.getByRole('button', { name: 'Prepare proposal' }).click()
      const proposal = page.getByRole('form', { name: 'Device proposal' })
      await proposal.getByRole('combobox', { name: 'Survey revision' }).selectOption({ label: 'Revision 1: ' + farm.name })
      await proposal.getByRole('combobox', { name: 'Survey zone' }).selectOption({ label: 'South orchard' })
      await proposal.getByLabel('Rental duration (months)').fill('6')
      const installationDate = new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 10)
      await proposal.getByLabel('Requested installation time').fill(installationDate + 'T09:00')
      await proposal.getByLabel('Device type').fill('Monitoring UAV')
      await proposal.getByLabel('Published rental product').selectOption(uavProduct)
      await proposal.getByLabel('Installation location').fill(revision === 1 ? 'Beside irrigation pump' : 'Marked platform two metres east of the irrigation pump')
      await proposal.getByRole('button', { name: 'Add proposed device' }).click()
      const second = proposal.getByRole('group', { name: 'Device 2', exact: true })
      await second.getByRole('combobox', { name: 'Survey zone' }).selectOption({ label: 'South orchard' })
      await second.getByLabel('Device type').fill('Mobile gateway')
      await second.getByLabel('Published rental product').selectOption(gatewayProduct)
      await second.getByLabel('Installation location').fill('Orchard dock')
      await proposal.getByRole('button', { name: 'Save proposal revision' }).click()
      await page.getByLabel('Confirmation evidence or reference').fill('Customer accepted survey proposal')
      await page.getByRole('button', { name: 'Record customer plan confirmation' }).click()
      await expect(page.getByText('Customer plan confirmation recorded.')).toBeVisible()
      await page.getByLabel('Confirmation evidence or reference').fill('External deposit receipt R-114')
      await page.getByRole('button', { name: 'Record external deposit confirmation' }).click()
      await page.getByLabel('Dossier note').fill('Attached external receipt R-114')
      const dossier = page.getByRole('group', { name: 'Installation dossier' })
      await dossier.getByLabel('Add evidence images').setInputFiles({ name: 'receipt.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=', 'base64') })
      await dossier.getByRole('button', { name: 'Submit installation request' }).click()
      const receipt = await page.getByText(/Dossier #\d+ submitted for administrator review/).innerText()
      await adminPage.goto('/app/installation-requests')
      await adminPage.getByRole('button', { name: 'Review dossier ' + receipt.match(/#\d+/)[0], exact: true }).click()
      await expect(adminPage.getByAltText('Evidence image 1')).toBeVisible()
      if (revision === 1) {
        await adminPage.getByLabel('Dossier review note').fill('Clarify the proposed installation location')
        await adminPage.getByRole('button', { name: 'Return dossier', exact: true }).click()
        await expect(adminPage.getByText('Dossier returned for correction.', { exact: true })).toBeVisible()
        await page.reload()
        await page.getByRole('button', { name: 'View proposal revision 1', exact: true }).click()
        await expect(page.getByText('Administrator review: Clarify the proposed installation location', { exact: true })).toBeVisible()
        await expect(page.getByAltText('Evidence image 1')).toBeVisible()
      }
    }
    await expect(adminPage.getByText(/Marked platform two metres east of the irrigation pump/)).toBeVisible()
    await adminPage.getByLabel('Dossier review note').fill('Reviewed survey, proposal and external deposit receipt')
    await adminPage.getByRole('button', { name: 'Approve dossier' }).click()
    const approved = await adminPage.getByText(/Installation request #\d+ approved for scheduling/).innerText()
    await installAndActivate(browser, adminPage, seed, Number(approved.match(/#(\d+)/)[1]), owner, password, farm)
  } finally { await context.close() }
}
