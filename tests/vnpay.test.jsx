import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import SiteVisitPanel from '../src/components/app/SiteVisitPanel'
import { api } from '../src/services/api'

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); vi.useRealTimers() })
const visit = { revision: 2, channel: 'ON_SITE', vnpayEnabled: true,
  current: { revision: 2, channel: 'ON_SITE', reviewStatus: 'APPROVED', paymentStatus: 'UNPAID', amountVnd: 100000 } }
const checkout = { checkoutUrl: 'https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_TxnRef=test', txnRef: 'test', status: 'PENDING', expiresAt: '2030-01-01T12:00:00Z', sandbox: true }

it('creates and copies a sandbox checkout without navigating or confirming payment', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue(checkout)
  const writeText = vi.fn().mockResolvedValue()
  vi.stubGlobal('navigator', { clipboard: { writeText } })
  render(<SiteVisitPanel consultationId={8} visit={visit} admin onSaved={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Tạo link VNPAY Sandbox' }))
  await screen.findByDisplayValue(checkout.checkoutUrl)
  expect(screen.getByText(/Chỉ dùng thử nghiệm/)).toBeTruthy()
  expect(request).toHaveBeenCalledExactlyOnceWith('/api/consultations/8/site-visit/payments/vnpay/checkout', expect.objectContaining({ method: 'POST', body: { expectedRevision: 2 }, retry: false }))
  expect(screen.getByLabelText('Số tiền thực nhận (VND)').closest('fieldset').disabled).toBe(true)
  fireEvent.click(screen.getByRole('button', { name: 'Sao chép link' }))
  await waitFor(() => expect(writeText).toHaveBeenCalledWith(checkout.checkoutUrl))
  expect(request).toHaveBeenCalledTimes(1)
})

it.each(['PENDING', 'UNKNOWN', 'RECONCILIATION_REQUIRED'])('blocks manual confirmation during %s', status => {
  const request = vi.spyOn(api, 'request')
  render(<SiteVisitPanel consultationId={8} visit={{ ...visit, current: { ...visit.current, vnpay: { ...checkout, status } } }} admin />)
  expect(screen.getByLabelText('Số tiền thực nhận (VND)').closest('fieldset').disabled).toBe(true)
  expect(screen.getByRole('button', { name: 'Tạo link VNPAY Sandbox' }).disabled).toBe(true)
  fireEvent.submit(screen.getByLabelText('Mã tham chiếu').closest('form'))
  expect(request).not.toHaveBeenCalled()
})

it('retains the manual bill flow after a failed VNPAY transaction', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue(visit)
  render(<SiteVisitPanel consultationId={8} visit={{ ...visit, current: { ...visit.current, vnpay: { ...checkout, status: 'FAILED' } } }} admin onSaved={vi.fn()} />)
  expect(screen.getByLabelText('Mã tham chiếu').closest('fieldset').disabled).toBe(false)
  for (const [label, value] of [['Số tiền thực nhận (VND)', '100000'], ['Thời điểm giao dịch', '2030-01-01T12:00'], ['Mã tham chiếu', 'bank-transfer']])
    fireEvent.change(screen.getByLabelText(label), { target: { value } })
  fireEvent.change(screen.getByLabelText('Thêm ảnh minh chứng'), { target: { files: [new File(['bill'], 'bill.png', { type: 'image/png' })] } })
  await screen.findByAltText('Ảnh minh chứng 1')
  fireEvent.click(screen.getByRole('button', { name: 'Xác nhận đã nhận tiền' }))
  await waitFor(() => expect(request).toHaveBeenCalledWith('/api/consultations/8/site-visit/payments/confirm', expect.objectContaining({ method: 'POST', body: expect.objectContaining({ expectedRevision: 2, reference: 'bank-transfer', paidAmountVnd: 100000, bills: [{ contentType: 'image/png', dataBase64: btoa('bill') }] }) })))
})

it('preserves manual fields and bill images on unavailable checkout and disabled configuration', async () => {
  vi.spyOn(api, 'request').mockRejectedValue(new Error('VNPAY chưa được cấu hình.'))
  const view = render(<SiteVisitPanel consultationId={8} visit={visit} admin />)
  fireEvent.change(screen.getByLabelText('Mã tham chiếu'), { target: { value: 'manual-draft' } })
  fireEvent.change(screen.getByLabelText('Thêm ảnh minh chứng'), { target: { files: [new File(['bill'], 'bill.png', { type: 'image/png' })] } })
  await screen.findByAltText('Ảnh minh chứng 1')
  fireEvent.click(screen.getByRole('button', { name: 'Tạo link VNPAY Sandbox' }))
  await screen.findByText('VNPAY chưa được cấu hình.')
  expect(screen.getByLabelText('Mã tham chiếu').value).toBe('manual-draft')
  expect(screen.getByAltText('Ảnh minh chứng 1')).toBeTruthy()
  view.rerender(<SiteVisitPanel consultationId={8} visit={{ ...visit, vnpayEnabled: false }} admin />)
  expect(screen.getByRole('button', { name: 'Tạo link VNPAY Sandbox' }).disabled).toBe(true)
  expect(screen.getByLabelText('Mã tham chiếu').value).toBe('manual-draft')
  expect(screen.getByAltText('Ảnh minh chứng 1')).toBeTruthy()
})

it('does not expose checkout controls to non-admin staff', () => {
  render(<SiteVisitPanel consultationId={8} visit={visit} />)
  expect(screen.queryByRole('button', { name: 'Tạo link VNPAY Sandbox' })).toBeNull()
})
