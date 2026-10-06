import { users } from './users.js'
import { alerts } from './alerts.js'
import { sensorTypes } from './sensorTypes.js'
import { sensorChannels } from './sensorChannels.js'
import { HUMAN_ROLES } from '../../config/roles.js'
import { hasPermission, PERMISSIONS } from '../../config/permissions.js'
import { MOCK_NOW, offsetMinutes } from './scenario.js'

export function createManagementState() {
  return {
    users: users.map((user) => ({ ...user, roles: [...user.roles], farmIds: [...user.farmIds], lastLoginAt: offsetMinutes(MOCK_NOW, -user.id * 12) })),
    alerts: alerts.map((alert) => ({ ...alert, history: alert.history.map((event) => ({ ...event })) })),
    nextUserId: Math.max(...users.map(({ id }) => id)) + 1,
    nextEventId: Math.max(...alerts.flatMap(({ history }) => history.map(({ id }) => id))) + 1,
    activity: [],
    settings: {
      thresholds: sensorTypes.map((type) => {
        const channel = sensorChannels.find(({ sensorTypeId }) => sensorTypeId === type.id)
        return { sensorTypeId: type.id, min: channel.warningMin, max: channel.warningMax }
      }),
      dataTimeoutMinutes: 90, lowBatteryPercent: 20, webNotifications: true, emailNotifications: false,
    },
  }
}

function numeric(value, min, max, label) {
  if (String(value ?? '').trim() === '' || !Number.isFinite(Number(value)) || Number(value) < min || Number(value) > max) throw new Error(`${label} must be between ${min} and ${max}.`)
  return Number(value)
}

export function applyManagementChange(state, change, actor) {
  const permission = { user: PERMISSIONS.USERS_MANAGE, alert: PERMISSIONS.ALERTS_MANAGE, settings: PERMISSIONS.SETTINGS_MANAGE }[change.kind]
  if (!permission || !hasPermission(actor.activeRole, permission)) throw new Error('Your current role cannot make this change.')
  const at = MOCK_NOW
  const log = (title, description) => [{ id: state.activity.length + 1, userId: actor.id, at, title, description }, ...state.activity]
  if (change.kind === 'user') {
    const existing = state.users.find(({ id }) => id === change.id)
    if (change.id !== undefined && !existing) throw new Error('User not found.')
    const values = { ...existing, ...change.values }
    const fullName = String(values.fullName ?? '').trim()
    const email = String(values.email ?? '').trim().toLowerCase()
    if (!fullName || fullName.length > 120) throw new Error('Enter a name between 1 and 120 characters.')
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) || email.length > 254) throw new Error('Enter a valid email address.')
    if (state.users.some((user) => user.id !== change.id && user.email.toLowerCase() === email)) throw new Error('This email address is already in use.')
    if (!Array.isArray(values.roles) || !values.roles.length || values.roles.some((role) => !HUMAN_ROLES.includes(role))) throw new Error('Assign at least one human role.')
    if (!['ACTIVE', 'INACTIVE'].includes(values.status)) throw new Error('Choose a valid user status.')
    const roles = [...new Set(values.roles)]
    const record = { ...existing, id: existing?.id ?? state.nextUserId, fullName, email, roles,
      activeRole: roles.includes(existing?.activeRole) ? existing.activeRole : roles[0],
      status: values.status, farmIds: existing?.farmIds ?? [], lastLoginAt: existing?.lastLoginAt ?? null }
    return { ...state, users: existing ? state.users.map((user) => user.id === record.id ? record : user) : [...state.users, record],
      nextUserId: existing ? state.nextUserId : state.nextUserId + 1,
      activity: log(existing ? 'Updated user' : 'Added user', record.fullName) }
  }
  if (change.kind === 'alert') {
    const alert = state.alerts.find(({ id }) => id === change.id)
    if (!alert) throw new Error('Alert not found.')
    const note = String(change.note ?? '').trim()
    if (note.length > 1000) throw new Error('Handling notes must be 1,000 characters or fewer.')
    const allowed = { ACKNOWLEDGED: ['OPEN'], CLOSED: ['OPEN', 'ACKNOWLEDGED'], REOPENED: ['CLOSED'], NOTE: ['OPEN', 'ACKNOWLEDGED', 'CLOSED'] }
    if (!allowed[change.action]?.includes(alert.status)) throw new Error('This action is not available for the current alert status.')
    if (change.action === 'NOTE' && !note) throw new Error('Enter a handling note.')
    const record = { ...alert, history: [...alert.history, { id: state.nextEventId, action: change.action, occurredAt: at, userId: actor.id, note }] }
    if (change.action === 'ACKNOWLEDGED') Object.assign(record, { status: 'ACKNOWLEDGED', acknowledgedAt: at, acknowledgedByUserId: actor.id })
    if (change.action === 'CLOSED') Object.assign(record, { status: 'CLOSED', closedAt: at, closedByUserId: actor.id })
    if (change.action === 'REOPENED') Object.assign(record, { status: 'OPEN', closedAt: null, closedByUserId: null, acknowledgedAt: null, acknowledgedByUserId: null })
    return { ...state, alerts: state.alerts.map((item) => item.id === record.id ? record : item), nextEventId: state.nextEventId + 1,
      activity: log(change.action === 'NOTE' ? 'Added handling note' : `${change.action.charAt(0)}${change.action.slice(1).toLowerCase()} alert`, alert.title) }
  }
  const input = change.values
  if (!Array.isArray(input.thresholds) || input.thresholds.length !== sensorTypes.length) throw new Error('Supply defaults for every sensor type.')
  const thresholds = sensorTypes.map((type) => {
    const matches = input.thresholds.filter(({ sensorTypeId }) => sensorTypeId === type.id)
    if (matches.length !== 1) throw new Error('Each sensor type must have one default range.')
    const min = numeric(matches[0].min, type.minValue, type.maxValue, `${type.name} minimum`)
    const max = numeric(matches[0].max, type.minValue, type.maxValue, `${type.name} maximum`)
    if (min >= max) throw new Error(`${type.name}: minimum must be below maximum.`)
    return { sensorTypeId: type.id, min, max }
  })
  const dataTimeoutMinutes = numeric(input.dataTimeoutMinutes, 1, 1440, 'Data timeout')
  const lowBatteryPercent = numeric(input.lowBatteryPercent, 1, 100, 'Low battery threshold')
  if (!Number.isInteger(dataTimeoutMinutes) || !Number.isInteger(lowBatteryPercent)) throw new Error('Timeout and battery threshold must be whole numbers.')
  if (typeof input.webNotifications !== 'boolean' || typeof input.emailNotifications !== 'boolean') throw new Error('Choose valid notification preferences.')
  return { ...state, settings: { thresholds, dataTimeoutMinutes, lowBatteryPercent, webNotifications: input.webNotifications, emailNotifications: input.emailNotifications }, activity: log('Updated settings', 'Monitoring defaults and notification preferences') }
}
