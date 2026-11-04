import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import InstallationRequestsPage from '../src/pages/app/InstallationRequestsPage'
import { api } from '../src/services/api'

vi.mock('../src/context/AuthContext', () => ({ useAuth: () => ({ can: () => true }) }))
afterEach(() => { cleanup(); vi.restoreAllMocks() })

it('shows submitted evidence and returns a request with its correction reason', async () => {
  const row = { id: 8, consultationId: 42, proposalId: 7, farmId: 3, ownerId: 2, status: 'SUBMITTED', lines: [{ id: 1, deviceType: 'Sensor', quantity: 3, installationLocation: 'North' }], rentalMonths: 6, depositConfirmationNote: 'Bank receipt checked' }
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => options.method === 'POST'
    ? { ...row, status: 'RETURNED', reviewNote: options.body.note } : path.endsWith('/8') ? row : { items: [row], pagination: { totalItems: 1 } })
  render(<InstallationRequestsPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open request #8' }))
  await screen.findByText('Bank receipt checked')
  fireEvent.change(screen.getByLabelText('Review note or correction reason'), { target: { value: 'Clarify location' } })
  fireEvent.click(screen.getByRole('button', { name: 'Return for correction' }))
  expect(await screen.findByText('Request returned for correction.')).toBeTruthy()
  expect(request).toHaveBeenCalledWith('/api/installation-requests/8/return', { method: 'POST', body: { note: 'Clarify location' } })
})
it('reviews a pending proposal with its original deposit bill and returns it for correction', async () => {
  const row = { id: 7, consultationId: 42, revision: 2, customerName: 'Owner', farmName: 'Orchard' }
  const bill = { contentType: 'image/png', dataBase64: 'YWJj' }
  let pending = true
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => {
    if (path.endsWith('/review') && options.method === 'POST') { pending = false; return { status: 'RETURNED' } }
    if (path === '/api/consultations/42/proposals/7') return { ...row, lines: [], depositBills: [bill] }
    const items = path === '/api/installation-dossiers' && pending ? [row] : []
    return { items, pagination: { totalItems: items.length } }
  })
  render(<InstallationRequestsPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Review dossier #7' }))
  expect((await screen.findByAltText('Evidence image 1')).src).toContain(bill.dataBase64)
  fireEvent.change(screen.getByLabelText('Dossier review note'), { target: { value: 'Receipt is illegible' } })
  fireEvent.click(screen.getByRole('button', { name: 'Return dossier' }))
  expect(await screen.findByText('Dossier returned for correction.')).toBeTruthy()
  expect(request).toHaveBeenCalledWith('/api/installation-dossiers/7/review', { method: 'POST',
    body: { approve: false, expectedRevision: 2, note: 'Receipt is illegible' } })
})
