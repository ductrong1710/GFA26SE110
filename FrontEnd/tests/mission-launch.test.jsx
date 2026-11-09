import React from 'react'
import { afterEach, expect, test, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import MissionLaunchPanel from '../src/components/app/MissionLaunchPanel'
import { api } from '../src/services/api'

vi.mock('../src/services/api', () => ({ api: { get: vi.fn(), post: vi.fn() } }))
afterEach(() => { cleanup(); vi.clearAllMocks() })
const ready = { state: 'AWAITING_CONFIRMATION', planRevision: 2, session: 'boot', scheduledStartAt: '2026-10-24T10:00:00Z', expiresAt: '2026-10-24T10:01:00Z' }
test('requires both confirmations and posts explicit readiness', async () => {
  api.get.mockResolvedValue(ready)
  api.post.mockResolvedValue({ ...ready, state: 'WAITING_FOR_TIME' })
  render(<MissionLaunchPanel missionId={4} planRevision={2} canManage />)
  const button = await screen.findByRole('button', { name: 'Confirm scheduled flight' })
  expect(button.disabled).toBe(true)
  for (const checkbox of screen.getAllByRole('checkbox')) fireEvent.click(checkbox)
  fireEvent.click(button)
  await waitFor(() => expect(api.post).toHaveBeenCalledWith('/api/missions/4/launch/approve', { confirmAircraftReady: true, confirmAreaClear: true, planRevision: 2, session: 'boot' }))
  await screen.findByText(/Confirmed. The gateway/)
})
test('readers and expired missions cannot confirm', async () => {
  api.get.mockResolvedValue({ ...ready, state: 'MISSED' })
  render(<MissionLaunchPanel missionId={4} planRevision={2} canManage={false} />)
  await screen.findByText(/Launch window expired/)
  expect(screen.queryByRole('button')).toBeNull()
  expect(api.post).not.toHaveBeenCalled()
})
test('a stale displayed route cannot approve a new revision', async () => {
  api.get.mockResolvedValue({ ...ready, planRevision: 3 })
  render(<MissionLaunchPanel missionId={4} planRevision={2} canManage />)
  await screen.findByText(/Waiting for the updated route/)
  expect(screen.queryByRole('button', { name: 'Confirm scheduled flight' })).toBeNull()
})
