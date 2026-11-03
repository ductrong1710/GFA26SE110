import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import DeviceIncidentsPage from '../src/pages/app/DeviceIncidentsPage'
import { api } from '../src/services/api'
import { PERMISSIONS as P } from '../src/config/permissions'

const auth = vi.hoisted(() => ({ owner: true, manage: false }))
vi.mock('../src/context/AuthContext', () => ({ useAuth: () => ({ can: permission => auth.owner && permission === P.INCIDENTS_CREATE || auth.manage && permission === P.SERVICE_JOBS_MANAGE }) }))
afterEach(() => { cleanup(); vi.restoreAllMocks(); auth.owner = true; auth.manage = false })
const incident = { id: 1, farmId: 8, rentalDeviceAssignmentId: 44, deviceCode: 'SN-42', description: 'No signal', reportedAt: '2026-10-29T00:00:00Z', status: 'OPEN', reporterId: 9, missionId: null, images: [{ contentType: 'image/png', dataBase64: 'aGVsbG8=' }] }
function mockApi(fail = false) {
  return vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => {
    if (options.method === 'POST') { if (fail) throw new Error('Device is no longer assigned.'); return incident }
    if (path === '/api/missions/12') return { id: 12, name: 'Failed flight', status: 'FAILED', farm: { id: 8 }, uav: { id: 2 } }
    if (path === '/api/rental-contracts') return { items: [{ id: 6, farmId: 8, status: 'EXPIRED' }, { id: 7, farmId: 8, status: 'ENDED' }], pagination: { totalItems: 2 } }
    if (path === '/api/rental-contracts/6') return { id: 6, farmId: 8, status: 'EXPIRED', devices: [{ id: 44, uavId: 2, code: 'SN-42', name: 'UAV', installationLocation: 'Canal edge', returnedAt: null }, { id: 45, code: 'RETURNED', returnedAt: '2026-01-01' }] }
    if (path === '/api/device-incidents/1') return incident
    return { items: [incident], pagination: { totalItems: 1 } }
  })
}
function mount(url = '/app/device-incidents') { render(<MemoryRouter initialEntries={[url]}><DeviceIncidentsPage /></MemoryRouter>) }
it('shows a resolved incident without offering another service job', async () => {
  auth.owner = false; auth.manage = true
  const resolved = { ...incident, status: 'RESOLVED' }
  vi.spyOn(api, 'get').mockResolvedValue({ items: [], pagination: { totalPages: 1 } })
  vi.spyOn(api, 'request').mockImplementation(async path => path.endsWith('/1') ? resolved : { items: [resolved], pagination: { totalItems: 1 } })
  mount()
  fireEvent.click(await screen.findByRole('button', { name: 'Open incident #1' }))
  await screen.findByAltText('Evidence image 1')
  expect(screen.queryByRole('button', { name: 'Create service job' })).toBeNull()
})
async function fill() {
  await screen.findByRole('option', { name: /Contract #6/ })
  expect(screen.queryByRole('option', { name: /Contract #7/ })).toBeNull()
  fireEvent.change(screen.getByLabelText('Rental contract'), { target: { value: '6' } })
  await screen.findByRole('option', { name: /SN-42/ })
  expect(screen.queryByRole('option', { name: /RETURNED/ })).toBeNull()
  fireEvent.change(screen.getByLabelText('Assigned device'), { target: { value: '44' } })
  fireEvent.change(screen.getByLabelText('Incident description'), { target: { value: 'No signal' } })
}
it('submits the rental assignment identity, including expired rentals, and displays saved evidence', async () => {
  const request = mockApi(); mount(); await fill()
  fireEvent.click(screen.getByRole('button', { name: 'Report incident' }))
  await screen.findByText('Incident reported.')
  expect(request).toHaveBeenCalledWith('/api/device-incidents', { method: 'POST', body: { rentalDeviceAssignmentId: 44, description: 'No signal', images: [] } })
  expect(screen.getByAltText('Evidence image 1').src).toBe('data:image/png;base64,aGVsbG8=')
})
it('keeps the owner draft after rejection', async () => {
  mockApi(true); mount(); await fill()
  fireEvent.click(screen.getByRole('button', { name: 'Report incident' }))
  expect(await screen.findByRole('alert')).toHaveProperty('textContent', 'Device is no longer assigned.')
  expect(screen.getByLabelText('Incident description').value).toBe('No signal')
  expect(screen.getByLabelText('Assigned device').value).toBe('44')
})
it('lets administrators read incident details without reporting or rental lookups', async () => {
  auth.owner = false; const request = mockApi(); mount()
  fireEvent.click(await screen.findByRole('button', { name: 'Open incident #1' }))
  expect(await screen.findByAltText('Evidence image 1')).toBeTruthy()
  expect(screen.queryByRole('button', { name: 'Report incident' })).toBeNull()
  expect(request.mock.calls.some(([path]) => path.startsWith('/api/rental-contracts'))).toBe(false)
})
it('includes a linked mission in the submitted incident', async () => {
  const request = mockApi(); mount('/app/device-incidents?missionId=12'); await fill()
  expect(await screen.findByText(/Failed flight/)).toBeTruthy()
  fireEvent.click(screen.getByRole('button', { name: 'Report incident' }))
  await screen.findByText('Incident reported.')
  expect(request).toHaveBeenCalledWith('/api/device-incidents', { method: 'POST', body: { rentalDeviceAssignmentId: 44, description: 'No signal', images: [], missionId: 12 } })
})
it('allows reporting a sensor targeted by the linked mission', async () => {
  const request = mockApi(); const normal = request.getMockImplementation()
  request.mockImplementation((path, options) => {
    if (path === '/api/missions/12') return Promise.resolve({ id: 12, name: 'Failed collection', farm: { id: 8 }, targets: [{ sensorNodeId: 5 }] })
    if (path === '/api/rental-contracts/6') return Promise.resolve({ id: 6, farmId: 8, status: 'ACTIVE', devices: [{ id: 44, sensorNodeId: 5, code: 'SN-42', name: 'Sensor', returnedAt: null }] })
    return normal(path, options)
  })
  mount('/app/device-incidents?missionId=12'); await fill()
  fireEvent.click(screen.getByRole('button', { name: 'Report incident' }))
  await screen.findByText('Incident reported.')
})
it('blocks submission when the linked mission cannot be read', async () => {
  const request = mockApi(); const normal = request.getMockImplementation()
  request.mockImplementation((path, options) => path === '/api/missions/12' ? Promise.reject(new Error('Mission access denied')) : normal(path, options))
  mount('/app/device-incidents?missionId=12'); await fill()
  expect(await screen.findByText('Mission access denied')).toBeTruthy()
  expect(screen.getByRole('button', { name: 'Report incident' }).disabled).toBe(true)
  expect(request.mock.calls.filter(([, options]) => options?.method === 'POST')).toHaveLength(0)
})
it('waits for images before submitting their encoded data', async () => {
  const request = mockApi(); mount(); await fill()
  fireEvent.change(screen.getByLabelText('Add evidence images'), { target: { files: [new File(['photo'], 'evidence.png', { type: 'image/png' })] } })
  expect(screen.getByRole('button', { name: 'Report incident' }).disabled).toBe(true)
  await screen.findByAltText('Evidence image 1')
  fireEvent.click(screen.getByRole('button', { name: 'Report incident' }))
  await screen.findByText('Incident reported.')
  expect(request).toHaveBeenCalledWith('/api/device-incidents', { method: 'POST', body: { rentalDeviceAssignmentId: 44, description: 'No signal', images: [{ contentType: 'image/png', dataBase64: btoa('photo') }] } })
})
