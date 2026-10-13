import { MOCK_NOW } from './scenario.js'

export const gateways = [
  { id: 1, farmId: 1, uavId: 1, code: 'GW_001', name: 'Valley mobile gateway', gatewayType: 'RASPBERRY_PI', status: 'SYNCING', batteryPercent: 84, assignedOperatorUserId: 2, firmwareVersion: '2.3.1', lastSeenAt: MOCK_NOW },
  { id: 2, farmId: 1, uavId: 2, code: 'GW_002', name: 'Irrigation mobile gateway', gatewayType: 'ESP32', status: 'ONLINE', batteryPercent: 89, assignedOperatorUserId: 5, firmwareVersion: '2.3.1', lastSeenAt: MOCK_NOW },
]
