import { test, expect } from '@playwright/test'

test('Owner reports symptoms, Engineer requests information, recommends and resolves', async ({ browser, request }) => {
  const api = process.env.E2E_API_URL
  if (api !== 'http://127.0.0.1:5189' || !process.env.TEST_POSTGRES_CONNECTION) throw new Error('Use the disposable PostgreSQL browser harness.')
  const password = process.env.SEED_ADMIN_PASSWORD
  async function post(path, body, token) {
    const response = await request.post(`${api}${path}`, { data: body, headers: token ? { Authorization: `Bearer ${token}` } : {} })
    expect(response.ok(), `${path}: ${response.status()}`).toBeTruthy()
    return (await response.json()).data
  }
  // The disposable database includes the explicitly retained sample-admin migration.
  const admin = await post('/api/auth/login', { email: 'admin@sep.local', password: '123456' })
  const owner = await post('/api/users', { email: 'browser-owner@example.com', fullName: 'Browser Owner', password, roles: ['FarmOwner'] }, admin.accessToken)
  await post('/api/users', { email: 'browser-engineer@example.com', fullName: 'Browser Engineer', password, roles: ['FarmEngineer'] }, admin.accessToken)
  const farm = await post('/api/farms', { name: 'Browser rice farm' }, admin.accessToken)
  await post(`/api/farms/${farm.id}/members`, { userId: owner.id }, admin.accessToken)
  async function login(email) {
    const context = await browser.newContext({ baseURL: 'http://127.0.0.1:5179' })
    const page = await context.newPage()
    await page.goto('/dang-nhap')
    await page.getByLabel('Email', { exact: true }).fill(email)
    await page.locator('input[name="password"]').fill(password)
    await page.locator('button[type="submit"]').click()
    await expect(page).toHaveURL(/\/app/)
    await page.getByRole('link', { name: 'Crop support', exact: true }).click()
    return { context, page }
  }
  const ownerUi = await login(owner.email)
  const engineerUi = await login('browser-engineer@example.com')
  try {
    const page = ownerUi.page
    await page.getByRole('combobox', { name: 'Farm', exact: true }).selectOption(String(farm.id))
    await page.getByLabel('Crop', { exact: true }).fill('Rice')
    await page.getByLabel('Symptoms', { exact: true }).fill('Yellow leaves observed this morning')
    await page.getByRole('button', { name: 'Submit crop report', exact: true }).click()
    await expect(page.getByRole('heading', { name: /Crop report #/ })).toBeVisible()
    const heading = await page.getByRole('heading', { name: /Crop report #/ }).innerText()
    const id = Number(heading.match(/#(\d+)/)[1])
    const engineer = engineerUi.page
    await engineer.reload()
    await engineer.getByRole('button', { name: `Open crop report #${id}` }).click()
    await engineer.getByRole('button', { name: 'Accept crop report' }).click()
    await engineer.getByLabel('Consultation note').fill('Please describe irrigation')
    await engineer.getByRole('button', { name: 'Request additional information' }).click()
    await expect(engineer.getByLabel('Consultation note')).toHaveCount(0)
    await page.getByRole('button', { name: 'Reload report' }).click()
    await page.getByLabel('Consultation note').fill('Watered yesterday')
    await page.getByRole('button', { name: 'Send additional information' }).click()
    await expect(page.getByLabel('Consultation note')).toHaveCount(0)
    await engineer.getByRole('button', { name: 'Reload report' }).click()
    await engineer.getByLabel('Consultation note').fill('Inspect roots and adjust irrigation after inspection')
    await engineer.getByLabel('Recommend an on-site survey').check()
    await engineer.getByRole('button', { name: 'Record recommendation' }).click()
    await expect(engineer.getByText('On-site survey recommended.', { exact: true })).toBeVisible()
    await engineer.getByLabel('Consultation note').fill('Advice provided; case resolved')
    await engineer.getByRole('button', { name: 'Resolve report' }).click()
    await expect(engineer.getByLabel('Consultation note')).toHaveCount(0)
    await page.getByRole('button', { name: 'Reload report' }).click()
    await expect(page.getByText('Advice provided; case resolved', { exact: true })).toBeVisible()
    await expect(page.getByText('Watered yesterday', { exact: true })).toBeVisible()
  } finally { await ownerUi.context.close(); await engineerUi.context.close() }
})
