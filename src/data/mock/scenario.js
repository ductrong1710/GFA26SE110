// Fixed demo clock: 08:18 on 6 October 2026 in Vietnam. Never use Date.now/random.
export const MOCK_NOW = '2026-10-06T01:18:00.000Z'
export const MAIN_FARM_ID = 1
export const ACTIVE_MISSION_ID = 1

export function offsetMinutes(iso, minutes) {
  return new Date(Date.parse(iso) + minutes * 60_000).toISOString()
}
