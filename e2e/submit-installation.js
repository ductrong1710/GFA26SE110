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
    await adminPage.goto('/app/consultations/' + consultationId)
    await adminPage.getByRole('button', { name: 'Mở danh sách phân công' }).click()
    await adminPage.getByRole('button', { name: 'Gán cho ' + engineer.fullName, exact: true }).click()
    await expect(adminPage.getByText('Đã phân công nhân viên', { exact: true })).toBeVisible()
    await page.goto('/dang-nhap')
    await page.getByLabel('Email', { exact: true }).fill(engineer.email)
    await page.locator('input[name=password]').fill(password)
    await page.locator('button[type=submit]').click()
    await page.getByRole('link', { name: 'Tư vấn', exact: true }).click()
    await page.getByRole('link', { name: 'Mở #' + consultationId }).click()
    await page.getByRole('button', { name: 'Ghi nhận khách chọn tư vấn trực tuyến' }).click()
    await page.getByLabel('Ghi chú', { exact: true }).fill('Reached customer by phone')
    await page.getByLabel('Kết quả liên hệ').fill('Confirmed orchard visit')
    await page.getByLabel('Bước tiếp theo').fill('Survey irrigation zones')
    await page.getByRole('button', { name: 'Lưu kết quả theo dõi' }).click()
    await expect(page.getByText('Đã lưu kết quả theo dõi.')).toBeVisible()

    await adminPage.goto('/app/consultations/' + consultationId)
    await adminPage.getByRole('combobox', { name: 'Nông trại của khách hàng' }).selectOption(String(farm.id))
    await adminPage.getByRole('combobox', { name: 'Chủ nông trại' }).selectOption(String(owner.id))
    await adminPage.getByRole('button', { name: 'Xác nhận nông trại của khách hàng' }).click()
    await expect(adminPage.getByText('Nông trại đã liên kết #' + farm.id + ' · Chủ nông trại #' + owner.id)).toBeVisible()

    await page.reload()
    await page.getByRole('button', { name: 'Ghi nhận khảo sát' }).click()
    const survey = page.getByRole('form', { name: 'Phiên bản khảo sát' })
    await survey.getByLabel('Tên nông trại').fill(farm.name)
    await survey.getByLabel('Địa điểm nông trại').fill('Đồng Tháp')
    await survey.getByLabel('Nhu cầu giám sát').fill('Measure moisture around roots')
    await survey.getByLabel('Tên khu vực').fill('South orchard')
    await survey.getByLabel('Vị trí lắp đặt đề xuất').fill('Beside irrigation pump')
    await survey.getByRole('button', { name: 'Lưu phiên bản khảo sát' }).click()
    await expect(page.getByText('Đã lưu bản khảo sát số 1.')).toBeVisible()
    for (const revision of [1, 2]) {
      await page.getByRole('button', { name: 'Lập phương án' }).click()
      const proposal = page.getByRole('form', { name: 'Phương án thiết bị' })
      await proposal.getByRole('combobox', { name: 'Phiên bản khảo sát' }).selectOption({ label: 'Phiên bản 1: ' + farm.name })
      await proposal.getByRole('combobox', { name: 'Khu vực khảo sát' }).selectOption({ label: 'South orchard' })
      await proposal.getByLabel('Thời hạn thuê (tháng)').fill('6')
      const installationDate = new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 10)
      await proposal.getByLabel('Thời gian lắp đặt mong muốn').fill(installationDate + 'T09:00')
      await proposal.getByLabel('Loại thiết bị').fill('Monitoring UAV')
      await proposal.getByLabel('Thiết bị cho thuê đã công khai').selectOption(uavProduct)
      await proposal.getByLabel('Vị trí lắp đặt').fill(revision === 1 ? 'Beside irrigation pump' : 'Marked platform two metres east of the irrigation pump')
      await proposal.getByRole('button', { name: 'Thêm thiết bị đề xuất' }).click()
      const second = proposal.getByRole('group', { name: 'Thiết bị 2', exact: true })
      await second.getByRole('combobox', { name: 'Khu vực khảo sát' }).selectOption({ label: 'South orchard' })
      await second.getByLabel('Loại thiết bị').fill('Mobile gateway')
      await second.getByLabel('Thiết bị cho thuê đã công khai').selectOption(gatewayProduct)
      await second.getByLabel('Vị trí lắp đặt').fill('Orchard dock')
      await proposal.getByRole('button', { name: 'Lưu phiên bản phương án' }).click()
      await page.getByLabel('Minh chứng hoặc mã tham chiếu xác nhận').fill('Customer accepted survey proposal')
      await page.getByRole('button', { name: 'Ghi nhận khách hàng xác nhận phương án' }).click()
      await expect(page.getByText('Đã ghi nhận khách hàng xác nhận phương án.')).toBeVisible()
      await page.getByLabel('Minh chứng hoặc mã tham chiếu xác nhận').fill('External deposit receipt R-114')
      await page.getByRole('button', { name: 'Ghi nhận xác nhận tiền đặt cọc bên ngoài hệ thống' }).click()
      await page.getByLabel('Ghi chú hồ sơ').fill('Attached external receipt R-114')
      const dossier = page.getByRole('group', { name: 'Hồ sơ lắp đặt' })
      await dossier.getByLabel('Thêm ảnh minh chứng').setInputFiles({ name: 'receipt.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=', 'base64') })
      await dossier.getByRole('button', { name: 'Gửi yêu cầu lắp đặt' }).click()
      const receipt = await page.getByText(/Đã gửi hồ sơ #\d+ để quản trị viên duyệt/).innerText()
      await adminPage.goto('/app/installation-requests')
      await adminPage.getByRole('button', { name: 'Review dossier ' + receipt.match(/#\d+/)[0], exact: true }).click()
      await expect(adminPage.getByAltText('Evidence image 1')).toBeVisible()
      if (revision === 1) {
        await adminPage.getByLabel('Dossier review note').fill('Clarify the proposed installation location')
        await adminPage.getByRole('button', { name: 'Return dossier', exact: true }).click()
        await expect(adminPage.getByText('Dossier returned for correction.', { exact: true })).toBeVisible()
        await page.reload()
        await page.getByRole('button', { name: 'Xem phiên bản phương án 1', exact: true }).click()
        await expect(page.getByText('Ý kiến của quản trị viên: Clarify the proposed installation location', { exact: true })).toBeVisible()
        await expect(page.getByAltText('Ảnh minh chứng 1')).toBeVisible()
      }
    }
    await expect(adminPage.getByText(/Marked platform two metres east of the irrigation pump/)).toBeVisible()
    await adminPage.getByLabel('Dossier review note').fill('Reviewed survey, proposal and external deposit receipt')
    await adminPage.getByRole('button', { name: 'Approve dossier' }).click()
    const approved = await adminPage.getByText(/Installation request #\d+ approved for scheduling/).innerText()
    await installAndActivate(browser, adminPage, seed, Number(approved.match(/#(\d+)/)[1]), owner, password, farm)
  } finally { await context.close() }
}
