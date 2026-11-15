import { useState } from 'react'
import { api } from '../../services/api'
import { listAll } from '../../services/api/resources'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'
import { SectionCard } from './ui'

export default function ConsultationBinding({ consultation, onSaved }) {
  const [farmId, setFarmId] = useState('')
  const [ownerId, setOwnerId] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const farms = useApiQuery('consultation-binding-farms', signal => listAll('/api/farms', {}, signal), !consultation.farmId)
  const members = useApiQuery(`consultation-binding-members:${farmId}`, signal => listAll(`/api/farms/${farmId}/members`, {}, signal), Boolean(farmId) && !consultation.farmId)
  async function save(event) {
    event.preventDefault(); if (pending) return
    setPending(true); setError('')
    try { const updated = await api.request(`/api/consultations/${consultation.id}/customer-farm`, { method: 'PUT', body: { farmId: Number(farmId), ownerId: Number(ownerId) } }); onSaved(updated) }
    catch (err) { setError(err.message) } finally { setPending(false) }
  }
  return <SectionCard title="Nông trại của khách hàng" description="Xác nhận tài khoản khách hàng và tư cách thành viên nông trại trước khi chia sẻ hồ sơ khảo sát.">
    {consultation.farmId ? <p>Nông trại đã liên kết #{consultation.farmId} · Chủ nông trại #{consultation.ownerId}</p> : <>
      <ApiState vietnamese {...farms} /><ApiState vietnamese {...members} />
      <form className="management-form" onSubmit={save}><fieldset disabled={pending} className="management-form">
        <label>Nông trại của khách hàng<select required value={farmId} onChange={event => { setFarmId(event.target.value); setOwnerId('') }}>
          <option value="">Chọn nông trại</option>{(farms.data ?? []).map(farm => <option key={farm.id} value={farm.id}>{farm.name}</option>)}
        </select></label>
        <label>Chủ nông trại<select required value={ownerId} onChange={event => setOwnerId(event.target.value)} disabled={!farmId || members.loading}>
          <option value="">Chọn thành viên hiện tại</option>{(members.data ?? []).filter(member => member.roles.includes('FarmOwner')).map(member =>
            <option key={member.userId} value={member.userId}>{member.fullName} · {member.email}</option>)}
        </select></label><button className="app-ui-button" disabled={!ownerId || !farmId || members.loading}>Xác nhận nông trại của khách hàng</button>
      </fieldset>{error && <p role="alert">{error}</p>}</form>
    </>}
  </SectionCard>
}
