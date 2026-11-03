import { MemoryRouter } from 'react-router-dom'
import { cleanup, render as renderComponent, screen } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import MaintenanceHistory from '../src/components/app/MaintenanceHistory'
import { api } from '../src/services/api'
afterEach(() => { cleanup(); vi.restoreAllMocks() })
test('shows the recorded policy, due date and generated job rather than the current policy', async () => {
  vi.spyOn(api, 'request').mockResolvedValue({ items: [{ id: 1, jobId: 8, deviceCode: 'UAV-OLD', dueAt: '2026-10-01T00:00:00Z', createdAt: '2026-10-02T00:00:00Z', policy: 'Original inspection policy', status: 'CLOSED' }], pagination: { totalItems: 1 } })
  render(<MaintenanceHistory scheduleId={4} />)
  expect(await screen.findByText('Original inspection policy')).toBeTruthy()
  expect(screen.getByText('UAV-OLD')).toBeTruthy()
  expect(screen.getByText('8')).toBeTruthy()
  expect(screen.getByText('CLOSED')).toBeTruthy()
})

function render(component) { return renderComponent(<MemoryRouter>{component}</MemoryRouter>) }
