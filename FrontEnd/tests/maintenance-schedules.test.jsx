import { MemoryRouter } from 'react-router-dom'
import { cleanup, fireEvent, render as renderComponent, screen } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import MaintenanceScheduleForm from '../src/components/app/MaintenanceScheduleForm'
import MaintenanceSchedulesPage from '../src/pages/app/MaintenanceSchedulesPage'
import { api } from '../src/services/api'
const auth = vi.hoisted(() => ({ allowed: true }))
vi.mock('../src/context/AuthContext', () => ({ useAuth: () => ({ can: () => auth.allowed }) }))
afterEach(() => { cleanup(); vi.restoreAllMocks(); auth.allowed = true })
function technicians() {
  vi.spyOn(api, 'get').mockResolvedValue({ items: [{ id: 3, fullName: 'Maintenance technician' }], pagination: { totalPages: 1 } })
}
test('non-admin page does not load schedules or expose forms', () => {
  auth.allowed = false
  const request = vi.spyOn(api, 'request')
  render(<MaintenanceSchedulesPage />)
  expect(screen.getByRole('alert').textContent).toContain('Administrator')
  expect(request).not.toHaveBeenCalled()
  expect(screen.queryByRole('button', { name: 'New schedule' })).toBeNull()
})
test('disables an overdue schedule using its revision without rescheduling', async () => {
  technicians()
  let row = { id: 9, deviceType: 'UAV', policy: 'Inspection', intervalDays: 30, nextDueAt: '2020-01-01T03:00:00Z', technicianId: 3, enabled: true, revision: 4 }
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => {
    if (options.method === 'POST') { row = { ...row, enabled: false, revision: 5 }; return row }
    return path.endsWith('/9') ? row : { items: [row], pagination: { totalItems: 1 } }
  })
  render(<MaintenanceSchedulesPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open schedule #9' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Disable schedule' }))
  await vi.waitFor(() => expect(screen.queryByRole('button', { name: 'Disable schedule' })).toBeNull())
  expect(request).toHaveBeenCalledWith('/api/maintenance-schedules/9/disable', { method: 'POST', body: { expectedRevision: 4 } })
  expect(screen.getByText('Maintenance schedule saved.')).toBeTruthy()
})
test('creates a typed schedule with numeric interval and UTC date', async () => {
  technicians()
  const request = vi.spyOn(api, 'request').mockResolvedValue({ id: 9 })
  const saved = vi.fn()
  render(<MaintenanceScheduleForm onSaved={saved} />)
  await screen.findByRole('option', { name: 'Maintenance technician' })
  fireEvent.change(screen.getByLabelText('Technician'), { target: { value: '3' } })
  fireEvent.change(screen.getByLabelText('Service policy'), { target: { value: 'Quarterly inspection' } })
  fireEvent.change(screen.getByLabelText('Interval (days)'), { target: { value: '90' } })
  fireEvent.change(screen.getByLabelText('Next maintenance'), { target: { value: '2027-01-01T10:00' } })
  fireEvent.click(screen.getByRole('button', { name: 'Create schedule' }))
  await vi.waitFor(() => expect(saved).toHaveBeenCalledWith({ id: 9 }))
  expect(request).toHaveBeenCalledWith('/api/maintenance-schedules', { method: 'POST', body: {
    rentalDeviceAssignmentId: null, deviceType: 'UAV', policy: 'Quarterly inspection', intervalDays: 90,
    nextDueAt: '2027-01-01T03:00:00.000Z', technicianId: 3, enabled: true, expectedRevision: 0,
  } })
})
test('conflicting edit preserves the draft and offers explicit reload', async () => {
  technicians()
  vi.spyOn(api, 'request').mockRejectedValue(new Error('Schedule changed. Reload its latest revision.'))
  const reload = vi.fn()
  render(<MaintenanceScheduleForm schedule={{ id: 9, deviceType: 'UAV', policy: 'Inspection', intervalDays: 30,
    nextDueAt: '2027-01-01T03:00:00Z', technicianId: 3, enabled: true, revision: 4 }} onSaved={vi.fn()} onReload={reload} />)
  await screen.findByRole('option', { name: 'Maintenance technician' })
  expect(screen.getByLabelText('Next maintenance').value).toBe('2027-01-01T10:00')
  fireEvent.change(screen.getByLabelText('Service policy'), { target: { value: 'Keep my draft' } })
  fireEvent.click(screen.getByRole('button', { name: 'Save schedule' }))
  expect((await screen.findByRole('alert')).textContent).toContain('Schedule changed')
  expect(screen.getByLabelText('Service policy').value).toBe('Keep my draft')
  fireEvent.click(screen.getByRole('button', { name: 'Reload schedule and discard draft' }))
  expect(reload).toHaveBeenCalledOnce()
})

