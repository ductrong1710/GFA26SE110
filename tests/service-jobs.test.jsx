import { MemoryRouter } from 'react-router-dom'
import { cleanup, fireEvent, render as renderComponent, screen, within } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import ServiceJobAssignmentForm from '../src/components/app/ServiceJobAssignmentForm'
import { api } from '../src/services/api'
import ServiceJobsPage from '../src/pages/app/ServiceJobsPage'
import { PERMISSIONS as P } from '../src/config/permissions'
vi.mock('../src/context/AuthContext', () => ({ useAuth: () => ({ user: { id: 3 }, can: permission => permission === P.SERVICE_JOBS_WORK }) }))
afterEach(() => { cleanup(); vi.restoreAllMocks() })

test('administrator creates a typed service appointment linked to the incident', async () => {
  vi.spyOn(api, 'get').mockResolvedValue({ items: [{ id: 3, fullName: 'Assigned technician' }], pagination: { totalPages: 1 } })
  const request = vi.spyOn(api, 'request').mockImplementation(async (_, options) => options?.method === 'POST' ? { id: 8 } : { items: [{ id: 3, fullName: 'Assigned technician' }], pagination: { totalItems: 1 } })
  const saved = vi.fn()
  render(<ServiceJobAssignmentForm incidentId={5} onSaved={saved} />)
  await screen.findByRole('option', { name: 'Assigned technician' })
  fireEvent.change(screen.getByLabelText('Technician'), { target: { value: '3' } })
  fireEvent.change(screen.getByLabelText('Appointment'), { target: { value: '2027-01-01T10:00' } })
  fireEvent.change(screen.getByLabelText('Assignment note'), { target: { value: 'Check motor' } })
  fireEvent.click(screen.getByRole('button', { name: 'Create service job' }))
  await vi.waitFor(() => expect(saved).toHaveBeenCalledWith({ id: 8 }))
  expect(request).toHaveBeenCalledWith('/api/device-incidents/5/jobs', { method: 'POST', body: { type: 'REPAIR', technicianId: 3, expectedRevision: 0, note: 'Check motor', scheduledAt: '2027-01-01T03:00:00.000Z' } })
})

test('technician starts the assigned service job with the current revision', async () => {
  const job = { id: 8, technicianId: 3, revision: 1, type: 'REPAIR', status: 'ASSIGNED', history: [], images: [] }
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => options.method === 'POST'
    ? { ...job, status: 'IN_PROGRESS', revision: 2 } : path.endsWith('/plans') ? [] : path.endsWith('/8') ? job : { items: [job], pagination: { totalItems: 1 } })
  render(<ServiceJobsPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open service job #8' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Start service work' }))
  await vi.waitFor(() => expect(screen.queryByRole('button', { name: 'Start service work' })).toBeNull())
  expect(request).toHaveBeenCalledWith('/api/service-jobs/8/start', { method: 'POST', body: { expectedRevision: 1 } })
})

test('service report submission refreshes the selected job and removes the submitted form', async () => {
  let job = { id: 8, technicianId: 3, revision: 4, type: 'REPAIR', status: 'REWORK', history: [], images: [] }
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => {
    if (options.method === 'POST') { job = { ...job, revision: 5, status: 'REPORT_SUBMITTED' }; return { id: 20, revision: 2 } }
    if (path.endsWith('/plans')) return [{ id: 1, status: 'EXECUTED' }]
    if (path.endsWith('/8')) return job
    return { items: path.endsWith('/reports') ? [] : [job], pagination: { totalItems: 1 } }
  })
  render(<ServiceJobsPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open service job #8' }))
  await screen.findByRole('button', { name: 'Submit service report' })
  for (const label of ['Cause', 'Work performed', 'Components used', 'Test results', 'Notes'])
    fireEvent.change(screen.getByLabelText(label), { target: { value: 'Recorded work' } })
  for (const label of ['Connectivity', 'Operation']) fireEvent.change(screen.getByLabelText(label), { target: { value: 'true' } })
  const handover = within(screen.getByRole('group', { name: 'Signed service handover' }))
  fireEvent.change(handover.getByLabelText('Add evidence images'), { target: { files: [new File(['signed'], 'signed.jpg', { type: 'image/jpeg' })] } })
  await handover.findByAltText('Evidence image 1')
  fireEvent.click(screen.getByRole('button', { name: 'Submit service report' }))
  await vi.waitFor(() => expect(request).toHaveBeenCalledWith('/api/service-jobs/8/reports', expect.objectContaining({ method: 'POST' })))
  await vi.waitFor(() => expect(screen.queryByRole('button', { name: 'Submit service report' })).toBeNull())
  expect(request).toHaveBeenCalledWith('/api/service-jobs/8/reports', expect.objectContaining({ body: expect.objectContaining({ expectedRevision: 4 }) }))
})

function render(component) { return renderComponent(<MemoryRouter>{component}</MemoryRouter>) }

test('opens the service job linked from maintenance history', async () => {
  const job = { id: 8, technicianId: 3, revision: 1, type: 'MAINTENANCE', status: 'ASSIGNED', history: [], images: [] }
  const request = vi.spyOn(api, 'request').mockImplementation(async path => path.endsWith('/plans') ? [] : path === '/api/service-jobs/8' ? job : { items: [], pagination: { totalItems: 0 } })
  renderComponent(<MemoryRouter initialEntries={['/app/service-jobs?jobId=8']}><ServiceJobsPage /></MemoryRouter>)
  expect(await screen.findByRole('button', { name: 'Start service work' })).toBeTruthy()
  expect(request).toHaveBeenCalledWith('/api/service-jobs/8', expect.objectContaining({ signal: expect.any(AbortSignal) }))
})
