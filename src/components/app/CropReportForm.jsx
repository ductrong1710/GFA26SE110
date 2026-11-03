import { useState } from 'react'
import { api } from '../../services/api'
import { listAll } from '../../services/api/resources'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'
import EvidenceImages from './EvidenceImages'
import { SectionCard } from './ui'

export default function CropReportForm({ onSaved, busy = false, onBusyChange }) {
  const [farmId, setFarmId] = useState('')
  const [zoneId, setZoneId] = useState('')
  const [nodeId, setNodeId] = useState('')
  const [selected, setSelected] = useState([])
  const [crop, setCrop] = useState('')
  const [description, setDescription] = useState('')
  const [images, setImages] = useState([])
  const [pending, setPending] = useState(false)
  const [reading, setReading] = useState(false)
  const [error, setError] = useState('')
  const farms = useApiQuery('crop-farms', signal => listAll('/api/farms', {}, signal))
  const zones = useApiQuery(`crop-zones:${farmId}`, signal => listAll(`/api/farms/${farmId}/zones`, {}, signal), Boolean(farmId))
  const nodes = useApiQuery(`crop-nodes:${farmId}:${zoneId}`, signal => listAll('/api/sensor-nodes', { farmId: Number(farmId), ...(zoneId ? { zoneId: Number(zoneId) } : {}) }, signal), Boolean(farmId))
  const channels = useApiQuery(`crop-channels:${nodeId}`, signal => listAll(`/api/sensor-nodes/${nodeId}/channels`, {}, signal), Boolean(nodeId))
  async function submit(event) {
    event.preventDefault(); if (pending || reading || busy) return
    setError('')
    if (!farms.data?.some(farm => farm.id === Number(farmId) && farm.isActive) || !crop.trim() || !description.trim()) { setError('Select an active farm and describe the crop symptoms.'); return }
    if (zoneId && !zones.data?.some(zone => zone.id === Number(zoneId))) { setError('Select a zone in this farm.'); return }
    setPending(true); onBusyChange?.(true)
    try {
      const row = await api.request('/api/crop-condition-reports', { method: 'POST', body: { farmId: Number(farmId), zoneId: zoneId ? Number(zoneId) : null,
        crop: crop.trim(), description: description.trim(), sensorChannelIds: selected.map(channel => channel.id), images } })
      setCrop(''); setDescription(''); setImages([]); setSelected([]); onSaved(row)
    } catch (cause) { setError(cause.message) } finally { setPending(false); onBusyChange?.(false) }
  }
  return <SectionCard title="Report crop condition"><form onSubmit={submit} className="management-form" aria-label="Report crop condition">
    {error && <p role="alert">{error}</p>}
    <fieldset disabled={pending || reading || busy} className="management-form"><legend>Crop symptoms</legend>
      <ApiState loading={farms.loading} error={farms.error} reload={farms.reload} />
      <label>Farm<select required value={farmId} onChange={event => { setFarmId(event.target.value); setZoneId(''); setNodeId(''); setSelected([]) }}>
        <option value="">Choose a farm</option>{(farms.data ?? []).filter(farm => farm.isActive).map(farm => <option key={farm.id} value={farm.id}>{farm.name}</option>)}
      </select></label>
      <ApiState loading={zones.loading} error={zones.error} reload={zones.reload} />
      <label>Zone<select disabled={!farmId} value={zoneId} onChange={event => { setZoneId(event.target.value); setNodeId(''); setSelected([]) }}>
        <option value="">Whole farm</option>{(zones.data ?? []).map(zone => <option key={zone.id} value={zone.id}>{zone.name}</option>)}
      </select></label>
      <label>Crop<input required maxLength={200} value={crop} onChange={event => setCrop(event.target.value)} /></label>
      <label>Symptoms<textarea required maxLength={4000} value={description} onChange={event => setDescription(event.target.value)} /></label>
      <EvidenceImages images={images} onChange={setImages} onBusyChange={value => { setReading(value); onBusyChange?.(value) }} />
      <ApiState loading={nodes.loading} error={nodes.error} reload={nodes.reload} />
      <label>Related sensor node (optional)<select disabled={!farmId} value={nodeId} onChange={event => setNodeId(event.target.value)}>
        <option value="">Choose a sensor</option>{(nodes.data ?? []).map(node => <option key={node.id} value={node.id}>{node.name} ({node.deviceCode})</option>)}
      </select></label>
      <ApiState loading={channels.loading} error={channels.error} reload={channels.reload} />
      {(channels.data ?? []).map(channel => <label key={channel.id}><input type="checkbox" checked={selected.some(item => item.id === channel.id)}
        disabled={selected.length >= 50 && !selected.some(item => item.id === channel.id)}
        onChange={event => setSelected(event.target.checked ? [...selected, channel] : selected.filter(item => item.id !== channel.id))} />{channel.name || channel.channelCode}</label>)}
      {selected.length > 0 && <p>Attached channels: {selected.map(channel => <button type="button" key={channel.id} onClick={() => setSelected(selected.filter(item => item.id !== channel.id))}>Remove {channel.name || channel.channelCode}</button>)}</p>}
      <button type="submit">Submit crop report</button>
    </fieldset>{pending && <p role="status">Submitting report…</p>}
  </form></SectionCard>
}
