import React from 'react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { api } from '../src/services/api'
import InstallationDossierPanel from '../src/components/workflow/InstallationDossierPanel'

vi.mock('../src/services/api', () => ({ api: { post: vi.fn() } }))
const proposal = { id: 7, revision: 2, status: 'DRAFT', depositBills: [] }
const lead = { customerName: 'Chủ vườn', email: 'garden@example.com' }
const renderPanel = props => render(<MemoryRouter><Routes>
  <Route path="/" element={<InstallationDossierPanel proposal={proposal} lead={lead} {...props} />} />
  <Route path="/app/installation-requests/:id" element={<p>Phân công KTV</p>} />
</Routes></MemoryRouter>)
beforeEach(() => { vi.resetAllMocks(); api.post.mockResolvedValue({ installationRequestId: 19 }) })
afterEach(cleanup)

it('requires the engineer to enter the customer and attach a bill before sending', async () => {
  const onDone = vi.fn()
  renderPanel({ engineer: true, farmLinked: false, onDone })
  const button = screen.getByRole('button', { name: 'Gửi hồ sơ và bill cho Admin' })
  expect(button.disabled).toBe(true)
  expect(screen.getByLabelText('Họ tên khách hàng đã chốt').required).toBe(true)
  expect(screen.getByLabelText('Email khách hàng').required).toBe(true)
  fireEvent.change(screen.getByLabelText('Thông tin đã chốt / ghi chú cọc bên ngoài'), { target: { value: 'Đã chốt qua điện thoại và nhận cọc' } })
  fireEvent.change(screen.getByLabelText(/Bill cọc bên ngoài/), { target: { files: [new File([new Uint8Array([255, 216, 255, 217])], 'bill.jpg', { type: 'image/jpeg' })] } })
  await waitFor(() => expect(button.disabled).toBe(false))
  fireEvent.submit(button.closest('form'))
  await waitFor(() => expect(api.post).toHaveBeenCalledWith('/api/installation-dossiers/7/submit', {
    expectedRevision: 2, note: 'Đã chốt qua điện thoại và nhận cọc', customer: { fullName: 'Chủ vườn', email: 'garden@example.com' },
    bills: [{ contentType: 'image/jpeg', dataBase64: '/9j/2Q==' }],
  }))
  expect(onDone).toHaveBeenCalled()
  expect(screen.queryByRole('button', { name: /Ghi nhận khách đồng ý|Ghi nhận cọc bên ngoài/ })).toBeNull()
})
it('gives admin one approval step before navigating to technician assignment', async () => {
  renderPanel({ admin: true, farmLinked: true, proposal: { ...proposal, status: 'WAITING_ADMIN' } })
  expect(screen.queryByLabelText('Email khách hàng')).toBeNull()
  const button = screen.getByRole('button', { name: 'Duyệt hồ sơ để phân công KTV' })
  fireEvent.change(screen.getByLabelText('Kết quả đối chiếu hồ sơ và bill'), { target: { value: 'Khách, vườn và bill đều khớp' } })
  fireEvent.submit(button.closest('form'))
  await screen.findByText('Phân công KTV')
  expect(api.post).toHaveBeenCalledWith('/api/installation-dossiers/7/review', { expectedRevision: 2, approve: true, note: 'Khách, vườn và bill đều khớp' })
})
it('keeps pending dossiers read-only for the engineer', () => {
  renderPanel({ engineer: true, farmLinked: true, proposal: { ...proposal, status: 'WAITING_ADMIN' } })
  expect(screen.queryByRole('button', { name: 'Gửi hồ sơ và bill cho Admin' })).toBeNull()
  expect(screen.queryByRole('button', { name: 'Duyệt hồ sơ để phân công KTV' })).toBeNull()
  expect(screen.getByText(/Đã gửi hồ sơ, đang chờ Admin/)).toBeTruthy()
})
it('preserves returned evidence and asks for a new version instead of overwriting the old bill', () => {
  renderPanel({ engineer: true, farmLinked: true, proposal: { ...proposal, status: 'RETURNED', dossierReviewNote: 'Chụp rõ bill', depositBills: [{ contentType: 'image/jpeg', dataBase64: '/9j/2Q==' }] } })
  expect(screen.getByText(/Admin phản hồi: Chụp rõ bill/)).toBeTruthy()
  expect(screen.getByText(/Phiên bản cũ được giữ để đối chiếu/)).toBeTruthy()
  expect(screen.queryByRole('button', { name: 'Gửi hồ sơ và bill cho Admin' })).toBeNull()
})
