import { useLocation } from 'react-router-dom'
import { ManagementContext } from './ManagementContext'
import { useApiQuery } from '../services/api/useApiQuery'
import { listAll } from '../services/api/resources'
import { api } from '../services/api'
import { normalizeUser, toBackendRoles } from '../services/api/roles'
import { ROLES, USER_ASSIGNABLE_ROLES } from '../config/roles'
import { useAuth } from './AuthContext'
import { PERMISSIONS } from '../config/permissions'

export default function ApiManagementProvider({ children }) {
  const { pathname } = useLocation()
  const { user, activeRole, can } = useAuth()
  const users = useApiQuery(`users:${user?.id}:${activeRole}`, signal => listAll('/api/users', {}, signal), pathname === '/app/users' && can(PERMISSIONS.USERS_MANAGE))
  const management = { users: (users.data ?? []).map(user => ({ ...normalizeUser(user), status: user.isActive ? 'ACTIVE' : 'INACTIVE' })), alerts: [], activity: [], settings: { thresholds: [] } }
  const changeManagement = async ({ kind, id, mode, values, action, note }) => {
    if (kind === 'user') {
      if (!id && (!Array.isArray(values.roles) || values.roles.length !== 1)) {
        throw new Error('Select exactly one role when creating a user.')
      }
      if ((!id || mode === 'roles') && values.roles.some(role => !USER_ASSIGNABLE_ROLES.includes(role))) {
        throw new Error(`Only ${USER_ASSIGNABLE_ROLES.map(role => role === ROLES.FARM_OWNER ? 'Farm Owner' : role === ROLES.UAV_DEVICE_OPERATOR ? 'UAV and Device Operator' : 'Agricultural Engineer').join(' and ')} roles can be assigned from this page.`)
      }
      if (!id) await api.post('/api/users', { email: values.email, fullName: values.fullName, password: values.password, roles: toBackendRoles(values.roles) })
      else if (mode === 'roles') await api.put(`/api/users/${id}/roles`, { roles: toBackendRoles(values.roles) })
      else if (mode === 'status') await api.patch(`/api/users/${id}/status`, { isActive: values.status === 'ACTIVE' })
      else await api.put(`/api/users/${id}`, { email: values.email, fullName: values.fullName })
      users.reload()
    } else if (kind === 'alert') {
      const suffix = { ACKNOWLEDGED: 'acknowledge', CLOSED: 'close', NOTE: 'notes' }[action]
      if (!suffix) throw new Error('This alert action is not supported.')
      await api.post(`/api/alerts/${id}/${suffix}`, { note })
    } else throw new Error('Global settings are not editable through the API. Configure individual sensor-channel thresholds.')
  }
  const state = pathname === '/app/users' ? users : {}
  return <ManagementContext.Provider value={{ management, changeManagement, loading: state.loading, error: state.error, reload: state.reload }}>{children}</ManagementContext.Provider>
}
