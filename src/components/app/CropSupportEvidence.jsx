import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { displayTime } from '../../utils/display'
import ApiState from './ApiState'
import EvidenceImages from './EvidenceImages'
import { DataTable, TablePagination } from './ui'

export function CropHistoryEntry({ update }) {
  return <article>
    <p><strong>{update.action}</strong> · User #{update.userId} · {displayTime(update.createdAt)}</p>
    <p style={{ whiteSpace: 'pre-wrap' }}>{update.content}</p>
    {update.followUpType && <p>Feedback: {update.followUpType}</p>}
    {update.assignedEngineerId && <p>Assigned to engineer #{update.assignedEngineerId}</p>}
    {update.cultivationActivityId && <p>Linked care activity #{update.cultivationActivityId}</p>}
    {update.referralType === 'DEVICE_INCIDENT' && <p>MF-03 device referral: {update.deviceIncidentId ? `incident #${update.deviceIncidentId}` : 'awaiting an incident from the owner'}</p>}
    {update.referralType === 'UAV_VIDEO' && <p>Video requested — pending MF-02 video/service integration. Manual consultation remains available.</p>}
    {update.recommendSiteSurvey && <p>On-site survey recommended.</p>}
    <EvidenceImages images={update.images} />
  </article>
}

export function CropHistory({ report }) {
  const [page, setPage] = useState(1)
  const history = useApiQuery(`crop-history:${report.id}:${report.revision}:${page}`, signal => api.request(`/api/crop-condition-reports/${report.id}/history`, { query: { page }, signal }), report.historyCount > 20)
  const rows = report.historyCount > 20 ? history.data?.items ?? [] : report.updates ?? []
  return <section><h3>Consultation history</h3>
    <ApiState loading={history.loading} error={history.error} reload={history.reload} />
    {rows.map(update => <CropHistoryEntry key={update.id} update={update} />)}
    {report.historyCount > 20 && <div><p>Newest updates first.</p><button disabled={page === 1 || history.loading} onClick={() => setPage(page - 1)}>Previous history</button>
      <span> Page {page} </span><button disabled={history.loading || page >= (history.data?.pagination?.totalPages ?? 0)} onClick={() => setPage(page + 1)}>Next history</button></div>}
  </section>
}

function EvidenceTable({ report, resource, title, columns, query, onActivity }) {
  const [page, setPage] = useState(1)
  const context = useApiQuery(`crop-evidence:${report.id}:${resource}:${JSON.stringify(query)}:${page}`, signal => api.request(`/api/crop-condition-reports/${report.id}/${resource}`, { query: { ...query, page }, signal }))
  return <section><h3>{title}</h3><ApiState loading={context.loading} error={context.error} reload={context.reload} />
    {!context.loading && !context.error && !context.data?.items?.length && <p>No data recorded in this scope.</p>}
    <DataTable caption={title} rows={context.data?.items ?? []} loading={context.loading} columns={onActivity ? [...columns,
      { key: 'imageCount', header: 'Evidence', render: (count, row) => count > 0 ? <button onClick={() => onActivity(row.id)}>View images for activity #{row.id}</button> : 'No images recorded' },
    ] : columns} footer={(context.data?.pagination?.totalItems ?? 0) > 0 && <TablePagination page={page} totalPages={context.data?.pagination?.totalPages} pageSize={context.data?.pagination?.pageSize} totalItems={context.data?.pagination?.totalItems} onPageChange={setPage} disabled={context.loading} label={`${title} pagination`} />} />
  </section>
}

export default function CropSupportEvidence({ report }) {
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [channel, setChannel] = useState('')
  const [query, setQuery] = useState({})
  const [error, setError] = useState('')
  const [activityId, setActivityId] = useState(null)
  const images = useApiQuery(`crop-activity-images:${report.id}:${activityId}`, signal => api.request(`/api/crop-condition-reports/${report.id}/care-activities/${activityId}/images`, { signal }), activityId !== null)
  const defaultRange = report.observedAt ? `${displayTime(new Date(new Date(report.observedAt).getTime() - 7 * 86400000).toISOString())} – ${displayTime(new Date(new Date(report.observedAt).getTime() + 86400000).toISOString())}` : null
  function filter(event) {
    event.preventDefault(); setError('')
    if (Boolean(from) !== Boolean(to) || from && (new Date(to) < new Date(from) || new Date(to) - new Date(from) > 31 * 86400000)) { setError('Choose both dates in order, at most 31 days apart.'); return }
    setQuery({ ...(from ? { from: new Date(from).toISOString(), to: new Date(to).toISOString() } : {}), ...(channel ? { sensorChannelId: Number(channel) } : {}) }); setActivityId(null)
  }
  return <section><h3>Evidence scope</h3>
    <p>Current channels in farm #{report.farmId}{report.zoneId ? `, zone #${report.zoneId}` : ''}. {query.from ? `Window: ${displayTime(query.from)} – ${displayTime(query.to)}.` : defaultRange ? `Observation window: ${defaultRange} (7 days before to 1 day after).` : 'Observation time not recorded: showing all available history for this report (legacy scope).'}</p>
    <p>Care journal dates are compared by UTC calendar day. Invalid measurements are labelled as errors; missing data remains unknown.</p>
    <form onSubmit={filter}><label>Evidence from (local time)<input type="datetime-local" value={from} onChange={e => setFrom(e.target.value)} /></label>
      <label>Evidence to (local time)<input type="datetime-local" value={to} onChange={e => setTo(e.target.value)} /></label>
      <label>Attached channel<select value={channel} onChange={e => setChannel(e.target.value)}><option value="">All attached channels</option>
        {(report.sensorChannelIds ?? []).map(id => <option key={id} value={id}>Channel #{id}</option>)}</select></label>
      <button>Apply evidence window</button><button type="button" onClick={() => { setFrom(''); setTo(''); setChannel(''); setQuery({}); setError('') }}>Reset evidence window</button>
      {error && <p role="alert">{error}</p>}
    </form>
    <EvidenceTable key={`readings:${JSON.stringify(query)}`} report={report} resource="sensor-readings" title="Supporting sensor readings" query={query} columns={[
      { key: 'channelCode', header: 'Channel' }, { key: 'value', header: 'Value', render: (value, row) => value == null ? 'Not recorded' : row.isValid ? value : `Invalid reading (${value})` },
      { key: 'unit', header: 'Unit', render: value => value ?? 'Unknown' }, { key: 'measuredAt', header: 'Measured', render: displayTime },
      { key: 'isValid', header: 'Quality', render: (value, row) => value ? 'Valid' : `Error: ${row.qualityStatus ?? 'Unspecified'}` },
    ]} />
    <EvidenceTable key={`alerts:${JSON.stringify(query)}`} report={report} resource="alerts" title="Related alerts" query={query} columns={[
      { key: 'type', header: 'Type' }, { key: 'severity', header: 'Severity' }, { key: 'status', header: 'Status' }, { key: 'message', header: 'Message' },
      { key: 'triggeredValue', header: 'Value', render: value => value ?? 'Unknown' }, { key: 'openedAt', header: 'Opened', render: displayTime },
    ]} />
    {report.cropSeasonId ? <EvidenceTable key={`care:${JSON.stringify(query)}`} report={report} resource="care-activities" title="Linked season care journal" query={query} onActivity={setActivityId} columns={[
      { key: 'date', header: 'Date' }, { key: 'type', header: 'Activity' }, { key: 'description', header: 'Description' }, { key: 'notes', header: 'Notes' },
    ]} /> : <p>No linked season: care journal context is unavailable.</p>}
    {activityId && <section><h4>Evidence for activity #{activityId}</h4><ApiState loading={images.loading} error={images.error} reload={images.reload} />
      <EvidenceImages images={images.data ?? []} /><button onClick={() => setActivityId(null)}>Close activity images</button></section>}
  </section>
}

export function FollowUpActivityPicker({ seasonId, value, onChange }) {
  const [page, setPage] = useState(1)
  // The reporter uses their existing season access; later care can be outside the symptom window.
  const rows = useApiQuery(`follow-up-activities:${seasonId}:${page}`, signal => api.request(`/api/crop-seasons/${seasonId}/activities`, { query: { page }, signal }))
  return <div><ApiState loading={rows.loading} error={rows.error} reload={rows.reload} />
    <label>Linked care activity (optional)<select value={value} onChange={e => onChange(e.target.value)}><option value="">No activity link</option>
      {value && !rows.data?.items?.some(row => String(row.id) === value) && <option value={value}>Activity #{value}</option>}
      {(rows.data?.items ?? []).map(row => <option key={row.id} value={row.id}>#{row.id} · {row.date} · {row.description}</option>)}</select></label>
    <button type="button" disabled={page === 1} onClick={() => setPage(page - 1)}>Previous care options</button>
    <button type="button" disabled={page >= (rows.data?.pagination?.totalPages ?? 0)} onClick={() => setPage(page + 1)}>Next care options</button>
  </div>
}
