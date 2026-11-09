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
  return <SectionCard title="Customer farm" description="Confirm the customer's account and existing farm membership before sharing survey records.">
    {consultation.farmId ? <p>Linked farm #{consultation.farmId} · Owner #{consultation.ownerId}</p> : <>
      <ApiState {...farms} /><ApiState {...members} />
      <form className="management-form" onSubmit={save}><fieldset disabled={pending} className="management-form">
        <label>Customer farm<select required value={farmId} onChange={event => { setFarmId(event.target.value); setOwnerId('') }}>
          <option value="">Choose farm</option>{(farms.data ?? []).map(farm => <option key={farm.id} value={farm.id}>{farm.name}</option>)}
        </select></label>
        <label>Farm owner<select required value={ownerId} onChange={event => setOwnerId(event.target.value)} disabled={!farmId || members.loading}>
          <option value="">Choose current member</option>{(members.data ?? []).filter(member => member.roles.includes('FarmOwner')).map(member =>
            <option key={member.userId} value={member.userId}>{member.fullName} · {member.email}</option>)}
        </select></label><button className="app-ui-button" disabled={!ownerId || !farmId || members.loading}>Confirm customer farm</button>
      </fieldset>{error && <p role="alert">{error}</p>}</form>
    </>}
  </SectionCard>
}
