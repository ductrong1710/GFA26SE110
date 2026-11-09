import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import RentalContractsPage from '../src/pages/app/RentalContractsPage'
import { api } from '../src/services/api'
import { PERMISSIONS as P } from '../src/config/permissions'

const auth = vi.hoisted(() => ({ admin: true }))
vi.mock('../src/context/AuthContext', () => ({ useAuth: () => ({ can: permission => auth.admin && permission === P.RENTALS_MANAGE }) }))
afterEach(() => { cleanup(); vi.restoreAllMocks(); auth.admin = true })
const row = { id: 6, installationJobId: 3, farmId: 8, ownerId: 9, revision: 2, status: 'ACTIVE', startAt: '2026-01-01T00:00:00Z', endAt: '2030-01-01T00:00:00Z',
  devices: [{ id: 1, sensorNodeId: 2, code: 'SN-42', name: 'North sensor', installationLocation: 'Canal edge', returnedAt: null }],
  history: [{ id: 1, action: 'ACTIVATED', userId: 1, note: 'Agreement confirmed', createdAt: '2026-01-01T00:00:00Z' }] }
function mockRental(detail = row) {
  return vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => {
    if (options.method === 'POST') return { ...detail, revision: detail.revision + 1, status: path.endsWith('/end') ? 'ENDED' : 'ACTIVE' }
    return path.endsWith('/6') ? detail : { items: [detail], pagination: { totalItems: 1 } }
  })
}
it('shows owner rental and device history without administrative actions', async () => {
  auth.admin = false; mockRental()
  render(<RentalContractsPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open contract #6' }))
  await screen.findByText('Canal edge')
  expect(screen.getByText('Agreement confirmed')).toBeTruthy()
  expect(screen.queryByLabelText('Contract action')).toBeNull()
})
it('activates a draft explicitly using its current revision', async () => {
  const request = mockRental({ ...row, status: 'DRAFT' })
  render(<RentalContractsPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open contract #6' }))
  expect((await screen.findByLabelText('Contract action')).value).toBe('activate')
  expect(screen.queryByLabelText('New rental end')).toBeNull()
  fireEvent.change(screen.getByLabelText('Decision note'), { target: { value: 'Agreed period has begun' } })
  fireEvent.click(screen.getByRole('button', { name: 'Record contract action' }))
  expect(await screen.findByText('Rental contract updated.')).toBeTruthy()
  expect(request).toHaveBeenCalledWith('/api/rental-contracts/6/activate', { method: 'POST', toastMessage: 'Rental contract activated successfully.', body: { expectedRevision: 2, note: 'Agreed period has begun' } })
  expect(screen.getByLabelText('Contract action').value).toBe('extend')
})
it('requires explicit physical return confirmation before ending the current revision', async () => {
  const request = mockRental()
  render(<RentalContractsPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open contract #6' }))
  fireEvent.change(await screen.findByLabelText('Contract action'), { target: { value: 'end' } })
  fireEvent.change(screen.getByLabelText('Decision note'), { target: { value: 'All equipment retrieved' } })
  fireEvent.click(screen.getByRole('button', { name: 'Record contract action' }))
  expect(request.mock.calls.filter(([, options]) => options?.method === 'POST')).toHaveLength(0)
  fireEvent.click(screen.getByLabelText('All assigned devices have been physically returned'))
  fireEvent.click(screen.getByRole('button', { name: 'Record contract action' }))
  expect(await screen.findByText('Rental contract updated.')).toBeTruthy()
  expect(request).toHaveBeenCalledWith('/api/rental-contracts/6/end', { method: 'POST', body: { expectedRevision: 2, note: 'All equipment retrieved', devicesReturned: true } })
})
it('records the agreed start in Vietnam time with the current draft revision', async () => {
  const request = mockRental({ ...row, status: 'DRAFT' })
  render(<RentalContractsPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open contract #6' }))
  fireEvent.change(await screen.findByLabelText('Contract action'), { target: { value: 'confirm-start' } })
  fireEvent.change(screen.getByLabelText('Agreed rental start'), { target: { value: '2027-02-01T00:00' } })
  fireEvent.change(screen.getByLabelText('Decision note'), { target: { value: 'Start agreed with owner' } })
  fireEvent.click(screen.getByRole('button', { name: 'Record contract action' }))
  expect(await screen.findByText('Rental contract updated.')).toBeTruthy()
  expect(request).toHaveBeenCalledWith('/api/rental-contracts/6/confirm-start', { method: 'POST',
    body: { expectedRevision: 2, startAt: '2027-01-31T17:00:00.000Z', note: 'Start agreed with owner' } })
})
it('preserves an extension agreement when a concurrent change rejects it', async () => {
  const request = mockRental()
  request.mockImplementation(async (path, options = {}) => {
    if (options.method === 'POST') throw new Error('Contract changed. Reload the latest revision.')
    return path.endsWith('/6') ? row : { items: [row], pagination: { totalItems: 1 } }
  })
  render(<RentalContractsPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open contract #6' }))
  fireEvent.change(await screen.findByLabelText('New rental end'), { target: { value: '2031-01-15T09:00' } })
  fireEvent.change(screen.getByLabelText('Decision note'), { target: { value: 'Extension confirmed outside system' } })
  fireEvent.click(screen.getByRole('button', { name: 'Record contract action' }))
  expect(await screen.findByRole('alert')).toHaveProperty('textContent', expect.stringContaining('Contract changed'))
  expect(screen.getByLabelText('Decision note').value).toBe('Extension confirmed outside system')
  expect(request).toHaveBeenCalledWith('/api/rental-contracts/6/extend', { method: 'POST', body: { expectedRevision: 2,
    note: 'Extension confirmed outside system', endAt: '2031-01-15T02:00:00.000Z' } })
})
