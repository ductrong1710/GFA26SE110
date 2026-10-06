import test from 'node:test'
import assert from 'node:assert/strict'
import * as data from '../src/data/mock/index.js'
import { mockAccounts } from '../src/data/mock/accounts.js'
import { ROLES } from '../src/config/roles.js'

const find = (dataset, id) => {
  const record = data[dataset].find((item) => item.id === id)
  assert.ok(record, `${dataset} missing ID ${id}`)
  return record
}
const datasets = ['farms', 'zones', 'sensorNodes', 'sensorTypes', 'sensorChannels', 'sensorReadings', 'uavs', 'gateways', 'missions', 'missionWaypoints', 'missionTargets', 'collectionAttempts', 'syncBatches', 'alerts', 'notifications', 'users']

test('owner dashboard stays farm-scoped and reconciles indicators with domain records', () => {
  const main = data.getFarmOwnerDashboard(1)
  assert.equal(main.onlineSensors, 11)
  assert.equal(main.nodes.length, 12)
  assert.equal(main.activeMissionCount, 1)
  assert.equal(main.openAlertCount, 6)
  assert.equal(main.health, 'Needs attention')
  assert.equal(data.formatSnapshotAge(main.latestSyncAt), '2 minutes ago')
  assert.equal(main.currentMission.progress.collectedTargets, 8)
  assert.equal(main.importantAlerts[0].severity, 'CRITICAL')
  assert.ok(main.importantAlerts.every(({ status }) => status !== 'CLOSED'))
  assert.ok(main.activity.some(({ title }) => title === 'Mission completed'))
  assert.ok(main.activity.some(({ title }) => title === 'Alert acknowledged'))
  main.environment.forEach((metric) => {
    assert.ok(metric.value !== null)
    assert.equal(metric.series.at(-1).value, metric.value)
  })
  const second = data.getFarmOwnerDashboard(2)
  assert.equal(second.onlineSensors, 3)
  assert.equal(second.currentMission, null)
  assert.equal(second.activeMissionCount, 0)
  assert.equal(second.openAlertCount, 0)
  assert.deepEqual(second.activity, [])
  assert.equal(second.environment.find(({ id }) => id === 2).value, null)
  assert.equal(data.getFarmOwnerDashboard(-1), null)
})

test('all datasets have stable integer identities and valid foreign keys', () => {
  const relations = {
    farmId: 'farms', zoneId: 'zones', sensorNodeId: 'sensorNodes', sensorTypeId: 'sensorTypes',
    sensorChannelId: 'sensorChannels', readingId: 'sensorReadings', uavId: 'uavs', gatewayId: 'gateways',
    missionId: 'missions', waypointId: 'missionWaypoints', missionTargetId: 'missionTargets',
    collectionAttemptId: 'collectionAttempts', userId: 'users', ownerUserId: 'users',
    administratorUserId: 'users', createdByUserId: 'users', operatorUserId: 'users',
    assignedOperatorUserId: 'users', acknowledgedByUserId: 'users', closedByUserId: 'users', alertId: 'alerts',
  }
  function check(record) {
    for (const [key, value] of Object.entries(record)) {
      if (relations[key] && value !== null) find(relations[key], value)
      if (key === 'farmIds') value.forEach((id) => find('farms', id))
      if (Array.isArray(value) && typeof value[0] === 'object') value.forEach(check)
    }
  }
  for (const name of datasets) {
    const records = data[name]
    assert.ok(records.length > 0)
    assert.equal(new Set(records.map(({ id }) => id)).size, records.length)
    records.forEach((record) => { assert.ok(Number.isInteger(record.id) && record.id > 0); check(record) })
  }
  assert.equal(data.getSensorNodesByFarmId(1).length, 12)
  assert.equal(data.sensorTypes.length, 6)
  assert.equal(data.mockFarms, data.farms)
})

