const statusGroups = {
  success: ['ACTIVE', 'ONLINE', 'SUCCESS', 'COMPLETED'],
  info: ['READY', 'IN_PROGRESS', 'SYNCING', 'INFO'],
  warning: ['PENDING', 'PARTIAL', 'WARNING', 'LOW_BATTERY', 'MAINTENANCE'],
  danger: ['FAILED', 'ERROR', 'CRITICAL', 'OFFLINE'],
  neutral: ['DRAFT', 'INACTIVE', 'CANCELLED'],
}

export const STATUS_TONES = Object.freeze(Object.fromEntries(
  Object.entries(statusGroups).flatMap(([tone, statuses]) => statuses.map((status) => [status, tone])),
))

export function getStatusMeta(status) {
  const key = typeof status === 'string' ? status.trim().toUpperCase().replace(/[\s-]+/g, '_') : ''
  return {
    status: key || 'UNKNOWN',
    label: key ? key.toLowerCase().replaceAll('_', ' ').replace(/^./, (letter) => letter.toUpperCase()) : 'Unknown',
    tone: Object.hasOwn(STATUS_TONES, key) ? STATUS_TONES[key] : 'neutral',
  }
}

export function getTone(tone) {
  return ['primary', 'success', 'info', 'warning', 'danger', 'neutral'].includes(tone) ? tone : 'neutral'
}

// Null, NaN, Infinity, and numeric strings are not measured values.
export function getProgress(value, max = 100) {
  if (!Number.isFinite(max) || max <= 0) return { value: null, max: 100, percent: null }
  if (!Number.isFinite(value)) return { value: null, max, percent: null }
  const bounded = Math.min(max, Math.max(0, value))
  return { value: bounded, max, percent: bounded / max * 100 }
}

export function getBatteryState(value) {
  const { value: level } = getProgress(value)
  if (level === null) return { level, tone: 'neutral', label: 'Unknown' }
  if (level <= 10) return { level, tone: 'danger', label: 'Critical battery' }
  if (level <= 20) return { level, tone: 'warning', label: 'Low battery' }
  return { level, tone: 'success', label: 'Battery level' }
}
