import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import ProposalPanel from '../src/components/app/ProposalPanel'
import { api } from '../src/services/api'

afterEach(() => { cleanup(); vi.restoreAllMocks() })

it('creates a proposal against the selected survey and its zone', async () => {
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
  fireEvent.change(screen.getByLabelText('Quantity'), { target: { value: '3' } })
  fireEvent.change(screen.getByLabelText('Installation location'), { target: { value: 'Canal edge' } })
  fireEvent.change(screen.getByLabelText('Rental duration (months)'), { target: { value: '6' } })
  fireEvent.change(screen.getByLabelText('Requested installation time'), { target: { value: '2030-01-15T09:00' } })
  fireEvent.submit(screen.getByRole('form', { name: 'Device proposal' }))
  await waitFor(() => expect(request).toHaveBeenCalledWith('/api/consultations/42/proposals', expect.objectContaining({ method: 'POST', body: expect.objectContaining({
    expectedRevision: 0, surveyId: 3, rentalMonths: 6, lines: [{ surveyZoneId: 5, deviceType: 'Soil moisture sensor', quantity: 3, installationLocation: 'Canal edge' }],
  }) })))
  expect(await screen.findByText('Proposal revision 1 saved.')).toBeTruthy()
})

it('records plan and deposit evidence before submitting installation', async () => {
  let row = { id: 7, revision: 1, surveyId: 3, rentalMonths: 6, status: 'DRAFT', lines: [] }
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => {
    if (options.method === 'POST') {
      if (path.endsWith('/submit-installation')) return { id: 9, status: 'SUBMITTED' }
      row = { ...row, status: path.endsWith('/confirm-plan') ? 'PLAN_CONFIRMED' : 'DEPOSIT_CONFIRMED' }
      return row
    }
    return path.endsWith('/7') ? row : { items: [row], pagination: { totalItems: 1 } }
  })
  render(<ProposalPanel consultationId={42} editable />)
  fireEvent.click(await screen.findByRole('button', { name: 'View proposal revision 1' }))
  for (const [button, evidence] of [['Record customer plan confirmation', 'Customer accepted by phone'], ['Record external deposit confirmation', 'Bank receipt REF-42']]) {
    const action = await screen.findByRole('button', { name: button })
    fireEvent.change(screen.getByLabelText('Confirmation evidence or reference'), { target: { value: evidence } })
    fireEvent.click(action)
  }
  fireEvent.click(await screen.findByRole('button', { name: 'Submit installation request' }))
  expect(await screen.findByText('Installation request #9 submitted for administrator review.')).toBeTruthy()
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
