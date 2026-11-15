import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render as renderComponent, screen, waitFor } from '@testing-library/react'
import ConsultationPage from '../src/pages/ConsultationPage'
import SiteVisitPanel from '../src/components/app/SiteVisitPanel'
import { api } from '../src/services/api/index.js'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import ConsultationsPage, { ConsultationDetailPage } from '../src/pages/app/ConsultationsPage'
import HeroSection from '../src/components/HeroSection'
vi.mock('../src/context/AuthContext', () => ({ useAuth: () => ({ user: { id: 4 }, can: permission => ['consultations.manage', 'consultations.view'].includes(permission) }) }))

afterEach(() => { cleanup(); vi.restoreAllMocks() })
function render(component) { return renderComponent(<MemoryRouter>{component}</MemoryRouter>) }

it('homepage consultation CTA opens the public request route', () => {
  render(<HeroSection />)
  expect(screen.getByRole('link', { name: 'Tư vấn thiết bị' }).getAttribute('href')).toBe('/tu-van')
})

it('submits an anonymous consultation and displays its receipt', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue({ id: 42, status: 'NEW' })
  render(<ConsultationPage />)
  for (const [label, value] of [['Họ tên *', 'Khách hàng'], ['Số điện thoại *', '0901234567'],
    ['Vị trí nông trại *', 'Cần Thơ'], ['Dịch vụ cần tư vấn *', 'Thuê cảm biến'], ['Mô tả nhu cầu *', 'Theo dõi độ ẩm'], ['Email *', 'customer@example.com'], ['Diện tích (ha) *', '4'], ['Loại cây đang trồng *', 'Cà phê']])
    fireEvent.change(screen.getByLabelText(label), { target: { value } })
  fireEvent.change(screen.getByLabelText(/Hình thức tư vấn/), { target: { value: 'ON_SITE' } })
  fireEvent.click(screen.getByRole('button', { name: 'Gửi yêu cầu' }))
  await waitFor(() => expect(request).toHaveBeenCalledWith('/api/consultations', expect.objectContaining({
    method: 'POST', auth: false, retry: false, toast: true, toastMessage: 'Đã gửi yêu cầu tư vấn.', body: expect.objectContaining({ customerName: 'Khách hàng', phone: '0901234567', farmArea: 4, email: 'customer@example.com', cropType: 'Cà phê', preferredChannel: 'ON_SITE' }),
  })))
  expect(await screen.findByRole('status')).toHaveProperty('textContent', expect.stringContaining('#42'))
  expect(screen.queryByRole('button', { name: 'Gửi yêu cầu' })).toBeNull()
})

it('keeps entered contact data available after a failed submission', async () => {
  vi.spyOn(api, 'request').mockRejectedValue(new Error('Service unavailable'))
  render(<ConsultationPage />)
  fireEvent.change(screen.getByLabelText('Họ tên *'), { target: { value: 'Khách hàng' } })
  fireEvent.submit(screen.getByRole('form', { name: 'Yêu cầu tư vấn' }))
  expect(await screen.findByRole('alert')).toHaveProperty('textContent', 'Service unavailable')
  expect(screen.getByLabelText('Họ tên *').value).toBe('Khách hàng')
})

it('shows assigned consultation details to the engineer', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue({
    items: [{ id: 42, serviceNeed: 'Sensors', status: 'IN_PROGRESS', createdAt: '2026-10-28T00:00:00Z' }],
    pagination: { totalItems: 1 },
  })
  render(<ConsultationsPage />)
  expect((await screen.findByRole('link', { name: 'Mở #42' })).getAttribute('href')).toBe('/app/consultations/42')
  expect(screen.getByText('Đang xử lý')).toBeTruthy()
  expect(request).toHaveBeenCalledWith('/api/consultations', expect.objectContaining({ query: { inbox: false, page: 1 } }))
})

