import { sensorReadings } from './sensorReadings.js'

const thresholdReading = (channelId) => sensorReadings.find((reading) => reading.sensorChannelId === channelId && reading.source === 'GATEWAY_HISTORY' && reading.measuredAt === '2026-10-06T00:35:00.000Z')

export const alerts = [
  { id: 1, type: 'SENSOR_THRESHOLD', direction: 'HIGH', severity: 'WARNING', status: 'OPEN', title: 'Greenhouse temperature high', message: 'Check ventilation near NODE_005.', sensorNodeId: 5, sensorChannelId: 501, readingId: thresholdReading(501).id, triggeredValue: 36.8, thresholdValue: 35, openedAt: '2026-10-06T00:36:00.000Z' },
  { id: 2, type: 'SENSOR_THRESHOLD', direction: 'LOW', severity: 'CRITICAL', status: 'ACKNOWLEDGED', title: 'Eastern beds soil moisture low', message: 'Inspect the irrigation line near NODE_003.', sensorNodeId: 3, sensorChannelId: 303, readingId: thresholdReading(303).id, triggeredValue: 15.2, thresholdValue: 25, openedAt: '2026-10-06T00:36:00.000Z', acknowledgedAt: '2026-10-06T00:40:00.000Z', acknowledgedByUserId: 4 },
  { id: 3, type: 'SENSOR_DATA_TIMEOUT', severity: 'WARNING', status: 'OPEN', title: 'Remote pump sensor offline', message: 'NODE_011 has not reported for more than 90 minutes.', sensorNodeId: 11, openedAt: '2026-10-06T00:31:00.000Z' },
  { id: 4, type: 'SENSOR_LOW_BATTERY', severity: 'WARNING', status: 'OPEN', title: 'Reservoir outlet battery low', message: 'NODE_012 battery is at 12%.', sensorNodeId: 12, triggeredValue: 12, thresholdValue: 20, openedAt: '2026-10-06T00:36:00.000Z' },
  { id: 5, type: 'MISSION_ERROR', severity: 'CRITICAL', status: 'ACKNOWLEDGED', title: 'Gateway diagnostic mission failed', message: 'MSN-2026-005 stopped after a gateway buffer error.', missionId: 5, uavId: 2, gatewayId: 2, openedAt: '2026-10-05T23:24:00.000Z', acknowledgedAt: '2026-10-05T23:28:00.000Z', acknowledgedByUserId: 5 },
  { id: 6, type: 'COLLECTION_FAILED', severity: 'WARNING', status: 'OPEN', title: 'Collection failed at remote pump', message: 'NODE_011 did not respond after two collection attempts.', missionId: 4, missionTargetId: 411, collectionAttemptId: 4112, sensorNodeId: 11, uavId: 2, gatewayId: 2, openedAt: '2026-10-06T00:11:00.000Z' },
  { id: 7, type: 'GATEWAY_ERROR', severity: 'ERROR', status: 'CLOSED', title: 'Gateway cache unavailable', message: 'GW_002 cache was repaired before the recovery run.', missionId: 5, gatewayId: 2, uavId: 2, openedAt: '2026-10-05T23:23:50.000Z', acknowledgedAt: '2026-10-05T23:28:00.000Z', acknowledgedByUserId: 5, closedAt: '2026-10-06T00:00:00.000Z', closedByUserId: 5 },
].map((alert) => ({
  farmId: 1, sensorNodeId: null, sensorChannelId: null, readingId: null, missionId: null,
  missionTargetId: null, collectionAttemptId: null, gatewayId: null, uavId: null,
  acknowledgedAt: null, acknowledgedByUserId: null, closedAt: null, closedByUserId: null,
  ...alert,
  history: [
    { id: alert.id * 10 + 1, action: 'OPEN', occurredAt: alert.openedAt, userId: null },
    ...(alert.acknowledgedAt ? [{ id: alert.id * 10 + 2, action: 'ACKNOWLEDGED', occurredAt: alert.acknowledgedAt, userId: alert.acknowledgedByUserId }] : []),
    ...(alert.closedAt ? [{ id: alert.id * 10 + 3, action: 'CLOSED', occurredAt: alert.closedAt, userId: alert.closedByUserId }] : []),
  ],
}))
