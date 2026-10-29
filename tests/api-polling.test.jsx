import { act, cleanup, renderHook } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import { useApiQuery } from '../src/services/api/useApiQuery'

afterEach(() => { cleanup(); vi.useRealTimers(); vi.restoreAllMocks() })

test('polling updates presence without blanking data and stops on unmount', async () => {
  vi.useFakeTimers()
  const load = vi.fn().mockResolvedValueOnce({ status: 'OFFLINE' }).mockResolvedValue({ status: 'ONLINE' })
  const { result, unmount } = renderHook(() => useApiQuery('presence', load, true, 10000))
  await act(async () => {})
  expect(result.current.data.status).toBe('OFFLINE')
  await act(async () => { await vi.advanceTimersByTimeAsync(10000) })
  expect(load).toHaveBeenCalledTimes(2)
  expect(result.current.data.status).toBe('ONLINE')
  expect(result.current.loading).toBe(false)
  unmount()
  await act(async () => { await vi.advanceTimersByTimeAsync(30000) })
  expect(load).toHaveBeenCalledTimes(2)
})

test('polling never overlaps a slow request and keeps data on transient failure', async () => {
  vi.useFakeTimers()
  let reject
  const load = vi.fn().mockResolvedValueOnce({ status: 'ONLINE' })
    .mockImplementationOnce(() => new Promise((_, fail) => { reject = fail }))
  const { result } = renderHook(() => useApiQuery('slow', load, true, 10000))
  await act(async () => {})
  await act(async () => { await vi.advanceTimersByTimeAsync(40000) })
  expect(load).toHaveBeenCalledTimes(2)
  expect(result.current.data.status).toBe('ONLINE')
  await act(async () => { reject(new Error('Network unavailable')) })
  expect(result.current.error.message).toBe('Network unavailable')
  expect(result.current.data.status).toBe('ONLINE')
})
