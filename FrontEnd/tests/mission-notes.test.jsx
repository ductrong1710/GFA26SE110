import { act, cleanup, renderHook } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import ApiOperationsProvider from '../src/context/ApiOperationsProvider'
import { useOperations } from '../src/context/OperationsContext'
import { api } from '../src/services/api'
import { getMissionMonitoring } from '../src/services/api/monitoring'

vi.mock('../src/context/AuthContext', () => ({ useAuth: () => ({ user: { id: 1 }, activeRole: 'FarmOwner' }) }))
vi.mock('../src/services/api/useApiQuery', () => ({ useApiQuery: () => ({ reload: vi.fn() }) }))
vi.mock('../src/services/api', () => ({ api: { post: vi.fn(), patch: vi.fn(), request: vi.fn(), put: vi.fn() } }))
afterEach(() => { cleanup(); vi.clearAllMocks() })

test('mission creation feedback is sent only for a new persisted mission', async () => {
  api.post.mockResolvedValue({ id: 7 })
  api.request.mockResolvedValue({ id: 7 })
  api.put.mockResolvedValue({ id: 7 })
  const wrapper = ({ children }) => <MemoryRouter><ApiOperationsProvider>{children}</ApiOperationsProvider></MemoryRouter>
  const { result } = renderHook(() => useOperations(), { wrapper })
  await act(async () => result.current.savePlannedMission({}, true))
  expect(api.request).toHaveBeenCalledWith('/api/missions', expect.objectContaining({ method: 'POST', toastMessage: 'UAV mission created successfully.' }))
  expect(result.current.missionWizard.plan.missionId).toBe(7)
  await act(async () => result.current.savePlannedMission({}, true))
  expect(api.request).toHaveBeenCalledTimes(1)
  expect(api.put).toHaveBeenCalledWith('/api/missions/7', expect.any(Object))
  expect(api.post).not.toHaveBeenCalled()
})

test('failed mission creation leaves the draft available for correction', async () => {
  api.request.mockRejectedValueOnce(new Error('Select equipment in an active rental.'))
  const wrapper = ({ children }) => <MemoryRouter><ApiOperationsProvider>{children}</ApiOperationsProvider></MemoryRouter>
  const { result } = renderHook(() => useOperations(), { wrapper })
  await act(async () => {
    await expect(result.current.savePlannedMission({}, true)).rejects.toThrow('Select equipment in an active rental.')
  })
  expect(result.current.missionWizard.plan.missionId).toBeNull()
  expect(api.post).not.toHaveBeenCalled()
})

test('manual running reports preserve the observation through the status endpoint', async () => {
  const wrapper = ({ children }) => <MemoryRouter><ApiOperationsProvider>{children}</ApiOperationsProvider></MemoryRouter>
  const { result } = renderHook(() => useOperations(), { wrapper })
  await act(async () => result.current.reportMission({ missionId: 7, status: 'RUNNING', note: 'Flight observed locally' }))
  expect(api.patch).toHaveBeenCalledWith('/api/missions/7/status', { status: 'RUNNING', note: 'Flight observed locally' })
  expect(api.post).not.toHaveBeenCalled()
})

test('standalone operational notes use the notes endpoint without a status change', async () => {
  const wrapper = ({ children }) => <MemoryRouter><ApiOperationsProvider>{children}</ApiOperationsProvider></MemoryRouter>
  const { result } = renderHook(() => useOperations(), { wrapper })
  const images = [{ contentType: 'image/png', dataBase64: btoa('photo') }]
  await act(async () => result.current.reportMission({ missionId: 7, note: 'Inspect sensor', images }))
  expect(api.post).toHaveBeenCalledWith('/api/missions/7/notes', { note: 'Inspect sensor', images })
  expect(api.patch).not.toHaveBeenCalled()
})

test('monitoring exposes persisted operational notes with author and timestamp', () => {
  const state = { missions: [{ id: 7, farmId: 2 }], missionTargets: [], missionWaypoints: [], sensorNodes: [], uavs: [], gateways: [], collectionAttempts: [],
    missionLogs: [{ id: 11, missionId: 7, logType: 'OPERATIONAL_NOTE', message: 'Inspect sensor', imageCount: 1, userId: 4, createdAt: '2026-10-29T10:00:00Z' }] }
  const data = getMissionMonitoring(7, state, { farms: [{ id: 2 }] })
  expect(data.notes).toEqual([{ id: 11, note: 'Inspect sensor', imageCount: 1, actorName: 'User #4', at: '2026-10-29T10:00:00Z' }])
})
test('manual completion submits explicit operator confirmation', async () => {
  const wrapper = ({ children }) => <MemoryRouter><ApiOperationsProvider>{children}</ApiOperationsProvider></MemoryRouter>
  const { result } = renderHook(() => useOperations(), { wrapper })
  await act(async () => result.current.reportMission({ missionId: 7, status: 'COMPLETED', note: 'Landed without collection data', manualCompletion: true }))
  expect(api.post).toHaveBeenCalledWith('/api/missions/7/complete', expect.objectContaining({ manualCompletion: true, note: 'Landed without collection data' }))
})
