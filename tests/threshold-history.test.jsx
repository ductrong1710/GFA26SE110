import { expect, test, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import SensorConfiguration from '../src/components/app/SensorConfiguration'
import { api } from '../src/services/api'

vi.mock('../src/context/AuthContext', () => ({ useAuth: () => ({ can: () => true }) }))
vi.mock('../src/services/api', () => ({ api: { request: vi.fn() } }))

test('administrator opens threshold history for the selected channel and can inspect earlier revisions', async () => {
  api.request.mockImplementation(async (_path, { query }) => ({ pagination: { totalPages: 2 }, items: [{
    id: query.page, changedAt: '2026-11-01T10:00:00Z', changedByUserId: query.page === 1 ? 42 : null,
    minValue: 0, maxValue: 35, isEnabled: false,
  }] }))
  render(<SensorConfiguration node={{ channels: [{ id: 17, channelCode: 'temperature', type: { name: 'Temperature' } }] }} types={[]} reload={() => {}} />)
  fireEvent.click(screen.getByRole('button', { name: /threshold history/i }))
  expect(await screen.findByText('User #42')).toBeTruthy()
  expect(screen.getByText('0')).toBeTruthy()
  fireEvent.click(screen.getByRole('button', { name: 'Older changes' }))
  expect(await screen.findByText('Legacy configuration')).toBeTruthy()
  expect(api.request).toHaveBeenCalledWith('/api/sensor-channels/17/threshold/history', expect.objectContaining({ query: { page: 2 } }))
})
