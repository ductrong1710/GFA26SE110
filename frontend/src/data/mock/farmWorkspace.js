import { farms } from './farms.js'
import { zones } from './zones.js'
import { sensorNodes } from './sensorNodes.js'
import { missions } from './missions.js'
import { uavs } from './uavs.js'
import { gateways } from './gateways.js'
import { MOCK_NOW } from './scenario.js'

export function createFarmWorkspace() {
  return {
    farms: farms.map((farm) => ({ ...farm, description: `${farm.name} agricultural monitoring site.` })),
    zones: zones.map((zone) => ({ ...zone, description: zone.crop,
      boundarySummary: `${zone.areaHectares} hectares; approximate boundary around the recorded center.` })),
    nextFarmId: Math.max(...farms.map(({ id }) => id)) + 1,
    nextZoneId: Math.max(...zones.map(({ id }) => id)) + 1,
    activity: [],
  }
}

export function getDeletionBlock(state, kind, id) {
  if (kind === 'zone') return sensorNodes.some(({ zoneId }) => zoneId === id)
    ? 'This zone has linked sensors. Remove or reassign those sensors before deleting the zone.' : null
  if (state.zones.some(({ farmId }) => farmId === id)) return 'This farm contains zones. Delete its empty zones first.'
  if ([...missions, ...uavs, ...gateways].some(({ farmId }) => farmId === id)) return 'This farm has linked devices or mission history and cannot be deleted.'
  return null
}

export function updateFarmWorkspace(state, { kind, id, values, remove = false, userId }, canManage) {
  if (!canManage) throw new Error('You do not have permission to manage farms and zones.')
  if (!['farm', 'zone'].includes(kind)) throw new Error('Unknown record type.')
  const key = kind === 'farm' ? 'farms' : 'zones'
  const existing = state[key].find((record) => record.id === id)
  if (id !== undefined && !existing) throw new Error('This record no longer exists.')
  if (remove) {
    const blocked = getDeletionBlock(state, kind, id)
    if (blocked) throw new Error(blocked)
    return { ...state, [key]: state[key].filter((record) => record.id !== id),
      activity: [{ id: state.activity.length + 1, userId, at: MOCK_NOW, title: `Deleted ${kind}`, description: existing.name }, ...state.activity] }
  }
  const cleaned = Object.fromEntries(['name', 'description', ...(kind === 'farm' ? ['location'] : ['boundarySummary'])].map((field) => [field, String(values[field] ?? '').trim()]))
  if (!cleaned.name || cleaned.name.length > 120) throw new Error('Enter a name between 1 and 120 characters.')
  if (cleaned.description.length > 500) throw new Error('Description must be 500 characters or fewer.')
  const farmId = existing?.farmId ?? Number(values.farmId)
  if (kind === 'zone' && !state.farms.some(({ id: farmIdValue }) => farmIdValue === farmId)) throw new Error('Choose an existing farm.')
  if (state[key].some((record) => record.id !== id && record.name.toLowerCase() === cleaned.name.toLowerCase() && (kind === 'farm' || record.farmId === farmId))) throw new Error('A record with this name already exists here.')
  for (const [field, min, max] of [['latitude', -90, 90], ['longitude', -180, 180], ['areaHectares', 0.01, 1000000]]) {
    const value = Number(values[field])
    if (String(values[field] ?? '').trim() === '' || !Number.isFinite(value) || value < min || value > max) throw new Error(`Enter a valid ${field} (${min} to ${max}).`)
    cleaned[field] = value
  }
  const counter = kind === 'farm' ? 'nextFarmId' : 'nextZoneId'
  const recordId = id ?? state[counter]
  const defaults = kind === 'farm'
    ? { timezone: 'Asia/Ho_Chi_Minh', createdAt: MOCK_NOW, administratorUserId: userId, ownerUserId: null }
    : { farmId, crop: cleaned.description }
  const record = { ...defaults, ...existing, ...cleaned, id: recordId, status: existing?.status ?? 'ACTIVE', code: existing?.code ?? `${kind.toUpperCase()}_${String(recordId).padStart(3, '0')}` }
  return { ...state, [key]: existing ? state[key].map((item) => item.id === id ? record : item) : [...state[key], record],
    [counter]: existing ? state[counter] : state[counter] + 1,
    activity: [{ id: state.activity.length + 1, userId, at: MOCK_NOW, title: `${existing ? 'Updated' : 'Added'} ${kind}`, description: record.name }, ...state.activity] }
}
