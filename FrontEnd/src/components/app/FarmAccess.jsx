import { useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS } from '../../config/permissions'
import { useApiQuery } from '../../services/api/useApiQuery'
import { listAll } from '../../services/api/resources'
import { api } from '../../services/api'
import ApiState from './ApiState'
import { SectionCard, DataTable } from './ui'

export default function FarmAccess({ farm, onChanged }) {
  const { user, activeRole, can } = useAuth()
  const [selected, setSelected] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const admin = can(PERMISSIONS.USERS_MANAGE)
  const query = useApiQuery(`members:${user.id}:${activeRole}:${farm.id}`, async signal => {
    const [members, users] = await Promise.all([listAll(`/api/farms/${farm.id}/members`, {}, signal), listAll('/api/users', {}, signal)])
    return { members, users }
  }, admin)
  const mutate = async action => {
    setPending(true); setError('')
    try { await action(); query.reload(); await onChanged?.() } catch (e) { setError(e.message) } finally { setPending(false) }
  }
  return <SectionCard title="Farm access & status">
    {error && <p role="alert">{error}</p>}
    {can(PERMISSIONS.FARMS_MANAGE) && <button className="app-ui-button" disabled={pending} onClick={() => mutate(() => api.patch(`/api/farms/${farm.id}/status`, { isActive: !farm.isActive }))}>{farm.isActive ? 'Deactivate' : 'Activate'} Farm</button>}
    {admin && <><ApiState {...query} /><form onSubmit={e => { e.preventDefault(); mutate(() => api.post(`/api/farms/${farm.id}/members`, { userId: Number(selected) })) }}><label>Assign member<select required value={selected} onChange={e => setSelected(e.target.value)}><option value="">Choose account</option>{(query.data?.users ?? []).filter(u => !query.data.members.some(m => m.userId === u.id)).map(u => <option key={u.id} value={u.id}>{u.fullName} · {u.email}</option>)}</select></label><button className="app-ui-button" disabled={pending || !selected}>Assign Member</button></form><DataTable caption="Farm members" rows={(query.data?.members ?? []).map(m => ({ ...m, id: m.userId }))} columns={[{ key: 'fullName', header: 'Name' }, { key: 'email', header: 'Email' }, { key: 'roles', header: 'Roles', render: roles => roles.join(', ') }, { key: 'remove', header: 'Access', render: (_, m) => <button className="app-ui-button" disabled={pending} onClick={() => mutate(() => api.request(`/api/farms/${farm.id}/members/${m.userId}`, { method: 'DELETE' }))}>Remove Membership</button> }]} /></>}
  </SectionCard>
}
