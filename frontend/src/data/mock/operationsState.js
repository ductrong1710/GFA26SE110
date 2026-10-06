import { sensorNodes } from './sensorNodes.js'
import { uavs } from './uavs.js'
import { gateways } from './gateways.js'
import { missions } from './missions.js'
import { missionTargets } from './missionTargets.js'
import { hasPermission, PERMISSIONS } from '../../config/permissions.js'
import { MOCK_NOW } from './scenario.js'

export const SENSOR_PROTOCOLS = ['LORA', 'BLE', 'WIFI']
export const UAV_PLATFORMS = ['MULTIROTOR', 'FIXED_WING', 'VTOL']
export const GATEWAY_TYPES = ['RASPBERRY_PI', 'ESP32', 'MOBILE_COMPUTER']

export function createOperationsState() {
  return {
    sensorNodes: sensorNodes.map((node) => ({ ...node, protocol: node.zoneId === 2 ? 'BLE' : 'LORA',
      serialNumber: `SN-NODE-${String(node.id).padStart(4, '0')}`, macAddress: node.zoneId === 2 ? `02:00:00:00:00:${node.id.toString(16).padStart(2, '0').toUpperCase()}` : '' })),
    uavs: uavs.map((uav) => ({ ...uav, platform: 'MULTIROTOR', gpsSupport: true, telemetrySupport: true })),
    gateways: gateways.map((gateway) => ({ ...gateway, softwareVersion: gateway.firmwareVersion })),
    nextSensorId: Math.max(...sensorNodes.map(({ id }) => id)) + 1,
    nextUavId: Math.max(...uavs.map(({ id }) => id)) + 1,
    nextGatewayId: Math.max(...gateways.map(({ id }) => id)) + 1,
  }
}

export const sensorDisplayStatus = (node) => node.isActive ? node.status : 'INACTIVE'
export function deviceAssignmentLocked(kind, id) {
  return missions.some((mission) => mission.status === 'IN_PROGRESS' && (kind === 'uav' ? mission.uavId === id : mission.gatewayId === id))
}

