import { useAuth } from '../../context/AuthContext'
import { useApiQuery } from './useApiQuery'
import { api } from './index'
import { listAll } from './resources'
import { nodeView } from './adapters'
import { validateAnalyticsRange, parseAnalyticsTime } from './analytics'

export async function loadAnalytics(farmId, filters, signal) {
  const rangeError = validateAnalyticsRange(filters)
  if (rangeError) throw new Error(rangeError)
  const [nodes, types, readings] = await Promise.all([
    listAll('/api/sensor-nodes', { farmId }, signal), listAll('/api/sensor-types', {}, signal),
    listAll('/api/sensor-readings', { farmId, from: new Date(parseAnalyticsTime(filters.from)).toISOString(), to: new Date(parseAnalyticsTime(filters.to)).toISOString() }, signal),
  ])
  const channels = (await Promise.all(nodes.map(n => listAll(`/api/sensor-nodes/${n.id}/channels`, {}, signal)))).flat()
  const configured = await Promise.all(channels.map(async c => {
    const threshold = await api.get(`/api/sensor-channels/${c.id}/threshold`, {}, signal).catch(e => { if (e.status === 404) return null; throw e })
    return { ...c, warningMin: threshold?.isEnabled ? threshold.minValue : null, warningMax: threshold?.isEnabled ? threshold.maxValue : null, dataTimeoutMinutes: threshold?.isEnabled ? threshold.dataTimeoutMinutes : null }
  }))
  return { sensorNodes: nodes.map(nodeView), sensorTypes: types, sensorChannels: configured, readings, gateways: [], uavs: [], missions: [] }
}
export function useAnalytics(farmId, filters) {
  const { user, activeRole } = useAuth()
  return useApiQuery(`analytics:${user?.id}:${activeRole}:${farmId}:${filters.from}:${filters.to}`, signal => loadAnalytics(farmId, filters, signal), Boolean(farmId) && !user?.isMock)
}
