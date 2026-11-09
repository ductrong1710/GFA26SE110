import { expect, test, vi } from 'vitest'
import { useDashboard } from '../src/services/api/useDashboard'

vi.mock('../src/context/AuthContext', () => ({ useAuth: () => ({ user: { id: 1 }, activeRole: 'FarmOwner' }) }))
vi.mock('../src/services/api/useApiQuery', () => ({ useApiQuery: (_key, load) => load() }))
vi.mock('../src/services/api/index', () => ({ api: { get: async () => ({}) } }))
vi.mock('../src/services/api/useAnalytics', () => ({ loadAnalytics: async () => ({ sensorNodes: [] }) }))
vi.mock('../src/services/api/resources', () => ({ listAll: async path =>
  ['/api/uavs', '/api/gateways'].includes(path)
    ? [{ id: 1, rental: { farmId: 10 } }, { id: 2, rental: { farmId: 20 } }, { id: 3 }]
    : [] }))

test('farm dashboard shows only equipment assigned to the selected farm', async () => {
  const { operations } = await useDashboard('10')
  expect(operations.uavs.map(x => x.id)).toEqual([1])
  expect(operations.gateways.map(x => x.id)).toEqual([1])
})

test('unfiltered administrator dashboard retains the complete equipment catalog', async () => {
  const { operations } = await useDashboard(null, true)
  expect(operations.uavs.map(x => x.id)).toEqual([1, 2, 3])
  expect(operations.gateways.map(x => x.id)).toEqual([1, 2, 3])
})
