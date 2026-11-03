import { afterEach, expect, test, vi } from 'vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import ServiceReplacementForm from '../src/components/app/ServiceReplacementForm'
import ServiceReports from '../src/components/app/ServiceReports'
import { api } from '../src/services/api'

afterEach(() => { cleanup(); vi.restoreAllMocks() })
const device = { sensorNodeId: null, uavId: 7, gatewayId: null, code: 'UAV-NEW', name: 'Replacement UAV' }

test('requires retrieval confirmation and submits the scoped typed device with current revision', async () => {
  const updated = { id: 8, revision: 5, currentDeviceCode: 'UAV-NEW' }
  const request = vi.spyOn(api, 'request').mockImplementation(async (_, options = {}) => options.method === 'POST' ? updated
    : { items: [device], pagination: { totalItems: 1 } })
  const saved = vi.fn()
  render(<ServiceReplacementForm job={{ id: 8, revision: 4, currentDeviceCode: 'UAV-OLD' }} onSaved={saved} />)
  await screen.findByRole('option', { name: 'UAV-NEW · Replacement UAV' })
  fireEvent.change(screen.getByLabelText('Replacement device'), { target: { value: 'UAV-NEW' } })
  fireEvent.change(screen.getByLabelText('Replacement note'), { target: { value: 'Motor failed; equipment exchanged' } })
  expect(screen.getByRole('button', { name: 'Record device replacement' }).disabled).toBe(true)
  fireEvent.click(screen.getByLabelText('I retrieved the old device'))
  fireEvent.click(screen.getByRole('button', { name: 'Record device replacement' }))
  await vi.waitFor(() => expect(saved).toHaveBeenCalledWith(updated))
  expect(request).toHaveBeenCalledWith('/api/service-jobs/8/replacements', { method: 'POST', body: {
    expectedRevision: 4, sensorNodeId: null, uavId: 7, gatewayId: null, oldDeviceRetrieved: true, note: 'Motor failed; equipment exchanged',
  } })
})

test('keeps replacement evidence on conflict and clears a selection when paging options', async () => {
  const request = vi.spyOn(api, 'request').mockImplementation(async (_, options = {}) => {
    if (options.method === 'POST') throw new Error('Job changed. Reload the latest revision.')
    return { items: options.query.page === 1 ? [device] : [{ ...device, uavId: 9, code: 'UAV-NEXT' }], pagination: { totalItems: 21 } }
  })
  render(<ServiceReplacementForm job={{ id: 8, revision: 4 }} onSaved={vi.fn()} />)
  await screen.findByRole('option', { name: 'UAV-NEW · Replacement UAV' })
  fireEvent.change(screen.getByLabelText('Replacement device'), { target: { value: 'UAV-NEW' } })
  fireEvent.change(screen.getByLabelText('Replacement note'), { target: { value: 'Retrieved broken unit' } })
  fireEvent.click(screen.getByLabelText('I retrieved the old device'))
  fireEvent.click(screen.getByRole('button', { name: 'Record device replacement' }))
  expect(await screen.findByRole('alert')).toHaveProperty('textContent', expect.stringContaining('Job changed'))
  expect(screen.getByLabelText('Replacement note').value).toBe('Retrieved broken unit')
  expect(screen.getByLabelText('Replacement device').value).toBe('UAV-NEW')
  fireEvent.click(screen.getByRole('button', { name: 'More replacement devices' }))
  await screen.findByRole('option', { name: 'UAV-NEXT · Replacement UAV' })
  expect(screen.getByLabelText('Replacement device').value).toBe('')
  expect(screen.getByRole('button', { name: 'Record device replacement' }).disabled).toBe(true)
  expect(request).toHaveBeenCalledWith('/api/service-jobs/8/replacement-options', expect.objectContaining({ query: { page: 2 } }))
})

test('report shows its recorded replacement instead of the latest job device', async () => {
  const report = { id: 5, revision: 1, images: [], replacement: { id: 3, oldDeviceCode: 'UAV-OLD', newDeviceCode: 'UAV-FIRST', technicianId: 2, createdAt: '2026-10-30T01:00:00Z', note: 'First replacement' } }
  vi.spyOn(api, 'request').mockImplementation(async path => path.endsWith('/5') ? report : { items: [report], pagination: { totalItems: 1 } })
  render(<ServiceReports job={{ id: 8, revision: 10, status: 'REWORK', currentDeviceCode: 'UAV-SECOND' }} onUpdated={vi.fn()} />)
  fireEvent.click(await screen.findByRole('button', { name: 'View report revision 1' }))
  expect(await screen.findByText('UAV-OLD → UAV-FIRST')).toBeTruthy()
  expect(screen.queryByText(/UAV-SECOND/)).toBeNull()
})
