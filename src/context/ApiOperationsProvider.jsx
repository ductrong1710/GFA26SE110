import { useState } from 'react'
import { useLocation } from 'react-router-dom'
import { OperationsContext } from './OperationsContext'
import { useAuth } from './AuthContext'
import { useApiQuery } from '../services/api/useApiQuery'
import { listAll } from '../services/api/resources'
import { api } from '../services/api'
import { nodeView, gatewayView, missionView, equipmentRequest, missionRequest } from '../services/api/adapters'
import { emptyMissionPlan, planFromMission, validateMissionPlan } from '../services/api/planning'

const empty = { sensorNodes: [], uavs: [], gateways: [], missions: [], missionTargets: [], missionWaypoints: [], missionLogs: [], collectionAttempts: [], telemetry: [], syncBatches: [], sensorChannels: [], sensorTypes: [], latestReadings: [], readings: [] }
export default function ApiOperationsProvider({ children }) {
  const { pathname } = useLocation()
  const { user, activeRole } = useAuth()
  const sensorId = pathname.match(/^\/app\/sensors\/(\d+)$/)?.[1]
  const missionId = pathname.match(/^\/app\/missions\/(\d+)$/)?.[1]
  const category = pathname.includes('/sensors') ? 'sensors' : pathname.includes('/devices') ? 'devices' : pathname.includes('/missions') ? 'missions' : pathname.includes('/farms') ? 'farms' : 'none'
  const query = useApiQuery(`operations:${user?.id}:${activeRole}:${category}:${sensorId ?? ''}:${missionId ?? ''}`, async signal => {
    const state = { ...empty }
    if (['sensors', 'farms', 'missions'].includes(category)) state.sensorNodes = (await listAll('/api/sensor-nodes', {}, signal)).map(nodeView)
    if (['devices', 'missions'].includes(category)) {
      const [uavs, gateways, missions] = await Promise.all([listAll('/api/uavs', {}, signal), listAll('/api/gateways', {}, signal), listAll('/api/missions', {}, signal)])
      Object.assign(state, { uavs, gateways: gateways.map(gatewayView), missions: missions.map(missionView) })
    }
    if (sensorId) {
      const [node, channels, types, latest] = await Promise.all([api.get(`/api/sensor-nodes/${sensorId}`, {}, signal), listAll(`/api/sensor-nodes/${sensorId}/channels`, {}, signal), listAll('/api/sensor-types', {}, signal), api.get(`/api/sensor-nodes/${sensorId}/latest`, {}, signal)])
      state.sensorNodes = state.sensorNodes.filter(n => n.id !== node.id).concat(nodeView(node))
      Object.assign(state, { sensorChannels: channels, sensorTypes: types, latestReadings: latest })
    }
    if (missionId) {
      const [m, results, waypoints, logs, attempts, telemetry, latestTelemetry, readings] = await Promise.all([api.get(`/api/missions/${missionId}`, {}, signal), api.get(`/api/missions/${missionId}/results`, {}, signal), api.get(`/api/missions/${missionId}/waypoints`, {}, signal), listAll(`/api/missions/${missionId}/logs`, {}, signal), listAll(`/api/missions/${missionId}/collection-attempts`, {}, signal), listAll(`/api/missions/${missionId}/telemetry`, {}, signal), api.get(`/api/missions/${missionId}/telemetry/latest`, {}, signal).catch(e => { if (e.status === 404) return null; throw e }), listAll('/api/sensor-readings', { missionId }, signal)])
      state.missions = state.missions.filter(x => x.id !== m.id).concat(missionView(m))
      Object.assign(state, { results, missionTargets: results.targets.map(t => ({ ...t, missionId: m.id })), missionWaypoints: waypoints.map(w => ({ ...w, missionId: m.id, altitudeMeters: w.altitudeM })), missionLogs: logs, collectionAttempts: attempts, telemetry, latestTelemetry, readings })
    }
    return state
  }, category !== 'none' && Boolean(user))
  const operations = query.data ?? empty
  const [missionWizard, setMissionWizard] = useState(() => ({ step: 1, plan: emptyMissionPlan() }))
  const changeOperations = async ({ kind, id, action, values }) => {
    const path = { sensor: '/api/sensor-nodes', uav: '/api/uavs', gateway: '/api/gateways' }[kind]
    if (action === 'assign' && kind === 'gateway') await api.post(`${path}/${id}/assign-uav`, { uavId: values.uavId ? Number(values.uavId) : null })
    else if (action === 'toggle' || action === 'status') await api.patch(`${path}/${id}/status`, { status: values.status || 'OFFLINE', isActive: values.isActive })
    else if (id) await api.put(`${path}/${id}`, equipmentRequest(kind, values))
    else await api.post(path, equipmentRequest(kind, values))
    query.reload()
  }
  const startMissionPlan = async mission => {
    const plan = mission ? planFromMission(await api.get(`/api/missions/${mission.id}`)) : emptyMissionPlan()
    setMissionWizard({ step: 1, plan })
  }
  const savePlannedMission = async (workspace, draft) => {
    const plan = missionWizard.plan
    const uav = operations.uavs.find(u => u.id === Number(plan.uavId))
    if (!draft) { const errors = validateMissionPlan(plan, operations, workspace).flat(); if (errors.length) throw new Error(errors.join(' ')) }
    const request = draft && !plan.points.length ? { ...missionRequest({ ...plan, sensorIds: [] }, uav), sensorNodeIds: plan.sensorIds, targetAssignments: null } : missionRequest(plan, uav)
    const result = plan.missionId ? await api.put(`/api/missions/${plan.missionId}`, request) : await api.post('/api/missions', request)
    setMissionWizard({ ...missionWizard, plan: { ...plan, missionId: result.id } })
    query.reload()
    if (!draft && request.scheduledStartAt) await api.post(`/api/missions/${result.id}/schedule`, { scheduledStartAt: request.scheduledStartAt, scheduledEndAt: request.scheduledEndAt })
    return result.id
  }
  const reportMission = async change => {
    const id = change.id ?? change.missionId
    const suffix = { RUNNING: 'start', COMPLETED: 'complete', FAILED: 'fail', CANCELLED: 'cancel' }[change.status]
    if (suffix) await api.post(`/api/missions/${id}/${suffix}`, { operatorNotes: change.notes ?? change.note, note: change.note, failureReason: change.failureReason ?? change.reason ?? change.note, reason: change.reason ?? change.note })
    else await api.patch(`/api/missions/${id}/status`, { status: change.status, note: change.note })
    query.reload()
  }
  return <OperationsContext.Provider value={{ operations, ...query, changeOperations, missionWizard, setMissionWizard, startMissionPlan, savePlannedMission, reportMission, retrySync: async () => { throw new Error('Gateway synchronization is device-only.') } }}>{children}</OperationsContext.Provider>
}
