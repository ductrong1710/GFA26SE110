import { MOCK_NOW } from './scenario.js'

export const uavs = [
  { id: 1, farmId: 1, code: 'UAV_001', name: 'Valley Scout', model: 'Quadrotor survey platform', status: 'IN_PROGRESS', batteryPercent: 72, assignedOperatorUserId: 2, lastSeenAt: MOCK_NOW, flightHours: 126.4 },
  { id: 2, farmId: 1, code: 'UAV_002', name: 'Irrigation Scout', model: 'Quadrotor survey platform', status: 'READY', batteryPercent: 91, assignedOperatorUserId: 5, lastSeenAt: MOCK_NOW, flightHours: 84.8 },
]
