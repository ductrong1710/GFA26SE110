import test from 'node:test'
import assert from 'node:assert/strict'
import { buildReport, REPORT_DEFAULT_RANGE } from '../src/data/mock/reportSelectors.js'
import { reportCsv, csvCell } from '../src/data/mock/reportCsv.js'
import { getReportTypes } from '../src/config/reports.js'
import { createOperationsState } from '../src/data/mock/operationsState.js'
import { createFarmWorkspace } from '../src/data/mock/farmWorkspace.js'
import { createManagementState } from '../src/data/mock/managementState.js'

const workspace = createFarmWorkspace()
const filters = { ...REPORT_DEFAULT_RANGE, farmId: 1, zoneId: '', type: 'sensor', metricId: '1' }
const build = (role = 'ADMINISTRATOR', values = {}, state = createOperationsState(), management = createManagementState()) => buildReport(role, { ...filters, ...values }, state, workspace, management)

test('report catalog, payloads and export actions respect role scope', () => {
  assert.deepEqual(getReportTypes('UAV_DEVICE_OPERATOR').map(({ id }) => id), ['mission', 'device'])
  assert.deepEqual(getReportTypes('AGRICULTURAL_ENGINEER').map(({ id }) => id), ['sensor', 'alert'])
  assert.throws(() => build('UAV_DEVICE_OPERATOR'), /cannot view/)
  assert.throws(() => build('AGRICULTURAL_ENGINEER', { type: 'mission' }), /cannot view/)
  const owner = build('FARM_OWNER')
  assert.equal(owner.detail, null)
  assert.ok(reportCsv(owner, 'FARM_OWNER').includes('Sensor statistics'))
  assert.throws(() => reportCsv(build('AGRICULTURAL_ENGINEER'), 'AGRICULTURAL_ENGINEER'), /cannot export/)
  assert.throws(() => reportCsv(owner, 'ADMINISTRATOR'), /cannot export/)
})

test('sensor summaries keep units separate, zone filters consistent and previews immutable', () => {
  const state = createOperationsState()
  const report = build('ADMINISTRATOR', { zoneId: '2' }, state)
  assert.equal(report.tables[0].rows.length, 6)
  assert.equal(report.tables[0].rows.find(({ name }) => name === 'pH').average, null)
  assert.equal(report.chart.comparison.zones.length, 1)
  assert.equal(report.chart.comparison.zones[0].id, 2)
  assert.ok(report.detail.rows.every(({ zone }) => zone.includes('Zone B')))
  assert.equal(report.kpis[0][1], report.detail.rows.length)
  const previous = report.detail.rows[0].sensor
  state.sensorNodes.forEach((node) => { node.deviceCode = 'CHANGED' })
  assert.equal(report.detail.rows[0].sensor, previous)
})

test('mission counts and weighted attempt success rate derive from linked outcomes', () => {
  const report = build('UAV_DEVICE_OPERATOR', { type: 'mission' })
  assert.equal(report.kpis.find(([name]) => name === 'Mission Count')[1], 4)
  assert.equal(report.kpis.find(([name]) => name === 'Completed')[1], 1)
  assert.equal(report.kpis.find(([name]) => name === 'Partial')[1], 1)
  assert.equal(report.kpis.find(([name]) => name === 'Failed')[1], 1)
  assert.equal(report.kpis.find(([name]) => name === 'Collection Success Rate')[1], '86%')
  const zone = build('UAV_DEVICE_OPERATOR', { type: 'mission', zoneId: '2' })
  assert.equal(zone.kpis[0][1], 3)
  assert.ok(zone.detail.rows.every(({ targets }) => targets > 0))
})

test('device report filters last seen and zone relationships without fabricating history', () => {
  const report = build('ADMINISTRATOR', { type: 'device' })
  assert.equal(report.kpis.find(([name]) => name === 'Devices')[1], 16)
  assert.equal(report.kpis.find(([name]) => name === 'Offline')[1], 1)
  const state = createOperationsState()
  state.uavs[1].status = 'MAINTENANCE'
  assert.equal(build('ADMINISTRATOR', { type: 'device' }, state).kpis.find(([name]) => name === 'Maintenance')[1], 1)
  const empty = build('ADMINISTRATOR', { type: 'device', from: '2026-10-07T00:00', to: '2026-10-07T08:00' })
  assert.equal(empty.detail.rows.length, 0)
})

test('alert reports use latest lifecycle and engineer environmental scope', () => {
  const report = build('ADMINISTRATOR', { type: 'alert' })
  assert.deepEqual(report.kpis.map(([, value]) => value), [7, 4, 2, 1])
  const engineer = build('AGRICULTURAL_ENGINEER', { type: 'alert' })
  assert.equal(engineer.kpis[0][1], 3)
  assert.ok(engineer.detail.rows.every(({ type }) => ['SENSOR_THRESHOLD', 'SENSOR_DATA_TIMEOUT'].includes(type)))
  const management = createManagementState()
  management.alerts[0].missionId = 1
  assert.equal(build('ADMINISTRATOR', { type: 'alert', zoneId: '1' }, createOperationsState(), management).detail.rows.some(({ id }) => id === 1), false)
})

test('invalid scopes and dates fail before preview generation', () => {
  assert.throws(() => build('ADMINISTRATOR', { farmId: 2, zoneId: 1 }), /zone/)
  assert.throws(() => build('ADMINISTRATOR', { from: filters.to, to: filters.from }), /Start time/)
  assert.throws(() => build('ADMINISTRATOR', { farmId: 999 }), /farm/)
  assert.throws(() => build('ADMINISTRATOR', { metricId: 999 }), /metric/)
})

test('CSV contains all preview rows, quoted Unicode/newlines, and neutralized formulas', () => {
  assert.equal(csvCell('=SUM(A1)'), '"\'=SUM(A1)"')
  assert.equal(csvCell('  +cmd'), '"\'  +cmd"')
  assert.equal(csvCell(-2), '"-2"')
  assert.equal(csvCell('Nông trại, "A"\nNorth'), '"Nông trại, ""A""\nNorth"')
  const report = build()
  const csv = reportCsv(report, 'ADMINISTRATOR')
  assert.ok(csv.startsWith('\uFEFF'))
  assert.ok(csv.includes('Sensor reading details'))
  assert.ok(csv.includes(csvCell(report.detail.rows.at(-1).measuredAt)))
  assert.ok(report.detail.rows.length > 20)
})
