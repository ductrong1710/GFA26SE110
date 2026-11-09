import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import UnresolvedReadings from '../src/components/app/UnresolvedReadings'
import { api } from '../src/services/api/index'
vi.mock('../src/context/AuthContext', () => ({ useAuth: () => ({ user: { id: 1 }, activeRole: 'FARM_OWNER' }) }))
vi.mock('../src/services/api/index', () => ({ api: { get: vi.fn() } }))
afterEach(() => { cleanup(); vi.clearAllMocks() })

test('preserves unknown measurement time and pages the separate archive', async () => {
  api.get.mockImplementation(async (_path, { page }) => ({ items: [{ id: page, sourceRecordKey: `node-${page}`, receivedAt: '2026-10-28T00:00:00Z', payload: { measuredAt: 0, timeSynced: false, temperature: 23 } }], pagination: { totalPages: 2 } }))
  render(<UnresolvedReadings nodeId={7} />)
  await screen.findByText('node-1')
  expect(screen.getByText(/"measuredAt": 0/).textContent).toContain('"timeSynced": false')
  fireEvent.click(screen.getByRole('button', { name: 'Next' }))
  await screen.findByText('node-2')
  await waitFor(() => expect(api.get).toHaveBeenLastCalledWith('/api/sensor-nodes/7/unresolved-records', { page: 2, pageSize: 20 }, expect.any(AbortSignal)))
  expect(screen.getByRole('button', { name: 'Next' }).disabled).toBe(true)
})
