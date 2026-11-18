import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render as renderComponent, screen, waitFor, within } from '@testing-library/react'
import ConsultationPage from '../src/pages/ConsultationPage'
import SiteVisitPanel from '../src/components/app/SiteVisitPanel'
import { api } from '../src/services/api/index.js'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import ConsultationsPage, { ConsultationDetailPage } from '../src/pages/app/ConsultationsPage'
import HeroSection from '../src/components/HeroSection'
import { useAuth } from '../src/context/AuthContext'
vi.mock('../src/context/AuthContext', () => ({ useAuth: vi.fn(() => ({ user: { id: 4 }, can: permission => ['consultations.manage', 'consultations.view'].includes(permission) })) }))

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

it('opens all consultations for the administrator and can filter unassigned requests', async () => {
  const request = vi.spyOn(api, 'request').mockImplementation(async (_, { query }) => ({
    items: [],
    pagination: { totalItems: query.inbox ? 7 : 0, totalPages: 1, pageSize: 10 },
  }))
  await useAuth.withImplementation(() => ({ can: () => true }), async () => {
    render(<ConsultationsPage />)
    const all = screen.getByRole('button', { name: 'Tất cả yêu cầu', pressed: true })
    const inbox = screen.getByRole('button', { name: /Chờ phân công/, pressed: false })
    await waitFor(() => expect(inbox.textContent).toContain('7'))
    expect(request).toHaveBeenCalledWith('/api/consultations', expect.objectContaining({ query: { inbox: true, page: 1, pageSize: 1 } }))
    expect(request).toHaveBeenCalledWith('/api/consultations', expect.objectContaining({ query: { inbox: false, page: 1 } }))
    fireEvent.click(inbox)
    expect(inbox.getAttribute('aria-pressed')).toBe('true')
    expect(request).toHaveBeenLastCalledWith('/api/consultations', expect.objectContaining({ query: { inbox: true, page: 1 } }))
    const pending = screen.getByRole('button', { name: 'Chờ tiếp nhận', pressed: false })
    fireEvent.click(pending)
    expect(pending.getAttribute('aria-pressed')).toBe('true')
    expect(request).toHaveBeenLastCalledWith('/api/consultations', expect.objectContaining({ query: { pendingAcceptance: true, page: 1 } }))
    fireEvent.click(all)
    expect(request).toHaveBeenLastCalledWith('/api/consultations', expect.objectContaining({ query: { inbox: false, page: 1 } }))
    await waitFor(() => expect(screen.queryByRole('status')).toBeNull())
  })
})

it('shows assigned consultation details to the engineer', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue({
    items: [{ id: 42, serviceNeed: 'Sensors', status: 'IN_PROGRESS', createdAt: '2026-10-28T00:00:00Z' }],
    pagination: { totalItems: 1 },
  })
  render(<ConsultationsPage />)
  expect((await screen.findByRole('link', { name: 'Xem chi tiết', exact: true })).getAttribute('href')).toBe('/app/consultations/42')
  expect(screen.getByRole('heading', { name: 'Công việc tư vấn được giao' })).toBeTruthy()
  expect(screen.queryByRole('table')).toBeNull()
  expect(screen.getByText('Đang xử lý')).toBeTruthy()
  expect(request).toHaveBeenCalledTimes(1)
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
  expect(screen.queryByRole('button', { name: /Lịch sử & Phân công/ })).toBeNull()
})

it('lets the engineer respond to their assignment before showing consultation controls', async () => {
  const detail = { id: 42, customerName: 'Khách hàng', farmLocation: 'Vườn điều', engineerId: 4,
    status: 'PENDING_ACCEPTANCE', preferredChannel: 'ONLINE', history: [],
    assignment: { id: 9, responseDeadline: '2026-11-19T04:00:00Z' } }
  let accepted = false
  const request = vi.spyOn(api, 'request').mockImplementation(async path => {
    if (path.endsWith('/assignment-response')) { accepted = true; return null }
    if (path.endsWith('/site-visit')) return { revision: 0, channel: null }
    return accepted ? { ...detail, status: 'IN_PROGRESS' } : detail
  })
  renderComponent(<MemoryRouter initialEntries={['/app/consultations/42?step=assignment']}><Routes><Route path="/app/consultations/:id" element={<ConsultationDetailPage />} /></Routes></MemoryRouter>)
  const accept = await screen.findByRole('button', { name: 'Tiếp nhận', exact: true })
  expect(screen.getByText('Vườn điều')).toBeTruthy()
  expect(screen.getByRole('button', { name: 'Từ chối', exact: true })).toBeTruthy()
  expect(screen.queryByRole('button', { name: /Lịch sử & Phân công/ })).toBeNull()
  expect(screen.queryByRole('button', { name: 'Hình thức tư vấn' })).toBeNull()
  expect(request.mock.calls.map(([path]) => path)).toEqual(['/api/consultations/42'])
  fireEvent.click(accept)
  fireEvent.click(await screen.findByRole('button', { name: 'Chuyển sang tư vấn tại vườn' }))
  expect(screen.getByLabelText('Lý do chuyển sang tại vườn')).toBeTruthy()
  expect(screen.queryByRole('button', { name: /Lịch sử & Phân công/ })).toBeNull()
})