export function changeOperationsState(state, change, workspace, actor) {
  const isSensor = change.kind === 'sensor'
  if (!['sensor', 'uav', 'gateway'].includes(change.kind) || !hasPermission(actor.activeRole, isSensor ? PERMISSIONS.SENSORS_MANAGE : PERMISSIONS.DEVICES_MANAGE)) throw new Error('Your role cannot manage this equipment.')
  const key = isSensor ? 'sensorNodes' : change.kind === 'uav' ? 'uavs' : 'gateways'
  const existing = state[key].find(({ id }) => id === change.id)
  if (change.id !== undefined && !existing) throw new Error('Equipment record not found.')
  const values = { ...existing, ...change.values }
  if (change.action === 'assign') {
    if (change.kind !== 'gateway' || !existing) throw new Error('Choose an existing gateway.')
    const uavId = values.uavId === '' || values.uavId === null ? null : Number(values.uavId)
    if (deviceAssignmentLocked('gateway', existing.id) || (existing.uavId && deviceAssignmentLocked('uav', existing.uavId)) || (uavId && deviceAssignmentLocked('uav', uavId))) throw new Error('Assignments are locked while a linked mission is in progress.')
    const uav = state.uavs.find(({ id }) => id === uavId)
    if (uavId !== null && (!uav || uav.farmId !== existing.farmId)) throw new Error('Select a UAV in the same farm.')
    if (uavId !== null && state.gateways.some((gateway) => gateway.id !== existing.id && gateway.uavId === uavId)) throw new Error('This UAV already has a gateway. Unassign it first.')
    return { ...state, gateways: state.gateways.map((gateway) => gateway.id === existing.id ? { ...gateway, uavId } : gateway) }
  }
  const name = String(values.name ?? '').trim()
  const codeKey = isSensor ? 'deviceCode' : 'code'
  const code = String(values[codeKey] ?? '').trim().toUpperCase()
  if (!name || name.length > 120) throw new Error('Enter a name between 1 and 120 characters.')
  if (!/^[A-Z0-9][A-Z0-9_-]{2,39}$/.test(code)) throw new Error('Use a 3–40 character code with letters, numbers, underscores or hyphens.')
  if (state[key].some((item) => item.id !== change.id && item[codeKey].toUpperCase() === code)) throw new Error('This device code is already registered.')
  if (existing && code !== existing[codeKey]) throw new Error('Device codes cannot be changed after registration.')
  const counter = isSensor ? 'nextSensorId' : change.kind === 'uav' ? 'nextUavId' : 'nextGatewayId'
  let record = { ...existing, id: existing?.id ?? state[counter], [codeKey]: code, name }
  if (isSensor) {
    const zone = workspace.zones.find(({ id }) => id === Number(values.zoneId))
    if (!zone) throw new Error('Select an existing zone.')
    const originalZone = workspace.zones.find(({ id }) => id === existing?.zoneId)
    if (originalZone && zone.farmId !== originalZone.farmId) throw new Error('Assign existing sensors within their current farm to preserve collection history.')
    if (!SENSOR_PROTOCOLS.includes(values.protocol)) throw new Error('Select a supported protocol.')
    const serialNumber = String(values.serialNumber ?? '').trim()
    const macAddress = String(values.macAddress ?? '').trim().toUpperCase()
    if (!serialNumber && !macAddress) throw new Error('Enter a serial number or MAC address.')
    if (serialNumber.length > 80 || (macAddress && !/^([0-9A-F]{2}:){5}[0-9A-F]{2}$/.test(macAddress))) throw new Error('Enter a valid serial number or six-pair MAC address.')
    if (state.sensorNodes.some((node) => node.id !== existing?.id && ((serialNumber && node.serialNumber.toLowerCase() === serialNumber.toLowerCase()) || (macAddress && node.macAddress === macAddress)))) throw new Error('This MAC address or serial number is already registered.')
    if (typeof values.isActive !== 'boolean') throw new Error('Choose whether the sensor is enabled.')
    const inProgress = existing && missionTargets.some((target) => target.sensorNodeId === existing.id && missions.some((mission) => mission.id === target.missionId && mission.status === 'IN_PROGRESS'))
    if (inProgress && (zone.id !== existing.zoneId || values.isActive !== existing.isActive || values.protocol !== existing.protocol || serialNumber !== existing.serialNumber || macAddress !== existing.macAddress)) throw new Error('Assignment and collection settings are locked for sensors in an active mission.')
    record = { status: 'OFFLINE', batteryPercent: null, lastSeenAt: null, installedAt: MOCK_NOW, firmwareVersion: null, ...record,
      zoneId: zone.id, protocol: values.protocol, serialNumber, macAddress, isActive: values.isActive,
      model: String(values.model ?? '').trim(), latitude: existing?.latitude ?? zone.latitude, longitude: existing?.longitude ?? zone.longitude }
  } else {
    const farmId = existing?.farmId ?? Number(values.farmId)
    if (!workspace.farms.some(({ id }) => id === farmId)) throw new Error('Choose an existing farm.')
    record = { status: 'OFFLINE', batteryPercent: null, lastSeenAt: null, assignedOperatorUserId: actor.id, ...record, farmId }
    if (change.kind === 'uav') {
      if (!UAV_PLATFORMS.includes(values.platform)) throw new Error('Choose a UAV platform.')
      const model = String(values.model ?? '').trim()
      if (!model || model.length > 120) throw new Error('Enter a UAV model.')
      if (typeof values.gpsSupport !== 'boolean' || typeof values.telemetrySupport !== 'boolean') throw new Error('Specify the supported capabilities.')
      record = { ...record, platform: values.platform, model, gpsSupport: values.gpsSupport, telemetrySupport: values.telemetrySupport }
    } else {
      if (!GATEWAY_TYPES.includes(values.gatewayType)) throw new Error('Choose a gateway device type.')
      const softwareVersion = String(values.softwareVersion ?? '').trim()
      if (!softwareVersion || softwareVersion.length > 40) throw new Error('Enter a software version (up to 40 characters).')
      record = { ...record, uavId: existing?.uavId ?? null, gatewayType: values.gatewayType, softwareVersion, firmwareVersion: softwareVersion }
    }
  }
  return { ...state, [key]: existing ? state[key].map((item) => item.id === existing.id ? record : item) : [...state[key], record], [counter]: existing ? state[counter] : state[counter] + 1 }
}
