import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import ProposalPanel from '../src/components/app/ProposalPanel'
import { api } from '../src/services/api'

afterEach(() => { cleanup(); vi.restoreAllMocks() })

it('creates a proposal against the selected survey and its zone', async () => {
  vi.spyOn(api, 'get').mockResolvedValue({ items: [
    { id: 12, kind: 'DEVICE', title: 'Soil probe', estimatedMonthlyRentVnd: 0 },
    { id: 13, kind: 'DEVICE', title: 'Unpriced device', estimatedMonthlyRentVnd: null },
    { id: 14, kind: 'COMPONENT', title: 'Spare board', replacementPriceVnd: 50 },
  ], pagination: { totalPages: 1 } })
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => {
    if (options.method === 'POST') return { id: 7, revision: 1, status: 'DRAFT', ...options.body }
    if (path.endsWith('/surveys/3')) return { id: 3, revision: 1, zones: [{ id: 5, name: 'North' }] }
    if (path.endsWith('/surveys')) return { items: [{ id: 3, revision: 1, farmName: 'Rice farm' }], pagination: { totalItems: 1 } }
    return { items: [], pagination: { totalItems: 0 } }
  })
  render(<ProposalPanel consultationId={42} editable />)
  fireEvent.click(await screen.findByRole('button', { name: 'Prepare proposal' }))
  await screen.findByRole('option', { name: 'Revision 1: Rice farm' })
  fireEvent.change(screen.getByLabelText('Survey revision'), { target: { value: '3' } })
  await screen.findByRole('option', { name: 'North' })
  fireEvent.change(screen.getByLabelText('Survey zone'), { target: { value: '5' } })
  fireEvent.change(screen.getByLabelText('Device type'), { target: { value: 'Soil moisture sensor' } })
  await screen.findByRole('option', { name: /Soil probe/ })
  expect(screen.queryByRole('option', { name: /Unpriced device|Spare board/ })).toBeNull()
  fireEvent.change(screen.getByLabelText('Published rental product'), { target: { value: '12' } })
  fireEvent.change(screen.getByLabelText('Quantity'), { target: { value: '3' } })
  fireEvent.change(screen.getByLabelText('Installation location'), { target: { value: 'Canal edge' } })
  fireEvent.change(screen.getByLabelText('Rental duration (months)'), { target: { value: '6' } })
  fireEvent.change(screen.getByLabelText('Requested installation time'), { target: { value: '2030-01-15T09:00' } })
  fireEvent.submit(screen.getByRole('form', { name: 'Device proposal' }))
  await waitFor(() => expect(request).toHaveBeenCalledWith('/api/consultations/42/proposals', expect.objectContaining({ method: 'POST', body: expect.objectContaining({
    expectedRevision: 0, surveyId: 3, rentalMonths: 6, requestedInstallationAt: '2030-01-15T02:00:00.000Z', lines: [{ surveyZoneId: 5, publicContentId: 12, deviceType: 'Soil moisture sensor', quantity: 3, installationLocation: 'Canal edge' }],
  }) })))
  expect(await screen.findByText('Proposal revision 1 saved.')).toBeTruthy()
})

it.each([true, false])('submits the confirmed installation dossier (linked customer: %s)', async linked => {
  let row = { id: 7, revision: 1, surveyId: 3, rentalMonths: 6, status: 'DRAFT', lines: [] }
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => {
    if (options.method === 'POST') {
      if (path === '/api/installation-dossiers/7/submit') return { proposalId: 7, status: 'WAITING_ADMIN' }
      row = { ...row, status: path.endsWith('/confirm-plan') ? 'PLAN_CONFIRMED' : 'DEPOSIT_CONFIRMED' }
      return row
    }
    return path.endsWith('/7') ? row : { items: [row], pagination: { totalItems: 1 } }
  })
  render(<ProposalPanel consultationId={42} editable linked={linked} customerName="Garden owner" customerEmail="owner@example.com" />)
  fireEvent.click(await screen.findByRole('button', { name: 'View proposal revision 1' }))
  for (const [button, evidence] of [['Record customer plan confirmation', 'Customer accepted by phone'], ['Record external deposit confirmation', 'Bank receipt REF-42']]) {
    const action = await screen.findByRole('button', { name: button })
    fireEvent.change(screen.getByLabelText('Confirmation evidence or reference'), { target: { value: evidence } })
    fireEvent.click(action)
  }
  const submit = await screen.findByRole('button', { name: 'Submit installation request' })
  expect(submit.disabled).toBe(true)
  fireEvent.change(screen.getByLabelText('Dossier note'), { target: { value: 'Receipt attached' } })
  fireEvent.change(screen.getByLabelText('Add evidence images'), { target: { files: [new File(['receipt'], 'receipt.png', { type: 'image/png' })] } })
  await screen.findByAltText('Evidence image 1')
  fireEvent.click(submit)
  await waitFor(() => expect(request).toHaveBeenCalledWith('/api/installation-dossiers/7/submit', {
    method: 'POST', body: { expectedRevision: 1, note: 'Receipt attached', ...(!linked && { customer: { fullName: 'Garden owner', email: 'owner@example.com' } }), bills: [expect.objectContaining({ contentType: 'image/png' })] },
  }))
  expect(await screen.findByText('Dossier #7 submitted for administrator review.')).toBeTruthy()
  expect(request).toHaveBeenCalledWith('/api/consultations/42/proposals/7/confirm-deposit', { method: 'POST', body: { note: 'Bank receipt REF-42' } })
})

it('keeps owner proposal details read-only', async () => {
  const row = { id: 7, revision: 1, status: 'DEPOSIT_CONFIRMED', lines: [] }
  vi.spyOn(api, 'request').mockImplementation(async path => path.endsWith('/7') ? row : { items: [row], pagination: { totalItems: 1 } })
  render(<ProposalPanel consultationId={42} />)
  fireEvent.click(await screen.findByRole('button', { name: 'View proposal revision 1' }))
  await screen.findByRole('region', { name: 'Proposal details' })
  expect(screen.queryByRole('button', { name: 'Prepare proposal' })).toBeNull()
  expect(screen.queryByRole('button', { name: 'Submit installation request' })).toBeNull()
  expect(screen.queryByLabelText('Confirmation evidence or reference')).toBeNull()
})
