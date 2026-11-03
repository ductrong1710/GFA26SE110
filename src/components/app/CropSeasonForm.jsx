import { useState } from 'react'
import { api } from '../../services/api'
import { listAll } from '../../services/api/resources'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'

export default function CropSeasonForm({ season, onSaved, onCancel }) {
  const [farmId, setFarmId] = useState(String(season?.farmId ?? ''))
  const [zoneId, setZoneId] = useState(String(season?.zoneId ?? ''))
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)
  const farms = useApiQuery('season-farms', signal => listAll('/api/farms', {}, signal), !season)
  const zones = useApiQuery(`season-zones:${farmId}`, signal => listAll(`/api/farms/${farmId}/zones`, {}, signal), !season && Boolean(farmId))
  async function submit(event) {
    event.preventDefault()
    if (pending) return
    const data = new FormData(event.currentTarget)
    const number = key => data.get(key) ? Number(data.get(key)) : null
    const body = { farmId: Number(farmId), zoneId: zoneId ? Number(zoneId) : null, expectedRevision: season?.revision ?? 0,
      name: data.get('name').trim(), crop: data.get('crop').trim(), areaHectares: number('areaHectares'), cropAgeDays: number('cropAgeDays'),
      startDate: data.get('startDate') || null, expectedEndDate: data.get('expectedEndDate') || null, notes: data.get('notes').trim() || null }
    setPending(true)
    setError('')
    try {
      const result = await api.request(season ? `/api/crop-seasons/${season.id}` : '/api/crop-seasons', { method: season ? 'PUT' : 'POST', body })
      onSaved(result)
    } catch (cause) { setError(cause.message) }
    finally { setPending(false) }
  }
  return <form aria-label="Season details" onSubmit={submit}><fieldset className="management-form" disabled={pending}>
    <legend>{season ? 'Edit season' : 'Create season'}</legend>
    {season ? <p>Farm #{farmId} · {zoneId ? `Zone #${zoneId}` : 'Whole farm'}</p> : <>
      <ApiState loading={farms.loading} error={farms.error} reload={farms.reload} /><label>Farm<select required value={farmId} onChange={event => { setFarmId(event.target.value); setZoneId('') }}>
        <option value="">Choose a farm</option>{farms.data?.filter(f => f.isActive).map(f => <option key={f.id} value={f.id}>{f.name}</option>)}</select></label>
      <ApiState loading={zones.loading} error={zones.error} reload={zones.reload} /><label>Zone<select disabled={!farmId} value={zoneId} onChange={event => setZoneId(event.target.value)}>
        <option value="">Whole farm</option>{zones.data?.map(z => <option key={z.id} value={z.id}>{z.name}</option>)}</select></label></>}
    <label>Season name<input name="name" required maxLength={200} defaultValue={season?.name} /></label>
    <label>Crop<input name="crop" required maxLength={200} defaultValue={season?.crop} /></label>
    <label>Area (ha)<input name="areaHectares" type="number" min="0.0001" step="0.0001" defaultValue={season?.areaHectares} /></label>
    <label>Crop age (days)<input name="cropAgeDays" type="number" min="0" defaultValue={season?.cropAgeDays} /></label>
    <label>Start date<input name="startDate" type="date" defaultValue={season?.startDate} /></label>
    <label>Expected end date<input name="expectedEndDate" type="date" defaultValue={season?.expectedEndDate} /></label>
    <label>Notes<textarea name="notes" maxLength={4000} defaultValue={season?.notes} /></label>
    {error && <p role="alert">{error}</p>}<button type="submit">Save season</button><button type="button" onClick={onCancel}>Cancel</button>
  </fieldset></form>
}
