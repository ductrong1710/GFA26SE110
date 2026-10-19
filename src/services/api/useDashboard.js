import { useAuth } from '../../context/AuthContext'
import { useApiQuery } from './useApiQuery'
import { api } from './index'
import { listAll } from './resources'
import { nodeView, alertView, gatewayView, missionView } from './adapters'
import { loadAnalytics } from './useAnalytics'
import { ANALYTICS_DEFAULT_RANGE } from './analytics'

export function useDashboard(farmId, admin = false) {
  const { user, activeRole } = useAuth()
  return useApiQuery(`dashboard:${user?.id}:${activeRole}:${farmId}:${admin}`, async signal => {
    const filters = farmId ? { farmId } : {}
    const [overview, nodes, gateways, uavs, missions, alerts, users, analytics] = await Promise.all([
      api.get('/api/dashboard/overview', filters, signal), farmId ? Promise.resolve(null) : listAll('/api/sensor-nodes', filters, signal), listAll('/api/gateways', {}, signal), listAll('/api/uavs', {}, signal), listAll('/api/missions', filters, signal), listAll('/api/alerts', filters, signal), admin ? listAll('/api/users', {}, signal) : Promise.resolve([]), farmId ? loadAnalytics(farmId, ANALYTICS_DEFAULT_RANGE, signal) : Promise.resolve({}),
    ])
    const running = farmId ? missions.find(m => m.status === 'RUNNING') : null
    const detail = running ? await api.get(`/api/missions/${running.id}`, {}, signal) : null
    const missionRows = missions.map(m => detail?.id === m.id ? missionView(detail) : missionView(m))
    return { overview, management: { users, alerts: alerts.map(a => ({ ...alertView(a), ...(farmId ? { farmId } : {}) })), activity: [], settings: {} }, operations: { ...analytics, sensorNodes: nodes ? nodes.map(nodeView) : analytics.sensorNodes, gateways: gateways.map(gatewayView), uavs, missions: missionRows, missionTargets: (detail?.targets ?? []).map(t => ({ ...t, missionId: detail.id })), missionWaypoints: (detail?.waypoints ?? []).map(w => ({ ...w, missionId: detail.id })), syncBatches: [] } }
  }, admin || Boolean(farmId))
}
