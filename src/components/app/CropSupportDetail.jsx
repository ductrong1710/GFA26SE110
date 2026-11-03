import { useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS as P } from '../../config/permissions'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { displayTime } from '../../utils/display'
import ApiState from './ApiState'
import EvidenceImages from './EvidenceImages'
import { DataTable, SectionCard, StatusBadge } from './ui'

function SupportingSeasons({ reportId }) {
  const [page, setPage] = useState(1)
  const { data, loading, error, reload } = useApiQuery(`crop-support-seasons:${reportId}:${page}`,
    signal => api.request(`/api/crop-condition-reports/${reportId}/crop-seasons`, { query: { page }, signal }))
  return <section><h3>Supporting crop seasons</h3>
    <ApiState loading={loading} error={error} reload={reload} />
    <DataTable caption="Crop seasons for this report" rows={data?.items ?? []} loading={loading} columns={[
      { key: 'name', header: 'Season' }, { key: 'crop', header: 'Crop' }, { key: 'status', header: 'Status' },
      { key: 'startDate', header: 'Start date' }, { key: 'endDate', header: 'End date' },
    ]} footer={<div className="management-row-actions">
      <button disabled={page === 1 || loading} onClick={() => setPage(page - 1)}>Previous seasons</button>
      <span>Page {page}</span>
      <button disabled={loading || page >= (data?.pagination?.totalPages ?? 0)} onClick={() => setPage(page + 1)}>Next seasons</button>
    </div>} />
  </section>
}

export default function CropSupportDetail({ report, onSaved, onReload, onBusyChange, busy = false }) {
  const { user, can } = useAuth()
  const [content, setContent] = useState('')
  const [images, setImages] = useState([])
  const [survey, setSurvey] = useState(false)
  const [pending, setPending] = useState(false)
  const [reading, setReading] = useState(false)
  const [error, setError] = useState('')
  const [page, setPage] = useState(1)
  const context = useApiQuery(`crop-readings:${report.id}:${page}`, signal => api.request(`/api/crop-condition-reports/${report.id}/sensor-readings`, { query: { page }, signal }))
  const engineer = can(P.CROP_SUPPORT_PROCESS) && report.engineerId === user.id
  const reply = can(P.CROP_SUPPORT_CREATE) && report.reporterId === user.id && report.status === 'AWAITING_INFORMATION'
  const process = engineer && report.status === 'IN_REVIEW'
  const claim = can(P.CROP_SUPPORT_PROCESS) && !report.engineerId && report.status === 'NEW'
  async function submit(action) {
    if (pending || reading || busy) return
    setError('')
    if (action !== 'claim' && !content.trim()) { setError('Enter a consultation note.'); return }
    setPending(true); onBusyChange?.(true)
    try {
      const options = { method: 'POST' }
      if (action !== 'claim') options.body = { expectedRevision: report.revision, content: content.trim(), recommendSiteSurvey: action === 'recommend' && survey, images }
      const updated = await api.request(`/api/crop-condition-reports/${report.id}/${action}`, options)
      setContent(''); setImages([]); setSurvey(false); onSaved(updated)
    } catch (cause) { setError(cause.message) }
    finally { setPending(false); onBusyChange?.(false) }
  }
  return <SectionCard title={`Crop report #${report.id}`} actions={<StatusBadge status={report.status} />}>
    <dl><dt>Farm / zone</dt><dd>#{report.farmId} / {report.zoneId ? `#${report.zoneId}` : 'Whole farm'}</dd>
      <dt>Crop</dt><dd>{report.crop}</dd><dt>Reported at</dt><dd>{displayTime(report.reportedAt)}</dd>
      <dt>Reporter</dt><dd>User #{report.reporterId}</dd><dt>Engineer</dt><dd>{report.engineerId ? `User #${report.engineerId}` : 'Awaiting assignment'}</dd></dl>
    <p style={{ whiteSpace: 'pre-wrap' }}>{report.description}</p><EvidenceImages images={report.images} />
    <h3>Consultation history</h3>
    {(report.updates ?? []).map(update => <article key={update.id}>
      <p><strong>{update.action}</strong> · User #{update.userId} · {displayTime(update.createdAt)}</p>
      <p style={{ whiteSpace: 'pre-wrap' }}>{update.content}</p>
      {update.recommendSiteSurvey && <p>On-site survey recommended.</p>}<EvidenceImages images={update.images} />
    </article>)}
    {error && <p role="alert">{error}</p>}
    <button disabled={pending || reading || busy} onClick={onReload}>Reload report</button>
    {claim && <button disabled={pending || reading || busy} onClick={() => submit('claim')}>Accept crop report</button>}
    {(process || reply) && <fieldset disabled={pending || reading || busy} className="management-form"><legend>Consultation update</legend>
      <label>Consultation note<textarea required maxLength={4000} value={content} onChange={event => setContent(event.target.value)} /></label>
      <EvidenceImages images={images} onChange={setImages} onBusyChange={value => { setReading(value); onBusyChange?.(value) }} />
      {process && <><label><input type="checkbox" checked={survey} onChange={event => setSurvey(event.target.checked)} />Recommend an on-site survey</label>
        <button onClick={() => submit('request-information')}>Request additional information</button>
        <button onClick={() => submit('recommend')}>Record recommendation</button>
        <button onClick={() => submit('resolve')}>Resolve report</button></>}
      {reply && <button onClick={() => submit('reply')}>Send additional information</button>}
    </fieldset>}
    {pending && <p role="status">Saving consultation…</p>}
    {engineer && <SupportingSeasons key={report.id} reportId={report.id} />}
    <h3>Supporting sensor readings</h3><p>Live context from attached channels still in this farm and zone.</p>
    <ApiState loading={context.loading} error={context.error} reload={context.reload} />
    <DataTable caption="Crop report sensor readings" rows={context.data?.items ?? []} loading={context.loading} columns={[
      { key: 'channelCode', header: 'Channel' }, { key: 'value', header: 'Value' }, { key: 'unit', header: 'Unit' },
      { key: 'measuredAt', header: 'Measured', render: displayTime }, { key: 'isValid', header: 'Valid', render: value => value ? 'Yes' : 'No' },
      { key: 'qualityStatus', header: 'Quality' },
    ]} footer={<div className="management-row-actions"><button disabled={page === 1 || context.loading} onClick={() => setPage(page - 1)}>Previous readings</button>
      <span>Page {page}</span><button disabled={context.loading || page * 20 >= (context.data?.pagination.totalItems ?? 0)} onClick={() => setPage(page + 1)}>Next readings</button></div>} />
  </SectionCard>
}
