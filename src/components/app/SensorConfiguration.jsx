import { useState } from 'react'
import { api } from '../../services/api'
import { nullableNumber } from '../../services/api/adapters'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS } from '../../config/permissions'
import { SectionCard } from './ui'

export default function SensorConfiguration({ node, types, reload }) {
  const { can, user } = useAuth()
  const manage = can(PERMISSIONS.SENSORS_MANAGE)
  const configure = user?.backendRoles?.some(role => ['FarmAdministrator', 'FarmEngineer'].includes(role))
  const [edit, setEdit] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState('')
  const openThreshold = async channel => {
    setBusy(true); setError('')
    try { const values = await api.get(`/api/sensor-channels/${channel.id}/threshold`).catch(e => { if (e.status === 404) return { isEnabled: true }; throw e }); setEdit({ ...values, kind: 'threshold', id: channel.id }) }
    catch (e) { setError(e.message) } finally { setBusy(false) }
  }
  const save = async event => {
    event.preventDefault(); setBusy(true); setError('')
    try {
      if (edit.kind === 'type') await api.post('/api/sensor-types', { code: edit.code, name: edit.name, unit: edit.unit || null, description: edit.description || null })
      else if (edit.kind === 'channel') {
        const body = { sensorTypeId: Number(edit.sensorTypeId), channelCode: edit.channelCode, name: edit.name || null, isActive: edit.isActive }
        if (edit.id) await api.put(`/api/sensor-channels/${edit.id}`, body)
        else await api.post(`/api/sensor-nodes/${node.id}/channels`, body)
      } else await api.put(`/api/sensor-channels/${edit.id}/threshold`, { minValue: nullableNumber(edit.minValue), maxValue: nullableNumber(edit.maxValue), dataTimeoutMinutes: nullableNumber(edit.dataTimeoutMinutes), lowBatteryPercent: nullableNumber(edit.lowBatteryPercent), isEnabled: edit.isEnabled })
      setNotice('Configuration saved.'); setEdit(null); reload()
    } catch (e) { setError(e.message) } finally { setBusy(false) }
  }
  const field = (key, label, type = 'text', required = false) => <label>{label}<input type={type} step={type === 'number' ? 'any' : undefined} required={required} value={edit[key] ?? ''} onChange={e => setEdit({ ...edit, [key]: e.target.value })} /></label>
  return <SectionCard title="Channel configuration" description="Sensor types and channel limits are stored in the backend.">
    {error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {manage && <div className="operations-actions"><button className="app-ui-button" disabled={busy} onClick={() => setEdit({ kind: 'channel', sensorTypeId: types[0]?.id ?? '', isActive: true })}>Add Channel</button><button className="app-ui-button" disabled={busy} onClick={() => setEdit({ kind: 'type' })}>Create Sensor Type</button></div>}
    <ul>{node.channels.map(c => <li key={c.id}>{c.channelCode} · {c.type.name} {manage && <button className="app-ui-button" disabled={busy} onClick={() => setEdit({ ...c, kind: 'channel' })}>Edit Channel</button>} {configure && <button className="app-ui-button" disabled={busy} onClick={() => openThreshold(c)}>Configure Threshold</button>}</li>)}</ul>
    {edit && <form onSubmit={save} className="management-form">
      {edit.kind === 'type' && <>{field('code', 'Type Code', 'text', true)}{field('name', 'Type Name', 'text', true)}{field('unit', 'Unit')}{field('description', 'Description')}</>}
      {edit.kind === 'channel' && <><label>Sensor Type<select required value={edit.sensorTypeId} onChange={e => setEdit({ ...edit, sensorTypeId: e.target.value })}><option value="">Choose a type</option>{types.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}</select></label>{field('channelCode', 'Channel Code', 'text', true)}{field('name', 'Channel Name')}<label><input type="checkbox" checked={edit.isActive} onChange={e => setEdit({ ...edit, isActive: e.target.checked })} />Active channel</label></>}
      {edit.kind === 'threshold' && <>{field('minValue', 'Minimum', 'number')}{field('maxValue', 'Maximum', 'number')}{field('dataTimeoutMinutes', 'Data Timeout (minutes)', 'number')}{field('lowBatteryPercent', 'Low Battery (%)', 'number')}<label><input type="checkbox" checked={edit.isEnabled} onChange={e => setEdit({ ...edit, isEnabled: e.target.checked })} />Threshold enabled</label></>}
      <button disabled={busy} className="app-ui-button app-ui-button--primary">{busy ? 'Saving…' : 'Save Configuration'}</button><button type="button" disabled={busy} className="app-ui-button" onClick={() => setEdit(null)}>Cancel</button>
    </form>}
  </SectionCard>
}
