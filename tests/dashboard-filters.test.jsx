import React from 'react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Outlet, Routes, Route } from 'react-router-dom'
import { AuthContext } from '../src/context/AuthContext'
import { api } from '../src/services/api'
import OperationsDashboard from '../src/pages/app/OperationsDashboard'
import WorkOrdersPage from '../src/pages/app/WorkOrdersPage'
import SupportPage from '../src/pages/app/SupportPage'
import ConsultationRequestsPage from '../src/pages/app/ConsultationRequestsPage'
import DispatchPage from '../src/pages/app/DispatchPage'

vi.mock('../src/services/api', () => ({ api: { get: vi.fn(), request: vi.fn() } }))
vi.mock('../src/components/app/GeographicFarmMap', () => ({ default: ({ farm }) => <div role="img" aria-label={'Map of ' + farm.name} /> }))
vi.mock('../src/components/workflow/OperationsCharts', () => ({ default: () => <p>Operations charts</p> }))
function mount(page) {
  return render(<AuthContext.Provider value={{ activeRole: 'ADMINISTRATOR' }}><MemoryRouter><Routes>
    <Route element={<Outlet context={{ currentFarm: { id: 5, name: 'Demo farm' }, farmWorkspace: { zones: [] } }} />}><Route path="/" element={page} /></Route>
  </Routes></MemoryRouter></AuthContext.Provider>)
}
beforeEach(() => {
  vi.resetAllMocks()
  api.request.mockResolvedValue([])
  api.get.mockImplementation(path => Promise.resolve(path === '/api/dashboard/overview'
    ? { farms: 1, sensorNodes: { total: 0 }, gateways: { total: 0 }, alerts: { open: 0 }, operations: { openInstallations: 0, openMaintenance: 0, openRepairs: 0, expiringRentals: 0 } }
    : { consultationRequests: 0, recentConsultationRequests: [], installationPipeline: [], rentalsEndingInPeriod: [], maintenance: [] }))
})
afterEach(cleanup)

it('shows all dispatch jobs until both date and view are selected and restores all when cleared', async () => {
  api.get.mockResolvedValue({ installations: [], services: [] })
  mount(<DispatchPage />)
  await waitFor(() => expect(api.get).toHaveBeenCalledWith('/api/operations/calendar', {}, expect.anything()))
  const date = screen.getByLabelText('Start date')
  const view = screen.getByLabelText('View')
  expect(date.value).toBe('')
  expect(view.value).toBe('')
  fireEvent.change(date, { target: { value: '2026-11-04' } })
  expect(api.get.mock.calls.at(-1)[1]).toEqual({})
  fireEvent.change(view, { target: { value: '7' } })
  await waitFor(() => expect(api.get).toHaveBeenLastCalledWith('/api/operations/calendar', { from: '2026-11-03T17:00:00.000Z', to: '2026-11-10T17:00:00.000Z' }, expect.anything()))
  fireEvent.change(date, { target: { value: '' } })
  await waitFor(() => expect(api.get).toHaveBeenLastCalledWith('/api/operations/calendar', {}, expect.anything()))
})

it('loads homepage consultation requests with blank dates and waits for complete custom filters', async () => {
  api.get.mockResolvedValue({ items: [{ id: 42, customerName: 'Guest customer', phone: '0901234567', serviceNeed: 'Sensor consultation', content: 'Monitor soil', farmLocation: 'Da Lat', createdAt: '2026-11-04T00:00:00Z' }], total: 1 })
  mount(<ConsultationRequestsPage />)
  await screen.findByText('Guest customer')
  expect(screen.getByLabelText('From date').value).toBe('')
  expect(screen.getByLabelText('To date').value).toBe('')
  const query = api.get.mock.calls[0][1]
  expect(api.get.mock.calls[0][0]).toBe('/api/operations/consultation-requests')
  expect(query.from).toBeUndefined()
  expect(query.to).toBeUndefined()
  expect(query.pageSize).toBe(10)
  fireEvent.change(screen.getByLabelText('From date'), { target: { value: '2026-11-01' } })
  expect(screen.getByText('Select both From date and To date to filter requests.')).toBeTruthy()
  fireEvent.change(screen.getByLabelText('To date'), { target: { value: '2026-11-04' } })
  await waitFor(() => expect(api.get).toHaveBeenCalledWith('/api/operations/consultation-requests', expect.objectContaining({ from: '2026-10-31T17:00:00.000Z', to: '2026-11-04T17:00:00.000Z' }), expect.anything()))
})

it('shows an English dashboard and restores the selected farm map without default dates', async () => {
  mount(<OperationsDashboard />)
  expect(screen.getByRole('heading', { name: 'Farm operations' })).toBeTruthy()
  expect(screen.getByLabelText('From date').value).toBe('')
  expect(screen.getByLabelText('To date').value).toBe('')
  await screen.findByRole('img', { name: 'Map of Demo farm' })
  expect(api.get.mock.calls.some(([path]) => path === '/api/operations/dashboard')).toBe(false)
  fireEvent.change(screen.getByLabelText('From date'), { target: { value: '2026-11-01' } })
  expect(api.get.mock.calls.some(([path]) => path === '/api/operations/dashboard')).toBe(false)
  fireEvent.change(screen.getByLabelText('To date'), { target: { value: '2026-11-04' } })
  await screen.findByText('Operations charts')
  expect(api.get).toHaveBeenCalledWith('/api/operations/dashboard', { from: '2026-10-31T17:00:00.000Z', to: '2026-11-04T17:00:00.000Z' }, expect.anything())
  fireEvent.change(screen.getByLabelText('From date'), { target: { value: '' } })
  expect(screen.getByLabelText('From date').value).toBe('')
})

it('does not select or fetch a work order type until the user chooses it', async () => {
  mount(<WorkOrdersPage />)
  const type = screen.getByLabelText('Work order type')
  expect(type.value).toBe('')
  expect(api.request).not.toHaveBeenCalled()
  fireEvent.change(type, { target: { value: 'installation' } })
  await waitFor(() => expect(api.request).toHaveBeenCalledWith('/api/installation-jobs', expect.anything()))
})

it('waits for report type selection before showing or loading support reports', async () => {
  mount(<SupportPage />)
  expect(screen.getByLabelText('Report type').value).toBe('')
  expect(screen.queryByRole('heading', { name: 'Reports' })).toBeNull()
  expect(api.request).not.toHaveBeenCalled()
  fireEvent.change(screen.getByLabelText('Report type'), { target: { value: 'device' } })
  await waitFor(() => expect(api.request).toHaveBeenCalledWith('/api/device-incidents', expect.anything()))
  expect(screen.getByRole('heading', { name: 'Reports' })).toBeTruthy()
})