test('device scope sends only the selected unreturned assignment', async () => {
  technicians()
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => {
    if (options.method === 'POST') return { id: 10 }
    if (path === '/api/rental-contracts') return { items: [{ id: 6, farmId: 2, status: 'ACTIVE' }], pagination: { totalItems: 1 } }
    return { status: 'ACTIVE', devices: [{ id: 12, code: 'UAV-12', name: 'Current UAV', returnedAt: null }, { id: 13, code: 'OLD', name: 'Returned UAV', returnedAt: '2026-01-01' }] }
  })
  const saved = vi.fn()
  render(<MaintenanceScheduleForm onSaved={saved} />)
  await screen.findByRole('option', { name: 'Maintenance technician' })
  fireEvent.change(screen.getByLabelText('Schedule scope'), { target: { value: 'DEVICE' } })
  await screen.findByRole('option', { name: 'Contract #6 · Farm #2' })
  fireEvent.change(screen.getByLabelText('Rental contract'), { target: { value: '6' } })
  await screen.findByRole('option', { name: 'UAV-12 · Current UAV' })
  expect(screen.queryByRole('option', { name: 'OLD · Returned UAV' })).toBeNull()
  fireEvent.change(screen.getByLabelText('Rental device'), { target: { value: '12' } })
  fireEvent.change(screen.getByLabelText('Technician'), { target: { value: '3' } })
  fireEvent.change(screen.getByLabelText('Service policy'), { target: { value: 'Inspect UAV' } })
  fireEvent.change(screen.getByLabelText('Next maintenance'), { target: { value: '2027-01-01T10:00' } })
  fireEvent.click(screen.getByRole('button', { name: 'Create schedule' }))
  await vi.waitFor(() => expect(saved).toHaveBeenCalled())
  expect(request).toHaveBeenCalledWith('/api/maintenance-schedules', expect.objectContaining({ body: expect.objectContaining({ deviceType: null, rentalDeviceAssignmentId: 12 }) }))
})

function render(component) { return renderComponent(<MemoryRouter>{component}</MemoryRouter>) }

test('editing a device schedule retains its original assignment lineage', async () => {
  technicians()
  const request = vi.spyOn(api, 'request').mockResolvedValue({ id: 9, revision: 5 })
  const saved = vi.fn()
  render(<MaintenanceScheduleForm schedule={{ id: 9, rentalDeviceAssignmentId: 12, deviceType: null,
    policy: 'Inspect successor', intervalDays: 30, nextDueAt: '2027-01-01T03:00:00Z', technicianId: 3, enabled: true, revision: 4 }} onSaved={saved} />)
  await screen.findByRole('option', { name: 'Maintenance technician' })
  expect(screen.getByLabelText('Schedule scope').value).toBe('CURRENT')
  fireEvent.change(screen.getByLabelText('Service policy'), { target: { value: 'Updated successor inspection' } })
  fireEvent.click(screen.getByRole('button', { name: 'Save schedule' }))
  await vi.waitFor(() => expect(saved).toHaveBeenCalledWith({ id: 9, revision: 5 }))
  expect(request).toHaveBeenCalledWith('/api/maintenance-schedules/9', expect.objectContaining({ method: 'PUT', body: expect.objectContaining({ rentalDeviceAssignmentId: 12, deviceType: null, expectedRevision: 4 }) }))
})
