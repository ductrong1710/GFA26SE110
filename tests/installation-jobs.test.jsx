import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render as renderComponent, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import InstallationJobsPage from '../src/pages/app/InstallationJobsPage'
import JobAssignmentForm from '../src/components/app/JobAssignmentForm'
import { api } from '../src/services/api'
import { PERMISSIONS as P } from '../src/config/permissions'

vi.mock('../src/context/AuthContext', () => ({ useAuth: () => ({ user: { id: 9 }, can: permission => permission === P.INSTALLATIONS_WORK }) }))
afterEach(() => { cleanup(); vi.restoreAllMocks() })
function render(component) { return renderComponent(<MemoryRouter>{component}</MemoryRouter>) }

it('starts assigned work with the displayed revision', async () => {
  const row = { id: 3, installationRequestId: 8, technicianId: 9, revision: 2, status: 'ASSIGNED', farmName: 'Rice farm', lines: [], history: [] }
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => options.method === 'POST'
    ? { ...row, revision: 3, status: 'IN_PROGRESS' } : path.endsWith('/3') ? row : { items: [row], pagination: { totalItems: 1 } })
  render(<InstallationJobsPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open job #3' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Start installation' }))
  expect(await screen.findByText('Installation work started.')).toBeTruthy()
  expect(request).toHaveBeenCalledWith('/api/installation-jobs/3/start', { method: 'POST', body: { expectedRevision: 2 } })
})

it('assigns an active technician and converts the agreed appointment to UTC', async () => {
  vi.spyOn(api, 'get').mockResolvedValue({ items: [{ id: 9, fullName: 'Technician Linh' }], pagination: { totalItems: 1, totalPages: 1 } })
  const request = vi.spyOn(api, 'request').mockResolvedValue({ id: 3 })
  const saved = vi.fn()
  render(<JobAssignmentForm requestId={8} onSaved={saved} />)
  await screen.findByRole('option', { name: 'Technician Linh' })
  fireEvent.change(screen.getByLabelText('Technician'), { target: { value: '9' } })
  fireEvent.change(screen.getByLabelText('Appointment'), { target: { value: '2030-01-15T09:00' } })
  fireEvent.change(screen.getByLabelText('Assignment note'), { target: { value: 'Customer agreed' } })
  fireEvent.click(screen.getByRole('button', { name: 'Create installation job' }))
  await vi.waitFor(() => expect(saved).toHaveBeenCalledWith({ id: 3 }))
  expect(request).toHaveBeenCalledWith('/api/installation-requests/8/jobs', { method: 'POST', toastMessage: 'Technician assigned successfully.', body: { expectedRevision: 0, technicianId: 9, scheduledAt: '2030-01-15T02:00:00.000Z', note: 'Customer agreed' } })
})
