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
