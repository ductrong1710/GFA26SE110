import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'
import EvidenceImages from './EvidenceImages'
import { DataTable } from './ui'

export function CultivationActivityForm({ season, onSaved, onCancel }) {
  const [images, setImages] = useState([])
  const [reading, setReading] = useState(false)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  async function submit(event) {
    event.preventDefault()
    if (pending || reading) return
    const data = new FormData(event.currentTarget)
    const body = { expectedRevision: season.revision, date: data.get('date'), type: data.get('type'),
      description: data.get('description').trim(), costVnd: data.get('cost') === '' ? null : Number(data.get('cost')),
      notes: data.get('notes').trim() || null, images }
    setError('')
    setPending(true)
    try {
      onSaved(await api.request(`/api/crop-seasons/${season.id}/activities`, { method: 'POST', toastMessage: 'Crop activity recorded successfully.', body }))
    } catch (cause) { setError(cause.message) }
    finally { setPending(false) }
  }
  return <form onSubmit={submit}><fieldset className="management-form" disabled={pending || reading}>
    <legend>Record cultivation activity</legend>
    <label>Activity date<input required name="date" type="date" /></label>
    <label>Activity type<select name="type" required defaultValue=""><option value="">Select an activity</option>
      {['Watering', 'Fertilizing', 'Pesticide', 'Inspection', 'Other'].map(type => <option key={type} value={type.toUpperCase()}>{type}</option>)}</select></label>
    <label>Description<textarea name="description" required maxLength={4000} /></label>
    <label>Activity cost (VND)<input name="cost" type="number" min="0" step="0.01" /></label>
    <label>Notes<textarea name="notes" maxLength={4000} /></label>
    <EvidenceImages images={images} onChange={setImages} onBusyChange={setReading} />
    {error && <p role="alert">{error}</p>}
    <button type="submit">Save activity</button><button type="button" onClick={onCancel}>Cancel</button>
  </fieldset></form>
}

export default function CultivationLog({ season }) {
  const [page, setPage] = useState(1)
  const [activityId, setActivityId] = useState(null)
  const path = `/api/crop-seasons/${season.id}/activities`
  const log = useApiQuery(`${path}:${season.revision}:${page}`, signal => api.request(path, { query: { page }, signal }))
  const evidence = useApiQuery(`${path}:${activityId}:images`, signal => api.request(`${path}/${activityId}/images`, { signal }), activityId !== null)
  function changePage(next) { setActivityId(null); setPage(next) }
  return <section><h3>Cultivation log</h3>
    <ApiState loading={log.loading} error={log.error} reload={log.reload} />
    <DataTable caption="Cultivation activities" rows={log.data?.items ?? []} loading={log.loading} columns={[
      { key: 'date', header: 'Date' }, { key: 'type', header: 'Activity' }, { key: 'description', header: 'Description' },
      { key: 'costVnd', header: 'Cost (VND)' }, { key: 'notes', header: 'Notes' }, { key: 'userId', header: 'Recorded by' },
      { key: 'imageCount', header: 'Evidence', render: (count, row) => count > 0
        ? <button onClick={() => setActivityId(row.id)} aria-label={`View images for activity #${row.id}`}>View images ({count})</button> : '—' },
    ]} footer={<div><button disabled={page === 1} onClick={() => changePage(page - 1)}>Previous activities</button> Page {page} <button
      disabled={page >= (log.data?.pagination?.totalPages ?? 0)} onClick={() => changePage(page + 1)}>Next activities</button></div>} />
    {activityId !== null && <section><h4>Evidence for activity #{activityId}</h4><ApiState loading={evidence.loading} error={evidence.error} reload={evidence.reload} />
      <EvidenceImages images={evidence.data ?? []} /><button onClick={() => setActivityId(null)}>Close images</button></section>}
  </section>
}