test('mission targets, attempts, devices and progress reconcile', () => {
  assert.deepEqual(data.getMissionProgress(1), { completedWaypoints: 6, totalWaypoints: 10, collectedTargets: 8, totalTargets: 12, waypointPercent: 60, collectionPercent: 67, batteryPercent: 72 })
  assert.deepEqual(new Set(data.missions.map(({ status }) => status)), new Set(['IN_PROGRESS', 'READY', 'COMPLETED', 'PARTIAL', 'FAILED']))
  for (const mission of data.missions) {
    const gateway = find('gateways', mission.gatewayId)
    assert.equal(gateway.uavId, mission.uavId)
    assert.equal(gateway.farmId, mission.farmId)
    assert.equal(find('uavs', mission.uavId).farmId, mission.farmId)
  }
  for (const target of data.missionTargets) {
    const mission = find('missions', target.missionId)
    assert.equal(data.getSensorNodeDetails(target.sensorNodeId).farm.id, mission.farmId)
    assert.equal(find('missionWaypoints', target.waypointId).missionId, mission.id)
    const attempts = data.collectionAttempts.filter(({ missionTargetId }) => missionTargetId === target.id)
    if (target.status === 'COLLECTED') assert.equal(attempts.at(-1).status, 'SUCCESS')
    if (target.status === 'FAILED') assert.equal(attempts.at(-1).status, 'FAILED')
    attempts.forEach((attempt, index) => {
      assert.equal(attempt.attemptNo, index + 1)
      assert.equal(attempt.missionId, mission.id)
      assert.equal(attempt.gatewayId, mission.gatewayId)
      assert.ok(attempt.startedAt >= mission.startedAt)
      assert.ok(attempt.finishedAt >= attempt.startedAt)
      assert.ok(attempt.finishedAt <= (mission.completedAt ?? data.MOCK_NOW))
      assert.equal(data.sensorReadings.filter(({ collectionAttemptId }) => collectionAttemptId === attempt.id).length, attempt.recordsReceived)
      if (index) assert.ok(attempt.startedAt > attempts[index - 1].finishedAt)
    })
  }
})

test('readings have valid ranges, chronology and unique source keys', () => {
  assert.equal(new Set(data.sensorReadings.map(({ sourceRecordKey }) => sourceRecordKey)).size, data.sensorReadings.length)
  for (const reading of data.sensorReadings) {
    const channel = find('sensorChannels', reading.sensorChannelId)
    const type = find('sensorTypes', channel.sensorTypeId)
    assert.ok(reading.value >= type.minValue && reading.value <= type.maxValue)
    assert.ok(reading.measuredAt <= reading.collectedAt)
    assert.ok(reading.collectedAt <= data.MOCK_NOW)
    assert.ok(reading.collectedAt <= find('sensorNodes', channel.sensorNodeId).lastSeenAt)
    if (reading.receivedAt) {
      assert.ok(reading.receivedAt >= reading.collectedAt)
      assert.ok(reading.receivedAt <= data.MOCK_NOW)
    }
    if (reading.collectionAttemptId) {
      const attempt = find('collectionAttempts', reading.collectionAttemptId)
      assert.equal(reading.missionId, attempt.missionId)
      assert.equal(reading.gatewayId, attempt.gatewayId)
      assert.equal(channel.sensorNodeId, find('missionTargets', attempt.missionTargetId).sensorNodeId)
    }
  }
})

