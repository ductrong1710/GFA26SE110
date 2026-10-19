import { useAuth } from '../../context/AuthContext'
import { useApiQuery } from './useApiQuery'
import { listAll } from './resources'
import { api } from './index'
import { farmView, zoneView, nullableNumber } from './adapters'

export function useFarmWorkspace(enabled = true) {
  const { user, activeRole } = useAuth()
  const loadWorkspace = async signal => {
    const farms = (await listAll('/api/farms', {}, signal)).map(farmView)
    const zones = (await Promise.all(farms.map(f => listAll(`/api/farms/${f.id}/zones`, {}, signal)))).flat().map(zoneView)
    return { farms, zones }
  }
  const query = useApiQuery(`farms:${user?.id}:${activeRole}`, loadWorkspace, enabled && Boolean(user))
  const refreshWorkspace = async () => { const data = await loadWorkspace(); query.replaceData(data); return data }
  const changeFarmRecord = async ({ kind, id, values }) => {
    if (kind === 'reload') return refreshWorkspace()
    if (kind === 'farm') {
      const body = { name: values.name, description: values.description || null, latitude: nullableNumber(values.latitude), longitude: nullableNumber(values.longitude) }
      if (id) await api.put(`/api/farms/${id}`, body)
      else await api.post('/api/farms', body)
    } else {
      const body = { name: values.name, description: values.description || null, centerLatitude: nullableNumber(values.latitude), centerLongitude: nullableNumber(values.longitude) }
      if (id) await api.put(`/api/zones/${id}`, body)
      else await api.post(`/api/farms/${values.farmId}/zones`, body)
    }
    return refreshWorkspace()
  }
  return { ...query, farmWorkspace: query.data ?? { farms: [], zones: [] }, changeFarmRecord }
}
