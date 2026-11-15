import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import App from '../src/App'
import { api } from '../src/services/api'

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.useRealTimers() })
beforeEach(() => { vi.spyOn(window, 'scrollTo').mockImplementation(() => {}) })
const token = 'a'.repeat(64)
function openReturn(query = 'token=' + token) {
  return render(<MemoryRouter initialEntries={['/payments/vnpay/return?' + query]}><App /></MemoryRouter>)
}

it('uses only the token in an anonymous GET despite forged success return parameters', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue({ status: 'PENDING', sandbox: true })
  openReturn(`token=${token}&vnp_ResponseCode=00&vnp_TransactionStatus=00&consultationId=123&amount=100000`)
  expect(await screen.findByText('Đang xác nhận thanh toán')).toBeTruthy()
  expect(request).toHaveBeenCalledExactlyOnceWith('/api/payments/vnpay/status', expect.objectContaining({ method: 'GET', auth: false, query: { token }, retry: false }))
  expect(screen.queryByText('Thanh toán thử nghiệm thành công')).toBeNull()
  expect(screen.queryByText(/123|100000/)).toBeNull()
  expect(screen.getByText(/Chỉ dùng thử nghiệm/)).toBeTruthy()
})

it.each(['', 'token=forged', 'vnp_ResponseCode=00'])('does not request status without a valid opaque token: %s', query => {
  const request = vi.spyOn(api, 'request')
  openReturn(query)
  expect(screen.getByRole('alert').textContent).toContain('không hợp lệ')
  expect(request).not.toHaveBeenCalled()
})

it('polls pending without overlap, stops on a terminal status and supports manual refresh', async () => {
  vi.useFakeTimers()
  const request = vi.spyOn(api, 'request').mockResolvedValueOnce({ status: 'PENDING', sandbox: true }).mockResolvedValue({ status: 'SUCCEEDED', sandbox: true })
  openReturn()
  await act(async () => {})
  expect(screen.getByText('Đang xác nhận thanh toán')).toBeTruthy()
  await act(async () => { await vi.advanceTimersByTimeAsync(5000) })
  expect(screen.getByText('Thanh toán thử nghiệm thành công')).toBeTruthy()
  await act(async () => { await vi.advanceTimersByTimeAsync(60000) })
  expect(request).toHaveBeenCalledTimes(2)
  fireEvent.click(screen.getByRole('button', { name: 'Cập nhật trạng thái' }))
  await act(async () => {})
  expect(request).toHaveBeenCalledTimes(3)
})

it('bounds pending polling and cancels requests when leaving the page', async () => {
  vi.useFakeTimers()
  const request = vi.spyOn(api, 'request').mockResolvedValue({ status: 'PENDING', sandbox: true })
  const view = openReturn()
  await act(async () => { await vi.advanceTimersByTimeAsync(120000) })
  expect(request).toHaveBeenCalledTimes(12)
  const signal = request.mock.calls.at(-1)[1].signal
  view.unmount()
  expect(signal.aborted).toBe(true)
  await act(async () => { await vi.advanceTimersByTimeAsync(60000) })
  expect(request).toHaveBeenCalledTimes(12)
})

it('waits for each response before scheduling the next request', async () => {
  vi.useFakeTimers()
  let resolve
  const request = vi.spyOn(api, 'request').mockReturnValue(new Promise(done => { resolve = done }))
  openReturn()
  await act(async () => { await vi.advanceTimersByTimeAsync(30000) })
  expect(request).toHaveBeenCalledTimes(1)
  expect(screen.getByRole('button', { name: 'Cập nhật trạng thái' }).disabled).toBe(true)
  await act(async () => { resolve({ status: 'FAILED', sandbox: true }) })
  expect(screen.getByText('Thanh toán thử nghiệm không thành công')).toBeTruthy()
})

it('allows retry after a status lookup error without trusting return parameters', async () => {
  const request = vi.spyOn(api, 'request').mockRejectedValueOnce(new Error('Không thể tra cứu.')).mockResolvedValue({ status: 'PENDING', sandbox: true })
  openReturn(`token=${token}&vnp_ResponseCode=00`)
  await screen.findByText('Không thể tra cứu.')
  expect(screen.queryByText('Thanh toán thử nghiệm thành công')).toBeNull()
  fireEvent.click(screen.getByRole('button', { name: 'Cập nhật trạng thái' }))
  await act(async () => {})
  expect(request).toHaveBeenCalledTimes(2)
})

it.each(['FAILED', 'UNKNOWN', 'RECONCILIATION_REQUIRED'])('never displays success for %s', async status => {
  vi.spyOn(api, 'request').mockResolvedValue({ status, sandbox: true })
  openReturn()
  expect(await screen.findByRole('status')).toBeTruthy()
  expect(screen.queryByText('Thanh toán thử nghiệm thành công')).toBeNull()
  expect(screen.getByRole('button', { name: 'Cập nhật trạng thái' })).toBeTruthy()
})
