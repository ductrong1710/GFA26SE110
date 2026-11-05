import { useAuth } from '../../context/AuthContext'
import { useApiQuery } from './useApiQuery'
import { useReadingQuery } from './useReadingQuery'
import { api } from './index'
import { listAll } from './resources'
import { nodeView } from './adapters'
import { validateAnalyticsRange, parseAnalyticsTime } from './analytics'

async function loadCatalog(farmId, signal) {
  const [nodes, types] = await Promise.all([
    listAll('/api/sensor-nodes', { farmId }, signal), listAll('/api/sensor-types', {}, signal),
  ])
  const channels = (await Promise.all(nodes.map(n => listAll(`/api/sensor-nodes/${n.id}/channels`, {}, signal)))).flat()
  const configured = await Promise.all(channels.map(async c => {
    const threshold = await api.get(`/api/sensor-channels/${c.id}/threshold`, {}, signal).catch(e => { if (e.status === 404) return null; throw e })
    return { ...c, warningMin: threshold?.isEnabled ? threshold.minValue : null, warningMax: threshold?.isEnabled ? threshold.maxValue : null, dataTimeoutMinutes: threshold?.isEnabled ? threshold.dataTimeoutMinutes : null }
  }))
  return { sensorNodes: nodes.map(nodeView), sensorTypes: types, sensorChannels: configured, gateways: [], uavs: [], missions: [] }
}
const loadReadings = (farmId, bounds, signal) => listAll('/api/sensor-readings', { farmId, ...bounds }, signal)

export async function loadAnalytics(farmId, filters, signal) {
  const error = validateAnalyticsRange(filters)
  if (error) throw new Error(error)
  const bounds = { from: new Date(parseAnalyticsTime(filters.from)).toISOString(), to: new Date(parseAnalyticsTime(filters.to)).toISOString() }
  const [catalog, readings] = await Promise.all([loadCatalog(farmId, signal), loadReadings(farmId, bounds, signal)])
  return { ...catalog, readings }
}
export function useAnalytics(farmId, fixedRange) {
  const { user, activeRole } = useAuth()
  const key = `analytics:${user?.id}:${activeRole}:${farmId}`
  const catalog = useApiQuery(key, signal => loadCatalog(farmId, signal), Boolean(farmId))
  const readings = useReadingQuery(key, fixedRange, (bounds, signal) => loadReadings(farmId, bounds, signal), Boolean(farmId))
  return { data: catalog.data && { ...catalog.data, readings: readings.data ?? [] }, range: readings.range,
    loading: catalog.loading || readings.loading, error: catalog.error ?? readings.error,
    reload: () => { catalog.reload(); readings.reload() } }
}
