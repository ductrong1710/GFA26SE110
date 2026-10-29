import { act, cleanup, fireEvent, render, renderHook, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, expect, test, vi } from 'vitest'
import { useNotifications } from '../src/services/api/useNotifications'
import NotificationsPanel from '../src/components/app/NotificationsPanel'
import { api } from '../src/services/api'
import { showToast } from '../src/utils/toast'
import { listAll } from '../src/services/api/resources'
vi.mock('../src/services/api', () => ({ api: { request: vi.fn() } }))
vi.mock('../src/utils/toast', () => ({ showToast: vi.fn() }))
vi.mock('../src/services/api/resources', () => ({ listAll: vi.fn() }))
const rows = Array.from({ length: 7 }, (_, i) => ({ id: i + 1, subject: `Notice ${i + 1}`,
  message: 'Message', createdAt: `2026-10-${20 + i}T00:00:00Z`, status: i === 0 ? 'UNREAD' : 'READ' }))
beforeEach(() => { listAll.mockResolvedValue(rows); api.request.mockResolvedValue(null) })
afterEach(() => { cleanup(); vi.clearAllMocks(); vi.useRealTimers() })
test('five newest only, but unread indicator includes older hidden notifications', async () => {
  const { result } = renderHook(() => useNotifications(1, 'ADMINISTRATOR'))
  await waitFor(() => expect(result.current.loading).toBe(false))
  expect(result.current.latest.map(n => n.id)).toEqual([7, 6, 5, 4, 3])
  expect(result.current.hasUnread).toBe(true)
  render(<NotificationsPanel notifications={result.current} />)
  expect(screen.getAllByRole('listitem')).toHaveLength(5)
  expect(screen.queryByText('Notice 1')).toBeNull()
  listAll.mockResolvedValue(rows.map(n => ({ ...n, status: 'READ' })))
  fireEvent.click(screen.getByRole('button', { name: 'Read all' }))
  await waitFor(() => expect(result.current.hasUnread).toBe(false))
  expect(api.request).toHaveBeenCalledWith('/api/notifications/1/read', { method: 'PATCH', toast: false })
})
test('read all marks every unread record and reports failures without clearing the badge', async () => {
  listAll.mockResolvedValue(rows.map(n => ({ ...n, status: 'UNREAD' })))
  const { result } = renderHook(() => useNotifications(1, 'ADMINISTRATOR'))
  await waitFor(() => expect(result.current.hasUnread).toBe(true))
  await act(async () => { await result.current.markRead() })
  expect(api.request).toHaveBeenCalledTimes(7)
  for (const [, options] of api.request.mock.calls) expect(options.toast).toBe(false)
  expect(showToast).toHaveBeenCalledTimes(1)
  expect(showToast).toHaveBeenCalledWith('success', expect.any(String))
  showToast.mockClear()
  api.request.mockRejectedValueOnce(new Error('Cannot update notifications'))
  await act(async () => { await result.current.markRead() })
  expect(result.current.mutationError).toBe('Cannot update notifications')
  expect(showToast).not.toHaveBeenCalled()
  expect(result.current.hasUnread).toBe(true)
})
test('polls while the dropdown is closed and updates unread status', async () => {
  vi.useFakeTimers()
  listAll.mockResolvedValueOnce([]).mockResolvedValue(rows)
  const { result } = renderHook(() => useNotifications(1, 'ADMINISTRATOR'))
  await act(async () => {})
  expect(result.current.hasUnread).toBe(false)
  await act(async () => { await vi.advanceTimersByTimeAsync(10000) })
  expect(result.current.hasUnread).toBe(true)
})