it('records follow-up on the assigned detail and renders persisted history', async () => {
  const detail = { id: 42, engineerId: 4, customerName: 'Customer', phone: '0901234567', farmLocation: 'Can Tho', serviceNeed: 'Sensors', content: 'Monitoring', status: 'IN_PROGRESS', history: [] }
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => path.endsWith('/site-visit') ? { revision: 0, channel: null } : (path.endsWith('/surveys') || path.endsWith('/proposals')) ? { items: [], pagination: { totalItems: 0 } } : options.method === 'POST'
    ? { ...detail, history: [{ id: 1, action: 'FOLLOW_UP', ...options.body, createdAt: '2026-10-28T00:00:00Z' }] } : detail)
  renderComponent(<MemoryRouter initialEntries={['/app/consultations/42']}><Routes><Route path="/app/consultations/:id" element={<ConsultationDetailPage />} /></Routes></MemoryRouter>)
  await screen.findByText('Customer')
  for (const [name, value] of [['Ghi chú', 'Called customer'], ['Kết quả liên hệ', 'Answered'], ['Bước tiếp theo', 'Arrange survey']])
    fireEvent.change(screen.getByLabelText(name), { target: { value } })
  fireEvent.click(screen.getByRole('button', { name: 'Lưu kết quả theo dõi' }))
  await screen.findByText('Đã lưu kết quả theo dõi.')
  expect(request).toHaveBeenCalledWith('/api/consultations/42/follow-ups', { method: 'POST', body: { notes: 'Called customer', contactResult: 'Answered', nextAction: 'Arrange survey' } })
  expect(screen.getByText('Arrange survey')).toBeTruthy()
  expect(screen.getByText('Đã ghi nhận kết quả theo dõi')).toBeTruthy()
})

it.each([
  ['CALCULATED', 0, 'Phí dự kiến 0 đ'],
  ['CALCULATED', 445000, 'Phí dự kiến 445.000 đ'],
  ['PRICING_NOT_CONFIGURED', null, 'Chưa cấu hình biểu phí'],
])('uses server preview %s and keeps inputs on errors', async (status, amountVnd, label) => {
  const request = vi.spyOn(api, 'request').mockImplementation(async path => {
    if (path.endsWith('fee-preview')) return { status, amountVnd, distanceKm: 165, pricingVersion:'test-version' }
    throw new Error('Change requires a fresh preview')
  })
  render(<SiteVisitPanel consultationId={8} editable visit={{ revision: 0, channel: 'ON_SITE', offices: [{ code: 'A', label: 'Trụ sở 1' }],
    tariff: { freeThroughKm: 40, firstTierThroughKm: 100, baseThroughKm: 150, firstTierVnd: 200000, baseVnd: 400000, extraVndPerKm: 3000 } }} />)
  expect(screen.getByText('Cách tính phí để tư vấn cho khách')).toBeTruthy()
  fireEvent.change(screen.getByLabelText('Trụ sở xuất phát'), {target:{value:'A'}})
  fireEvent.change(screen.getByLabelText('Quãng đường một chiều (km)'), {target:{value:'165'}})
  await screen.findByText(label)
  const button = screen.getByRole('button',{name:'Gửi báo cáo và báo giá cho quản trị viên'})
  if (status === 'PRICING_NOT_CONFIGURED') { expect(button.disabled).toBe(true); return }
  fireEvent.change(screen.getByLabelText('Đường dẫn Google Maps của vườn'), {target:{value:'https://maps.app.goo.gl/Visit123'}})
  fireEvent.change(screen.getByLabelText(/Ngày giờ tư vấn tại vườn dự kiến/), {target:{value:'2030-01-01T12:00'}})
  fireEvent.click(button)
  await screen.findByText('Change requires a fresh preview')
  expect(screen.getByLabelText('Quãng đường một chiều (km)').value).toBe('165')
  expect(request).toHaveBeenCalledWith('/api/consultations/8/site-visit/proposals',expect.objectContaining({
    body:expect.objectContaining({pricingVersion:'test-version',expectedRevision:0,distanceKm:165})
  }))
})
