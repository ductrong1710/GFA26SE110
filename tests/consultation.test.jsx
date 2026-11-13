import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import ConsultationPage from '../src/pages/ConsultationPage'
import SiteVisitPanel from '../src/components/app/SiteVisitPanel'
import { api } from '../src/services/api/index.js'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import ConsultationsPage, { ConsultationDetailPage } from '../src/pages/app/ConsultationsPage'
import HeroSection from '../src/components/HeroSection'
vi.mock('../src/context/AuthContext', () => ({ useAuth: () => ({ user: { id: 4 }, can: permission => ['consultations.manage', 'consultations.view'].includes(permission) }) }))

afterEach(() => { cleanup(); vi.restoreAllMocks() })

it('homepage consultation CTA opens the public request route', () => {
  render(<MemoryRouter><HeroSection /></MemoryRouter>)
  expect(screen.getByRole('link', { name: 'Tư vấn thiết bị' }).getAttribute('href')).toBe('/tu-van')
})

it('submits an anonymous consultation and displays its receipt', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue({ id: 42, status: 'NEW' })
  render(<ConsultationPage />)
  for (const [label, value] of [['Họ tên', 'Khách hàng'], ['Số điện thoại', '0901234567'],
    ['Vị trí nông trại', 'Cần Thơ'], ['Nhu cầu dịch vụ', 'Thuê cảm biến'], ['Nội dung tư vấn', 'Theo dõi độ ẩm']])
    fireEvent.change(screen.getByLabelText(label), { target: { value } })
  fireEvent.change(screen.getByLabelText('Hình thức tư vấn'), { target: { value: 'ON_SITE' } })
  fireEvent.click(screen.getByRole('button', { name: 'Gửi yêu cầu tư vấn' }))
  await waitFor(() => expect(request).toHaveBeenCalledWith('/api/consultations', expect.objectContaining({
    method: 'POST', auth: false, toast: true, toastMessage: 'Consultation request submitted successfully.', body: expect.objectContaining({ customerName: 'Khách hàng', phone: '0901234567', farmArea: null }),
  })))
  expect(await screen.findByRole('status')).toHaveProperty('textContent', expect.stringContaining('#42'))
  expect(screen.queryByRole('button', { name: 'Gửi yêu cầu tư vấn' })).toBeNull()
})

it('keeps entered contact data available after a failed submission', async () => {
  vi.spyOn(api, 'request').mockRejectedValue(new Error('Service unavailable'))
  render(<ConsultationPage />)
  fireEvent.change(screen.getByLabelText('Họ tên'), { target: { value: 'Khách hàng' } })
  fireEvent.submit(screen.getByRole('form', { name: 'Yêu cầu tư vấn' }))
  expect(await screen.findByRole('alert')).toHaveProperty('textContent', 'Service unavailable')
  expect(screen.getByLabelText('Họ tên').value).toBe('Khách hàng')
})

it('claims an inbox request then shows assigned work', async () => {
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => {
    if (path.endsWith('/claim')) return { id: 42 }
    return { items: [{ id: 42, serviceNeed: 'Sensors', status: options.query.inbox ? 'NEW' : 'IN_PROGRESS', createdAt: '2026-10-28T00:00:00Z' }], pagination: { totalItems: 1 } }
  })
  render(<MemoryRouter><ConsultationsPage /></MemoryRouter>)
  fireEvent.click(await screen.findByRole('button', { name: 'Claim request #42' }))
  await screen.findByRole('link', { name: 'Open #42' })
  expect(request).toHaveBeenCalledWith('/api/consultations/42/claim', { method: 'POST' })
})

it('records follow-up on the assigned detail and renders persisted history', async () => {
  const detail = { id: 42, engineerId: 4, customerName: 'Customer', phone: '0901234567', farmLocation: 'Can Tho', serviceNeed: 'Sensors', content: 'Monitoring', status: 'IN_PROGRESS', history: [] }
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => (path.endsWith('/surveys') || path.endsWith('/proposals')) ? { items: [], pagination: { totalItems: 0 } } : options.method === 'POST'
    ? { ...detail, history: [{ id: 1, action: 'FOLLOW_UP', ...options.body, createdAt: '2026-10-28T00:00:00Z' }] } : detail)
  render(<MemoryRouter initialEntries={['/app/consultations/42']}><Routes><Route path="/app/consultations/:id" element={<ConsultationDetailPage />} /></Routes></MemoryRouter>)
  await screen.findByText('Customer')
  for (const [name, value] of [['Notes', 'Called customer'], ['Contact result', 'Answered'], ['Next action', 'Arrange survey']])
    fireEvent.change(screen.getByLabelText(name), { target: { value } })
  fireEvent.click(screen.getByRole('button', { name: 'Save follow-up' }))
  await screen.findByText('Follow-up recorded.')
  expect(request).toHaveBeenCalledWith('/api/consultations/42/follow-ups', { method: 'POST', body: { notes: 'Called customer', contactResult: 'Answered', nextAction: 'Arrange survey' } })
  expect(screen.getByText('Arrange survey')).toBeTruthy()
})

it.each([
  ['CALCULATED', 0, '0 đ'],
  ['MANUAL_QUOTE_REQUIRED', null, 'Cần báo giá riêng'],
  ['PRICING_NOT_CONFIGURED', null, 'Chưa cấu hình biểu phí'],
])('uses server preview %s and keeps inputs on errors', async (status, amountVnd, label) => {
  const request = vi.spyOn(api, 'request').mockImplementation(async path => {
    if (path.endsWith('fee-preview')) return { status, amountVnd, pricingVersion:'test-version' }
    throw new Error('Change requires a fresh preview')
  })
  render(<SiteVisitPanel consultationId={8} editable visit={{revision:0, channel:'ON_SITE', offices:[{code:'A',label:'Trụ sở 1'}]}} />)
  fireEvent.change(screen.getByLabelText('Trụ sở'), {target:{value:'A'}})
  fireEvent.change(screen.getByLabelText('Quãng đường một chiều (km)'), {target:{value:'165'}})
  await screen.findByText(label)
  const button = screen.getByRole('button',{name:'Gửi duyệt phí / lịch'})
  if (status === 'PRICING_NOT_CONFIGURED') { expect(button.disabled).toBe(true); return }
  fireEvent.change(screen.getByLabelText('Đường dẫn Google Maps'), {target:{value:'https://maps.app.goo.gl/Visit123'}})
  fireEvent.change(screen.getByLabelText('Lịch khảo sát dự kiến'), {target:{value:'2030-01-01T12:00'}})
  if (status === 'MANUAL_QUOTE_REQUIRED') {
    fireEvent.change(screen.getByLabelText('Báo giá riêng (VND)'), {target:{value:'550000'}})
    fireEvent.change(screen.getByLabelText('Lý do báo giá riêng'), {target:{value:'Đường xa'}})
  }
  fireEvent.click(button)
  await screen.findByText('Change requires a fresh preview')
  expect(screen.getByLabelText('Quãng đường một chiều (km)').value).toBe('165')
  expect(request).toHaveBeenCalledWith('/api/consultations/8/site-visit/proposals',expect.objectContaining({
    body:expect.objectContaining({pricingVersion:'test-version',expectedRevision:0,distanceKm:165})
  }))
})
