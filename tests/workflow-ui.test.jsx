import React from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom'
import { AuthContext } from '../src/context/AuthContext'
import { api } from '../src/services/api'
import { allPages, safeContact, chartRows } from '../src/services/api/workflow'
import LiveHomepage from '../src/components/LiveHomepage'
import AssignmentForm from '../src/components/workflow/AssignmentForm'
import ServicePlanPanel from '../src/components/workflow/ServicePlanPanel'
import InstallationReportForm from '../src/components/workflow/InstallationReportForm'
import { NewSupportForm } from '../src/components/workflow/SupportForms'
import { readEvidence } from '../src/components/workflow/EvidenceUpload'

vi.mock('../src/services/api', () => ({ api: { get: vi.fn(), post: vi.fn(), put: vi.fn(), request: vi.fn() } }))
const page = items => ({ items, pagination: { page: 1, totalPages: 1, totalItems: items.length } })
const wrapper = children => <AuthContext.Provider value={{ activeRole: 'ADMINISTRATOR' }}><MemoryRouter>{children}</MemoryRouter></AuthContext.Provider>
beforeEach(() => { vi.resetAllMocks(); api.request.mockResolvedValue(page([])); api.get.mockResolvedValue([]); api.post.mockResolvedValue({}); api.put.mockResolvedValue({}) })
afterEach(cleanup)

describe('new workflow contracts', () => {
  it('loads every backend page without silently losing products', async () => {
    api.request.mockResolvedValueOnce({ items: [{ id: 1 }], pagination: { totalPages: 2 } }).mockResolvedValueOnce({ items: [{ id: 2 }], pagination: { totalPages: 2 } })
    expect(await allPages('/api/public-content', {}, undefined, false)).toEqual([{ id: 1 }, { id: 2 }])
    expect(api.request.mock.calls[1][1]).toMatchObject({ auth: false, query: { page: 2, pageSize: 100 } })
  })
  it('rejects unsafe contact links and never invents chart counts', () => {
    expect(safeContact('javascript:alert(1)')).toBeNull()
    expect(safeContact('http://unsafe.test')).toBeNull()
    expect(safeContact('https://zalo.me/0900000000')).toBeTruthy()
    expect(chartRows([{ date: '2026-11-03', status: 'CLOSED', count: 2 }, { date: '2026-11-03', status: 'CLOSED', count: 3 }])).toEqual([{ date: '2026-11-03', CLOSED: 5 }])
    expect(chartRows([])).toEqual([])
  })
  it('submits a homepage request anonymously and shows success only after saving', async () => {
    render(wrapper(<LiveHomepage />))
    fireEvent.change(screen.getByLabelText('Họ và tên'), { target: { value: 'Khách kiểm thử' } })
    fireEvent.change(screen.getByLabelText('Số điện thoại'), { target: { value: '0900000000' } })
    fireEvent.change(screen.getByLabelText('Địa điểm nông trại'), { target: { value: 'Nông trại kiểm thử' } })
    fireEvent.change(screen.getByLabelText('Bạn muốn giám sát điều gì?'), { target: { value: 'Độ ẩm' } })
    fireEvent.change(screen.getByLabelText('Nội dung tư vấn'), { target: { value: 'Tư vấn thiết bị' } })
    fireEvent.submit(screen.getByRole('button', { name: 'Gửi yêu cầu tư vấn →' }).closest('form'))
    await screen.findByText('Đã gửi yêu cầu tư vấn')
    expect(api.request).toHaveBeenCalledWith('/api/consultations', expect.objectContaining({ method: 'POST', auth: false, body: expect.objectContaining({ customerName: 'Khách kiểm thử', email: null, farmArea: null }) }))
  })
  it('does not let an admin assign a technician when no free appointment exists', async () => {
    api.get.mockResolvedValue({ technicians: [], recommended: null })
    render(wrapper(<AssignmentForm path="/api/installation-requests/1/jobs" />))
    await screen.findByText(/Không có KTV rảnh/)
    expect(screen.getByRole('button', { name: 'Xác nhận phân công' }).disabled).toBe(true)
    expect(api.post).not.toHaveBeenCalled()
  })
  it('uses the job revision when the admin approves a replacement plan', async () => {
    api.get.mockResolvedValue([{ id: 8, status: 'PROPOSED', totalVnd: 0, priceLinesJson: '[]', evidenceJson: '{}' }])
    const done = vi.fn()
    render(wrapper(<ServicePlanPanel job={{ id: 2, revision: 7, status: 'IN_PROGRESS' }} admin onDone={done} />))
    await screen.findByText('Phương án #8')
    const button = screen.getByRole('button', { name: 'Duyệt phương án trước khi làm' })
    fireEvent.change(button.closest('form').querySelector('input'), { target: { value: 'Đã kiểm tra bảo hành' } })
    fireEvent.submit(button.closest('form'))
    await waitFor(() => expect(api.post).toHaveBeenCalledWith('/api/operations/service-plans/8/review', { expectedRevision: 7, approve: true, note: 'Đã kiểm tra bảo hành' }))
    expect(done).toHaveBeenCalled()
  })
  it('requires signed contract and paper handover before installation report submission', async () => {
    api.get.mockResolvedValue([{ id: 1, proposalLineId: 1, uavId: 9, name: 'UAV', code: 'UAV-9' }])
    render(wrapper(<InstallationReportForm job={{ id: 3, revision: 4 }} />))
    await screen.findByText('UAV · UAV-9')
    expect(screen.getByRole('button', { name: 'Gửi báo cáo chờ Admin duyệt' }).disabled).toBe(true)
    expect(screen.queryByText(/ký trên App/i)).toBeNull()
    expect(screen.getByLabelText(/Hợp đồng giấy có chữ ký/).required).toBe(true)
  })
  it('allows an owner to report missing data without a serial or device code', async () => {
    render(<MemoryRouter><Routes><Route element={<Outlet context={{ farmWorkspace: { farms: [{ id: 12, name: 'Vườn kiểm thử' }] } }} />}><Route path="/" element={<NewSupportForm type="device" />} /></Route></Routes></MemoryRouter>)
    fireEvent.change(screen.getByLabelText('Nông trại'), { target: { value: '12' } })
    fireEvent.change(screen.getByLabelText('Hiện tượng bạn đang gặp'), { target: { value: 'Không nhận được dữ liệu cảm biến' } })
    fireEvent.submit(screen.getByRole('button', { name: 'Gửi báo cáo hỗ trợ' }).closest('form'))
    await waitFor(() => expect(api.post).toHaveBeenCalledWith('/api/device-incidents', expect.objectContaining({ farmId: 12, rentalDeviceAssignmentId: null, description: 'Không nhận được dữ liệu cảm biến', images: [] })))
    expect(screen.queryByLabelText(/serial|mã máy/i)).toBeNull()
  })
  it('rejects oversized and unsupported paper uploads before reading', async () => {
    await expect(readEvidence([new File(['pdf'], 'contract.pdf', { type: 'application/pdf' })])).rejects.toThrow('JPEG/PNG')
    await expect(readEvidence(Array.from({ length: 6 }, () => new File(['x'], 'a.png', { type: 'image/png' })))).rejects.toThrow('5 ảnh')
  })
})
