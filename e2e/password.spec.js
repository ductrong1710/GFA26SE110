import { expect, test } from '@playwright/test'

test.use({ actionTimeout: 15000 })
test('owner changes password and can sign in only with the replacement', async ({ page, request }) => {
  const api = process.env.E2E_API_URL
  if (api !== 'http://127.0.0.1:5189' || !process.env.TEST_POSTGRES_CONNECTION) throw new Error('Use the disposable PostgreSQL harness.')
  const login = await request.post(api + '/api/auth/login', { data: { email: 'admin@sep.local', password: '123456' } })
  expect(login.ok()).toBeTruthy()
  const token = (await login.json()).data.accessToken
  const email = 'password-browser@example.com'
  const password = process.env.SEED_ADMIN_PASSWORD
  const created = await request.post(api + '/api/users', { data: { email, password, fullName: 'Password Browser', roles: ['FarmOwner'] }, headers: { Authorization: 'Bearer ' + token } })
  expect(created.ok()).toBeTruthy()
  await page.goto('/dang-nhap')
  await page.getByLabel('Email', { exact: true }).fill(email)
  await page.locator('input[name=password]').fill(password)
  await page.locator('button[type=submit]').click()
  await page.getByRole('button', { name: /^Account:/ }).click()
  await page.getByRole('link', { name: 'Đổi mật khẩu' }).click()
  const replacement = password + '-updated'
  await page.locator('input[name=currentPassword]').fill(password)
  await page.locator('input[name=newPassword]').fill(replacement)
  await page.locator('input[name=confirm]').fill(replacement)
  await page.getByRole('button', { name: 'Đổi mật khẩu', exact: true }).click()
  await expect(page).toHaveURL(/\/dang-nhap$/)
  await page.getByLabel('Email', { exact: true }).fill(email)
  await page.locator('input[name=password]').fill(password)
  await page.locator('button[type=submit]').click()
  await expect(page.getByRole('alert')).toBeVisible()
  await page.locator('input[name=password]').fill(replacement)
  await page.locator('button[type=submit]').click()
  await expect(page).toHaveURL(/\/app\/dashboard$/)
})
