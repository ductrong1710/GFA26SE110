import { expect, test } from '@playwright/test'
import { existsSync, readFileSync } from 'node:fs'

test.use({ actionTimeout: 15000, trace: 'off', screenshot: 'off' })
test('owner changes a password and recovers the account with a single-use emailed code', async ({ page, request }) => {
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
  await page.getByRole('button', { name: /^Account:/ }).click()
  await page.getByRole('button', { name: 'Đăng xuất' }).click()
  await page.getByRole('link', { name: 'Quên mật khẩu?' }).click()
  await page.getByLabel('Email', { exact: true }).fill(email)
  await page.getByRole('button', { name: 'Gửi hướng dẫn' }).click()
  await expect(page).toHaveURL(/\/dat-lai-mat-khau$/)
  const mailbox = process.env.E2E_RESET_MAIL
  if (!mailbox) throw new Error('Local password test mailbox is required.')
  await expect.poll(() => existsSync(mailbox), { timeout: 15000 }).toBe(true)
  const message = readFileSync(mailbox, 'utf8')
  const body = message.slice(message.indexOf('\n\n') + 2)
  const decoded = /^Content-Transfer-Encoding: base64$/im.test(message) ? Buffer.from(body, 'base64').toString()
    : body.replace(/=\n/g, '').replace(/=([0-9a-f]{2})/gi, (_, hex) => String.fromCharCode(parseInt(hex, 16)))
  const code = decoded.match(/\b[A-F0-9]{64}\b/)?.[0]
  if (!code) throw new Error('Recovery email did not contain a reset code.')
  const recovered = password + '-recovered'
  await page.getByLabel('Mã khôi phục').fill(code)
  await page.locator('input[name=newPassword]').fill(recovered)
  await page.locator('input[name=confirm]').fill(recovered)
  await page.getByRole('button', { name: 'Đặt lại mật khẩu', exact: true }).click()
  await expect(page).toHaveURL(/\/dang-nhap$/)
  const replay = await request.post(api + '/api/auth/reset-password', { data: { token: code, newPassword: password } })
  expect(replay.status()).toBe(400)
  await page.getByLabel('Email', { exact: true }).fill(email)
  await page.locator('input[name=password]').fill(replacement)
  await page.locator('button[type=submit]').click()
  await expect(page.getByRole('alert')).toBeVisible()
  await page.locator('input[name=password]').fill(recovered)
  await page.locator('button[type=submit]').click()
  await expect(page).toHaveURL(/\/app\/dashboard$/)
})
