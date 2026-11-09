import { afterEach, expect, test, vi } from 'vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import ServiceReports from '../src/components/app/ServiceReports'
import { api } from '../src/services/api'

afterEach(() => { cleanup(); vi.restoreAllMocks() })
test('report shows its recorded replacement instead of the latest job device', async () => {
  const report = { id: 5, revision: 1, images: [], replacement: { id: 3, oldDeviceCode: 'UAV-OLD', newDeviceCode: 'UAV-FIRST', technicianId: 2, createdAt: '2026-10-30T01:00:00Z', note: 'First replacement' } }
  vi.spyOn(api, 'request').mockImplementation(async path => path.endsWith('/5') ? report : { items: [report], pagination: { totalItems: 1 } })
  render(<ServiceReports job={{ id: 8, revision: 10, status: 'REWORK', currentDeviceCode: 'UAV-SECOND' }} onUpdated={vi.fn()} />)
  fireEvent.click(await screen.findByRole('button', { name: 'View report revision 1' }))
  expect(await screen.findByText('UAV-OLD → UAV-FIRST')).toBeTruthy()
  expect(screen.queryByText(/UAV-SECOND/)).toBeNull()
})
