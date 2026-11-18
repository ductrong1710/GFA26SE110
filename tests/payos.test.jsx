import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import SiteVisitPanel from '../src/components/app/SiteVisitPanel'
import { api } from '../src/services/api'

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals() })
const visit = { revision: 2, channel: 'ON_SITE', payOsEnabled: true,
  current: { revision: 2, channel: 'ON_SITE', reviewStatus: 'APPROVED', paymentStatus: 'UNPAID', amountVnd: 100000 } }
const checkout = { orderCode: 987654321, paymentLinkId: 'link-1', checkoutUrl: 'https://pay.payos.vn/web/abc', qrCode: '000201010212', status: 'PENDING', expiresAt: '2030-01-01T12:00:00Z' }

it('creates and copies a PayOS link without marking the payment confirmed', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue(checkout)
  const writeText = vi.fn().mockResolvedValue()
  vi.stubGlobal('navigator', { clipboard: { writeText } })
  render(<SiteVisitPanel consultationId={8} visit={visit} admin onSaved={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Tạo link thanh toán PayOS' }))
  await screen.findByDisplayValue(checkout.checkoutUrl)
  expect(screen.getByText(/Chờ thanh toán/)).toBeTruthy()
  expect(screen.getByText('Số tiền: 100.000 đ')).toBeTruthy()
  expect(screen.getByText('Mã QR thanh toán PayOS')).toBeTruthy()
  expect(request).toHaveBeenCalledExactlyOnceWith('/api/consultations/8/site-visit/payments/payos/checkout', expect.objectContaining({ method: 'POST', body: { expectedRevision: 2 }, retry: false }))
  expect(screen.getByText('Chưa thanh toán')).toBeTruthy()
  fireEvent.click(screen.getByRole('button', { name: 'Sao chép link' }))
  await waitFor(() => expect(writeText).toHaveBeenCalledWith(checkout.checkoutUrl))
  expect(request).toHaveBeenCalledTimes(1)
})

it.each(['CREATING', 'PENDING', 'RECONCILIATION_REQUIRED'])('blocks manual confirmation during %s', status => {
  const request = vi.spyOn(api, 'request')
  render(<SiteVisitPanel consultationId={8} visit={{ ...visit, current: { ...visit.current, payOs: { ...checkout, status } } }} admin />)
  expect(screen.getByRole('button', { name: 'Tạo link thanh toán PayOS' }).disabled).toBe(true)
  expect(screen.getByLabelText('Số tiền thực nhận (VND)').closest('fieldset').disabled).toBe(true)
  fireEvent.click(screen.getByRole('button', { name: 'Đối soát PayOS' }))
  expect(request).toHaveBeenCalledWith('/api/payments/payos/987654321/reconcile', expect.objectContaining({ method: 'POST', retry: false }))
})

it('allows assigned staff to view and copy the PayOS checkout link', () => {
  render(<SiteVisitPanel consultationId={8} visit={{ ...visit, current: { ...visit.current, payOs: checkout } }} editable />)
  expect(screen.getByLabelText('Link thanh toán PayOS').value).toBe(checkout.checkoutUrl)
  expect(screen.queryByRole('button', { name: 'Tạo link thanh toán PayOS' })).toBeNull()
})

it('retains manual receipt flow after a failed PayOS order', () => {
  render(<SiteVisitPanel consultationId={8} visit={{ ...visit, current: { ...visit.current, payOs: { ...checkout, status: 'FAILED' } } }} admin />)
  expect(screen.getByLabelText('Số tiền thực nhận (VND)').closest('fieldset').disabled).toBe(false)
})

it('does not expose checkout controls to non-admin staff', () => {
  render(<SiteVisitPanel consultationId={8} visit={visit} />)
  expect(screen.queryByRole('button', { name: 'Tạo link thanh toán PayOS' })).toBeNull()
})
