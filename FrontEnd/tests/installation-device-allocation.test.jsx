import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import InstallationDeviceAllocation from '../src/components/app/InstallationDeviceAllocation'
import { api } from '../src/services/api'

afterEach(() => { cleanup(); vi.restoreAllMocks() })
it('allocates a registered farm sensor against the proposed line', async () => {
  vi.spyOn(api, 'get').mockImplementation(async path => ({ items: path === '/api/sensor-nodes' ? [{ id: 2, deviceCode: 'SN-42', name: 'North sensor', isActive: true }] : [], pagination: { totalPages: 1 } }))
  const request = vi.spyOn(api, 'request').mockResolvedValue([])
  const saved = vi.fn()
  render(<InstallationDeviceAllocation job={{ id: 3, farmId: 4, revision: 4, lines: [{ id: 8, quantity: 1, deviceType: 'Sensor' }] }} onSaved={saved} />)
  await screen.findByRole('option', { name: 'Sensor · SN-42 · North sensor' })
  fireEvent.change(screen.getByLabelText('Device 1 for Sensor'), { target: { value: 'sensor:2' } })
  fireEvent.change(screen.getByLabelText('Device allocation note'), { target: { value: 'Prepared for installation' } })
  fireEvent.click(screen.getByRole('button', { name: 'Save allocated devices' }))
  await vi.waitFor(() => expect(saved).toHaveBeenCalled())
  expect(request).toHaveBeenCalledWith('/api/installation-jobs/3/devices', { method: 'PUT', body: { expectedRevision: 4, note: 'Prepared for installation', devices: [{ proposalLineId: 8, sensorNodeId: 2, uavId: null, gatewayId: null }] } })
})
