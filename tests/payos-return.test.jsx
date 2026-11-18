import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import App from '../src/App'
import { api } from '../src/services/api'

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.useRealTimers() })
beforeEach(() => { vi.spyOn(window, 'scrollTo').mockImplementation(() => {}) })
const token = 'a'.repeat(64)
function openReturn(path = '/payments/payos/return', query = 'token=' + token) {
  return render(<MemoryRouter initialEntries={[path + '?' + query]}><App /></MemoryRouter>)
}

it('uses only the opaque token and never trusts forged success query parameters', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue({ status: 'PENDING' })
  openReturn('/payments/payos/return', `token=${token}&code=00&orderCode=123&amount=100000`)
  expect(await screen.findByText('Đang chờ PayOS xác nhận giao dịch.')).toBeTruthy()
  expect(request).toHaveBeenCalledExactlyOnceWith(`/api/payments/payos/public/${token}`, expect.objectContaining({ method: 'GET', auth: false, retry: false }))
  expect(screen.queryByText(/123|100000/)).toBeNull()
})

it.each(['', 'token=forged', 'code=00'])('does not request status without a valid token: %s', query => {
  const request = vi.spyOn(api, 'request')
  openReturn('/payments/payos/return', query)
  expect(screen.getByRole('alert').textContent).toContain('không hợp lệ')
  expect(request).not.toHaveBeenCalled()
})

it('shows success only after backend status confirms payment', async () => {
  vi.spyOn(api, 'request').mockResolvedValue({ status: 'SUCCEEDED' })
  openReturn('/payments/payos/return', `token=${token}&code=00`)
  expect(await screen.findByText('PayOS đã xác nhận thanh toán thành công.')).toBeTruthy()
})

it('shows cancellation as incomplete and checks actual backend status', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue({ status: 'PENDING' })
  openReturn('/payments/payos/cancel')
  expect(await screen.findByText(/quay lại trước khi thanh toán hoàn tất/)).toBeTruthy()
  expect(request).toHaveBeenCalledWith(`/api/payments/payos/public/${token}`, expect.objectContaining({ auth: false }))
})

it('polls pending with a bounded number of requests and allows manual refresh', async () => {
  vi.useFakeTimers()
  const request = vi.spyOn(api, 'request').mockResolvedValue({ status: 'PENDING' })
  openReturn()
  await act(async () => { await vi.advanceTimersByTimeAsync(60000) })
  expect(request).toHaveBeenCalledTimes(12)
  fireEvent.click(screen.getByRole('button', { name: 'Cập nhật trạng thái' }))
  await act(async () => {})
  expect(request).toHaveBeenCalledTimes(13)
})

it.each(['FAILED', 'EXPIRED', 'RECONCILIATION_REQUIRED'])('never displays success for %s', async status => {
  vi.spyOn(api, 'request').mockResolvedValue({ status })
  openReturn()
  expect(await screen.findByRole('status')).toBeTruthy()
  expect(screen.queryByText('PayOS đã xác nhận thanh toán thành công.')).toBeNull()
})
