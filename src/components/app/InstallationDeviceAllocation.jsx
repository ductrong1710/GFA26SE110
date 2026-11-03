import { useState } from 'react'
import { api } from '../../services/api'
import { listAll } from '../../services/api/resources'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'

export default function InstallationDeviceAllocation({ job, onSaved }) {
  const [selected, setSelected] = useState({})
  const [note, setNote] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const catalog = useApiQuery(`installation-device-catalog:${job.farmId}`, async signal => {
    const [sensors, uavs, gateways] = await Promise.all([listAll('/api/sensor-nodes', { farmId: job.farmId, isActive: true }, signal), listAll('/api/uavs', {}, signal), listAll('/api/gateways', {}, signal)])
    return [...sensors.filter(d => d.isActive).map(d => ({ value: `sensor:${d.id}`, label: `Sensor · ${d.deviceCode} · ${d.name}` })),
      ...uavs.filter(d => d.isActive).map(d => ({ value: `uav:${d.id}`, label: `UAV · ${d.code} · ${d.name}` })),
      ...gateways.filter(d => d.isActive).map(d => ({ value: `gateway:${d.id}`, label: `Gateway · ${d.code} · ${d.name}` }))]
  })
  async function save(event) {
    event.preventDefault(); if (pending) return
    setPending(true); setError('')
    try {
      const devices = job.lines.flatMap(line => Array.from({ length: line.quantity }, (_, index) => {
        const [type, id] = selected[`${line.id}:${index}`].split(':')
        return { proposalLineId: line.id, sensorNodeId: type === 'sensor' ? Number(id) : null, uavId: type === 'uav' ? Number(id) : null, gatewayId: type === 'gateway' ? Number(id) : null }
      }))
      await api.request(`/api/installation-jobs/${job.id}/devices`, { method: 'PUT', body: { expectedRevision: job.revision, note, devices } }); onSaved()
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  return <form className="management-form" aria-label="Installation device allocation" onSubmit={save}>
    <ApiState loading={catalog.loading} error={catalog.error} />{error && <p role="alert">{error}</p>}
    <fieldset disabled={pending || catalog.loading || !!catalog.error} className="management-form"><legend>Allocate registered devices</legend>
      <p>Select the complete device set for this installation. Saving a new set preserves the previous allocation history.</p>
      {job.lines.flatMap(line => Array.from({ length: line.quantity }, (_, index) => {
        const key = `${line.id}:${index}`
        return <label key={key}>Device {index + 1} for {line.deviceType}<select required value={selected[key] ?? ''} onChange={event => setSelected({ ...selected, [key]: event.target.value })}>
          <option value="">Choose registered device</option>{catalog.data?.map(d => <option key={d.value} value={d.value} disabled={Object.entries(selected).some(([k, value]) => k !== key && value === d.value)}>{d.label}</option>)}
        </select></label>
      }))}
      <label>Device allocation note<textarea required maxLength={2000} value={note} onChange={event => setNote(event.target.value)} /></label>
      <button type="submit">Save allocated devices</button>
    </fieldset>
  </form>
}