test('sync outcomes reconcile without creating duplicate readings', () => {
  assert.deepEqual(new Set(data.syncBatches.map(({ status }) => status)), new Set(['PENDING', 'SYNCING', 'SUCCESS', 'PARTIAL', 'FAILED']))
  for (const batch of data.syncBatches) {
    assert.equal(batch.recordCount, batch.records.length)
    assert.equal(batch.recordCount, batch.acceptedCount + batch.duplicateCount + batch.rejectedCount + batch.pendingCount)
    for (const record of batch.records) {
      if (record.status === 'REJECTED') assert.ok(record.errorCode && record.errorMessage)
      if (!record.readingId) { assert.equal(record.status, 'REJECTED'); continue }
      const reading = find('sensorReadings', record.readingId)
      assert.equal(reading.gatewayId, batch.gatewayId)
      assert.equal(reading.missionId, batch.missionId)
      assert.equal(reading.sourceRecordKey, record.sourceRecordKey)
      assert.equal(reading.sensorChannelId, record.sensorChannelId)
      if (['ACCEPTED', 'DUPLICATE'].includes(record.status)) assert.ok(reading.receivedAt)
      else assert.equal(reading.receivedAt, null)
    }
  }
  const partial = data.syncBatches.find(({ status }) => status === 'PARTIAL')
  const duplicate = partial.records.find(({ status }) => status === 'DUPLICATE')
  assert.ok(partial.records.some((record) => record.status === 'ACCEPTED' && record.readingId === duplicate.readingId))
})

test('alerts and notifications preserve related entities and lifecycle', () => {
  for (const alert of data.alerts) {
    assert.equal(alert.history.at(-1).action, alert.status)
    if (alert.type === 'GATEWAY_ERROR') assert.ok(alert.gatewayId)
    if (alert.readingId) {
      const reading = find('sensorReadings', alert.readingId)
      assert.equal(reading.sensorChannelId, alert.sensorChannelId)
      assert.equal(reading.value, alert.triggeredValue)
      assert.ok(alert.direction === 'HIGH' ? reading.value > alert.thresholdValue : reading.value < alert.thresholdValue)
    }
    alert.history.forEach((event, index) => {
      assert.ok(event.occurredAt <= data.MOCK_NOW)
      if (index) assert.ok(event.occurredAt >= alert.history[index - 1].occurredAt)
    })
  }
  for (const notification of data.notifications) {
    assert.ok(notification.createdAt >= find('alerts', notification.alertId).openedAt)
    if (notification.readAt) assert.ok(notification.readAt >= notification.createdAt)
  }
})

test('selectors respect accepted data, scopes and missing entities without mutation', () => {
  const before = JSON.stringify(datasets.map((name) => data[name]))
  assert.equal(data.getLatestReading(303).source, 'GATEWAY_HISTORY')
  assert.equal(data.getLatestReading(303, { includeUnsynced: true }).missionId, 1)
  assert.deepEqual(data.getReadingsForChannel(303, { from: '2027-01-01T00:00:00Z' }), [])
  assert.equal(data.getSensorNodeDetails(-1), null)
  assert.equal(data.getMissionDetails(-1), null)
  assert.equal(data.getMissionProgress(-1), null)
  assert.equal(data.getLatestReading(-1), null)
  assert.deepEqual(data.getZonesByFarmId(-1), [])
  assert.deepEqual(data.getAlertsForUser(-1), [])
  assert.deepEqual(data.getAlertsForUser(1, 'UNKNOWN'), [])
  assert.ok(data.getAlertsForUser(2).some(({ id }) => id === 6))
  assert.ok(!data.getAlertsForUser(2).some(({ id }) => id === 5))
  assert.ok(data.getAlertsForUser(5).some(({ id }) => id === 5))
  assert.equal(data.getAlertsForUser(1).length, data.alerts.length)
  assert.equal(data.getAlertsForUser(5, ROLES.AGRICULTURAL_ENGINEER).length, data.alerts.length)
  assert.ok(data.getNotificationsForUser(1, { unreadOnly: true }).every(({ userId, readAt }) => userId === 1 && !readAt))
  data.getMissionDetails(1)
  assert.equal(JSON.stringify(datasets.map((name) => data[name])), before)
  assert.ok(data.users.every((user) => !Object.hasOwn(user, 'password')))
  assert.equal(mockAccounts.length, 4)
  assert.ok(mockAccounts.every(({ id, email, password }) => find('users', id).email === email && password === 'Demo@123'))
})
