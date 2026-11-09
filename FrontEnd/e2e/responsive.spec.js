import { expect, test } from '@playwright/test'

test('public pages and role navigation remain usable on narrow screens', async ({ browser, request }) => {
  const api = process.env.E2E_API_URL
  if (api !== 'http://127.0.0.1:5189' || !process.env.TEST_POSTGRES_CONNECTION) throw new Error('Use the disposable PostgreSQL harness.')
  const password = process.env.SEED_ADMIN_PASSWORD
  const login = await request.post(`${api}/api/auth/login`, { data: { email: 'admin@sep.local', password: '123456' } })
  expect(login.ok()).toBeTruthy()
  const token = (await login.json()).data.accessToken
  const roles = ['FarmAdministrator', 'FarmOwner', 'Technician', 'FarmEngineer']
  for (const role of roles.slice(1)) {
    const created = await request.post(`${api}/api/users`, { headers: { Authorization: `Bearer ${token}` },
      data: { email: `responsive-${role}@example.test`, fullName: `Responsive ${role}`, password, roles: [role] } })
    expect(created.ok(), `Create ${role}: ${created.status()}`).toBeTruthy()
  }
  for (const width of [390, 768]) {
    const context = await browser.newContext({ baseURL: 'http://127.0.0.1:5179', viewport: { width, height: 844 } })
    try {
      const page = await context.newPage()
      const fits = async () => {
        await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)).toBeLessThanOrEqual(1)
      }
      await page.goto('http://127.0.0.1:5179/')
      await expect(page.locator('h1').first()).toBeVisible()
      await fits()
      for (const role of roles) {
        await context.clearCookies()
        await page.evaluate(() => { localStorage.clear(); sessionStorage.clear() })
        await page.goto('/dang-nhap')
        const admin = role === 'FarmAdministrator'
        await page.getByLabel('Email', { exact: true }).fill(admin ? 'admin@sep.local' : `responsive-${role}@example.test`)
        await page.locator('input[name=password]').fill(admin ? '123456' : password)
        await page.locator('button[type=submit]').click()
        await expect(page).toHaveURL(/\/app\/dashboard$/)
        await expect(page.getByRole('main')).toBeVisible()
        await fits()
        const menu = page.getByRole('button', { name: 'Open navigation', exact: true })
        await menu.click()
        const drawer = page.getByRole('dialog', { name: 'Workspace navigation' })
        await expect(drawer).toBeVisible()
        await page.keyboard.press('Escape')
        await expect(menu).toBeFocused()
        await expect(menu).toHaveAttribute('aria-expanded', 'false')
        await menu.click()
        const engineer = role === 'FarmEngineer'
        await drawer.getByRole('link', { name: engineer ? 'Consultations' : 'Installation jobs', exact: true }).click()
        await expect(page).toHaveURL(engineer ? /\/app\/consultations$/ : /\/app\/installation-jobs$/)
        await expect(menu).toHaveAttribute('aria-expanded', 'false')
        await fits()
      }
    } finally { await context.close() }
  }
})
