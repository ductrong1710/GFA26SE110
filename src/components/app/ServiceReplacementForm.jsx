import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'

export default function ServiceReplacementForm({ job, onSaved }) {
  const [page, setPage] = useState(1)
  const [code, setCode] = useState('')
  const [note, setNote] = useState('')
  const [retrieved, setRetrieved] = useState(false)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const query = useApiQuery(`replacement-options:${job.id}:${job.revision}:${page}`, signal => api.request(`/api/service-jobs/${job.id}/replacement-options`, { query: { page }, signal }))
  const device = query.data?.items.find(row => row.code === code)
  function changePage(value) { setCode(''); setPage(value) }
  async function submit(event) {
    event.preventDefault()
    if (pending || query.loading || !device || !retrieved) return
    setPending(true); setError('')
    try {
      onSaved(await api.request(`/api/service-jobs/${job.id}/replacements`, { method: 'POST', body: {
        expectedRevision: job.revision, sensorNodeId: device.sensorNodeId, uavId: device.uavId, gatewayId: device.gatewayId, oldDeviceRetrieved: retrieved, note,
      } }))
    } catch (cause) { setError(cause.message) } finally { setPending(false) }
  }
  return <section aria-label="Device replacement"><h3>Replace device</h3>
    <p>Current device: {job.currentDeviceCode ?? job.deviceCode}. Record the exchange only after retrieving it. Retrieved equipment becomes inactive until an administrator reactivates it after repair.</p>
    <ApiState loading={query.loading} error={query.error} reload={query.reload} />{error && <p role="alert">{error}</p>}
    <form className="management-form" onSubmit={submit}><fieldset disabled={pending} className="management-form">
      <label>Replacement device<select required disabled={query.loading} value={code} onChange={event => setCode(event.target.value)}>
        <option value="">Choose available equipment</option>
        {(query.data?.items ?? []).map(row => <option key={row.code} value={row.code}>{row.code} · {row.name}</option>)}
      </select></label>
      {!query.loading && !query.error && query.data?.items.length === 0 && <p>No available replacement devices.</p>}
      <div className="management-row-actions"><button type="button" disabled={page === 1 || query.loading} onClick={() => changePage(page - 1)}>Previous replacement devices</button>
        <span>Page {page}</span><button type="button" disabled={query.loading || page * 20 >= (query.data?.pagination.totalItems ?? 0)} onClick={() => changePage(page + 1)}>More replacement devices</button></div>
      <label>Replacement note<textarea required maxLength={2000} value={note} onChange={event => setNote(event.target.value)} /></label>
      <label><input type="checkbox" checked={retrieved} onChange={event => setRetrieved(event.target.checked)} />I retrieved the old device</label>
      <button type="submit" disabled={query.loading || !device || !retrieved}>Record device replacement</button>
    </fieldset></form>
  </section>
}