it('shows the reassigned engineer and newest named history event after saving', async () => {
  const detail = { id: 42, engineerId: 4, engineerName: 'Nguyễn Văn Bình', customerName: 'Lê Minh An',
    status: 'IN_PROGRESS', history: [
      { id: 1, action: 'SUBMITTED', createdAt: '2026-10-27T00:00:00Z' },
      { id: 2, action: 'CHANNEL_CHANGED', actorName: 'Nguyễn Văn Bình', createdAt: '2026-10-28T00:00:00Z', notes: 'UNSPECIFIED -> ONLINE' },
    ] }
  const request = vi.spyOn(api, 'request').mockImplementation(async path => {
    if (path.endsWith('/assignment-candidates')) return [{ id: 5, fullName: 'Trần Thị Mai', openCount: 1, capacity: 10 }]
    if (path.endsWith('/assign')) return { ...detail, engineerId: 5, engineerName: 'Trần Thị Mai', history: [...detail.history, {
      id: 3, action: 'REASSIGNED', actorName: 'Phạm Văn Nam', previousEngineerName: detail.engineerName,
      assignedEngineerName: 'Trần Thị Mai', createdAt: '2026-10-29T09:27:00Z',
    }] }
    if (path === '/api/consultations/42') return detail
    return { items: [], pagination: { totalItems: 0 } }
  })
  await useAuth.withImplementation(() => ({ user: { id: 1 }, can: () => true }), async () => {
    renderComponent(<MemoryRouter initialEntries={['/app/consultations/42']}><Routes><Route path="/app/consultations/:id" element={<ConsultationDetailPage />} /></Routes></MemoryRouter>)
    fireEvent.click(await screen.findByRole('button', { name: /Lịch sử & Phân công/ }))
    const card = within(await screen.findByRole('region', { name: 'Lịch sử & Phân công tư vấn' }))
    expect(card.getByText('Chưa xác định → Tư vấn trực tuyến')).toBeTruthy()
    fireEvent.click(card.getByRole('button', { name: 'Phân công lại' }))
    fireEvent.click(await card.findByRole('button', { name: 'Gán cho Trần Thị Mai' }))
    expect(await card.findByText('Đã phân công lại kỹ sư phụ trách.')).toBeTruthy()
    expect(card.getByText('Trần Thị Mai', { selector: 'strong' })).toBeTruthy()
    const [latest] = card.getAllByRole('listitem')
    expect(latest.textContent).toContain('29/10/2026 16:27')
    expect(latest.textContent).toContain('Thực hiện bởi: Phạm Văn Nam')
    expect(latest.textContent).toContain('Từ: Nguyễn Văn Bình')
    expect(latest.textContent).toContain('Sang: Trần Thị Mai')
    expect(request).toHaveBeenCalledWith('/api/consultations/42/assign', {
      method: 'POST', body: { engineerId: 5, expectedEngineerId: 4 }, retry: false,
    })
  })
})

it('shows an empty state for an unavailable step and leaves assignment accessible', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue({
    id: 42, customerName: 'Khách hàng', status: 'NEW', history: [],
  })
  await useAuth.withImplementation(() => ({ user: { id: 1 }, can: () => true }), async () => {
    renderComponent(<MemoryRouter initialEntries={['/app/consultations/42?step=farm']}><Routes><Route path="/app/consultations/:id" element={<ConsultationDetailPage />} /></Routes></MemoryRouter>)
    const step = await screen.findByRole('button', { name: 'Nông trại khách hàng' })
    expect(step.getAttribute('aria-expanded')).toBe('true')
    expect(screen.getByText('Không có dữ liệu')).toBeTruthy()
    expect(screen.queryByRole('combobox', { name: 'Nông trại của khách hàng' })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: /Lịch sử & Phân công/ }))
    expect(screen.getByRole('button', { name: 'Mở danh sách phân công' })).toBeTruthy()
    expect(request.mock.calls.map(([path]) => path)).toEqual(['/api/consultations/42'])
  })
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
