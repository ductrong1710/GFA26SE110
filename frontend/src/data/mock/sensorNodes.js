import { zones } from './zones.js'
import { MOCK_NOW } from './scenario.js'

const placements = [
  [1, 1, 'North vegetable beds', 88], [2, 1, 'Central vegetable beds', 82],
  [3, 1, 'Dry eastern beds', 64], [4, 1, 'South vegetable beds', 76],
  [5, 2, 'Greenhouse north aisle', 91], [6, 2, 'Greenhouse tomato rows', 79],
  [7, 2, 'Greenhouse ventilation bay', 67], [8, 2, 'Greenhouse cucumber rows', 85],
  [9, 3, 'Reservoir inlet', 73], [10, 3, 'Main irrigation channel', 58],
  [11, 3, 'Remote pump station', 34], [12, 3, 'Reservoir outlet', 12],
  [13, 4, 'Upper strawberry beds', 94], [14, 4, 'Middle strawberry beds', 86],
  [15, 4, 'Lower strawberry beds', 77],
]

export const sensorNodes = placements.map(([id, zoneId, name, batteryPercent]) => {
  const zone = zones.find((item) => item.id === zoneId)
  return {
    id, zoneId, deviceCode: `NODE_${String(id).padStart(3, '0')}`, name,
    model: zoneId === 2 ? 'ESP32 climate node' : 'ESP32 field node',
    status: id === 11 ? 'OFFLINE' : 'ONLINE', batteryPercent,
    latitude: Number((zone.latitude + (id % 4) * 0.00015).toFixed(7)),
    longitude: Number((zone.longitude + (id % 3) * 0.00018).toFixed(7)),
    lastSeenAt: id === 11 ? '2026-10-05T23:00:00.000Z' : id <= 8 ? MOCK_NOW : '2026-10-06T00:35:00.000Z',
    firmwareVersion: '1.4.2', installedAt: '2026-08-20T02:00:00.000Z', isActive: true,
  }
})
