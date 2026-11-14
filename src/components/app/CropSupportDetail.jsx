import { useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS as P } from '../../config/permissions'
import { api } from '../../services/api'
import { listAll } from '../../services/api/resources'
import { useApiQuery } from '../../services/api/useApiQuery'
import { displayTime } from '../../utils/display'
import ApiState from './ApiState'
import EvidenceImages from './EvidenceImages'
import CropSupportEvidence, { CropHistory, FollowUpActivityPicker } from './CropSupportEvidence'
import { DataTable, SectionCard, StatusBadge, TablePagination } from './ui'

function SupportingSeasons({ reportId }) {
  const [page, setPage] = useState(1)
  const rows = useApiQuery(`crop-support-seasons:${reportId}:${page}`, signal => api.request(`/api/crop-condition-reports/${reportId}/crop-seasons`, { query: { page }, signal }))
  return <section><h3>Supporting crop seasons</h3><p>Related seasons in the reporter's farm/zone. Care journal context uses the explicitly linked season.</p>
    <ApiState loading={rows.loading} error={rows.error} reload={rows.reload} />
    <DataTable caption="Crop seasons for this report" rows={rows.data?.items ?? []} loading={rows.loading} columns={[
      { key: 'name', header: 'Season' }, { key: 'crop', header: 'Crop' }, { key: 'status', header: 'Status' },
    ]} footer={(rows.data?.pagination?.totalItems ?? 0) > 0 && <TablePagination page={page} totalPages={rows.data?.pagination?.totalPages} pageSize={rows.data?.pagination?.pageSize} totalItems={rows.data?.pagination?.totalItems} onPageChange={setPage} disabled={rows.loading} label="Supporting crop seasons pagination" />} />
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
  const [draftRevision, setDraftRevision] = useState(null)
  const [engineerId, setEngineerId] = useState('')
  const [followType, setFollowType] = useState('ACKNOWLEDGED')
  const [activityId, setActivityId] = useState('')
  const [referralType, setReferralType] = useState('DEVICE_INCIDENT')
  const [incidentId, setIncidentId] = useState('')
  const engineer = can(P.CROP_SUPPORT_PROCESS) && report.engineerId === user.id
  const owner = can(P.CROP_SUPPORT_CREATE) && report.reporterId === user.id
  const allowed = action => report.allowedActions ? report.allowedActions.includes(action) : ({
    claim: can(P.CROP_SUPPORT_PROCESS) && !report.engineerId && report.status === 'NEW',
    assign: can(P.CROP_SUPPORT_ASSIGN) && !report.engineerId && report.status === 'NEW',
    reply: owner && report.status === 'AWAITING_INFORMATION',
    recommend: engineer && report.status === 'IN_REVIEW',
    'request-information': engineer && report.status === 'IN_REVIEW',
    resolve: engineer && report.status === 'IN_REVIEW',
    reopen: engineer && report.status === 'RESOLVED',
  }[action] ?? false)
  const evidence = report.canReadEvidence ?? (engineer || owner || can(P.CROP_SUPPORT_ASSIGN))
  const users = useApiQuery(`crop-engineers:${report.id}`, signal => listAll('/api/users', {}, signal), allowed('assign') && can(P.CROP_SUPPORT_ASSIGN))
  const incidents = useApiQuery(`crop-incidents:${report.id}:${report.revision}`, signal => listAll('/api/device-incidents', {}, signal), allowed('link-incident') && owner)
  const stale = draftRevision !== null && draftRevision !== report.revision
  const noteActions = ['reply', 'recommend', 'request-information', 'resolve', 'reopen', 'follow-ups', 'refer', 'link-incident'].some(allowed)
  function markDraft() { setDraftRevision(previous => previous ?? report.revision) }
  async function submit(action) {
    if (pending || reading || busy || stale) return
    setError('')
    if (!['claim', 'assign'].includes(action) && !content.trim()) { setError('Enter a consultation note.'); return }
    if (action === 'assign' && !engineerId) { setError('Choose an active engineer.'); return }
    if (action === 'link-incident' && !incidentId) { setError('Choose an incident created through device support.'); return }
    setPending(true); onBusyChange?.(true)
    try {
      const expectedRevision = draftRevision ?? report.revision
      const options = { method: 'POST' }
      if (action === 'assign') options.body = { expectedRevision, engineerId: Number(engineerId) }
      else if (action === 'follow-ups') options.body = { expectedRevision, type: report.status === 'RESOLVED' ? 'NOT_IMPROVED' : followType, content: content.trim(), images, cultivationActivityId: activityId ? Number(activityId) : null }
      else if (action === 'refer' || action === 'link-incident') options.body = { expectedRevision, type: action === 'link-incident' ? 'DEVICE_INCIDENT' : referralType, content: content.trim(), deviceIncidentId: action === 'link-incident' ? Number(incidentId) : null, images }
      else if (action !== 'claim') options.body = { expectedRevision, content: content.trim(), recommendSiteSurvey: action === 'recommend' && survey, images }
      const endpoint = action === 'refer' || action === 'link-incident' ? 'referrals' : action
      const updated = await api.request(`/api/crop-condition-reports/${report.id}/${endpoint}`, options)
      setContent(''); setImages([]); setSurvey(false); setActivityId(''); setDraftRevision(null); onSaved(updated)
    } catch (cause) { setError(cause.message) }
    finally { setPending(false); onBusyChange?.(false) }
  }
  return <SectionCard title={`Crop report #${report.id}`} actions={<StatusBadge status={report.status} />}>
    <dl><dt>Farm / zone</dt><dd>#{report.farmId} / {report.zoneId ? `#${report.zoneId}` : 'Whole farm'}</dd>
      <dt>Crop at reporting</dt><dd>{report.crop}</dd><dt>Observed at</dt><dd>{report.observedAt ? displayTime(report.observedAt) : 'Not recorded'}</dd>
      <dt>Reported at</dt><dd>{displayTime(report.reportedAt)}</dd><dt>Linked season</dt><dd>{report.linkedSeason ? `${report.linkedSeason.name} (#${report.linkedSeason.id}, ${report.linkedSeason.status})` : report.cropSeasonId ? `#${report.cropSeasonId} — details unavailable in current scope` : 'No season linked'}</dd>
      <dt>Reporter</dt><dd>User #{report.reporterId}</dd><dt>Engineer</dt><dd>{report.engineerId ? `User #${report.engineerId}` : 'Awaiting assignment'}</dd></dl>
    <p style={{ whiteSpace: 'pre-wrap' }}>{report.description}</p>
    {evidence ? <><EvidenceImages images={report.images} /><CropHistory report={report} /></> : <p>Inbox classification only. Accept assignment to read evidence.</p>}
    {report.status === 'RESOLVED' && <p>This support case is closed. A non-improvement response requests review; the assigned engineer decides whether to reopen. Crop season status is independent.</p>}
    {error && <p role="alert">{error}</p>}
    <button disabled={pending || reading || busy} onClick={onReload}>Reload report</button>
    {stale && <p role="alert">The report changed from revision {draftRevision} to {report.revision}. Your draft is preserved. Compare the history before sending again.
      <button onClick={() => setDraftRevision(report.revision)}>Use latest revision for this draft</button></p>}
    {allowed('claim') && <button disabled={pending || reading || busy} onClick={() => submit('claim')}>Accept crop report</button>}
    {allowed('assign') && can(P.CROP_SUPPORT_ASSIGN) && <fieldset disabled={pending || busy || stale}><legend>Assign crop report</legend>
      <ApiState loading={users.loading} error={users.error} reload={users.reload} />
      <label>Responsible engineer<select value={engineerId} onChange={e => { setEngineerId(e.target.value); markDraft() }}><option value="">Choose an active engineer</option>
        {(users.data ?? []).filter(row => row.isActive && row.roles?.includes('FarmEngineer')).map(row => <option key={row.id} value={row.id}>{row.fullName} (#{row.id})</option>)}</select></label>
      <button onClick={() => submit('assign')}>Assign engineer</button>
    </fieldset>}
    {noteActions && <fieldset disabled={pending || reading || busy} className="management-form"><legend>Consultation update</legend>
      <label>Consultation note<textarea required maxLength={4000} value={content} onChange={e => { setContent(e.target.value); markDraft() }} /></label>
      <EvidenceImages images={images} onChange={value => { setImages(value); markDraft() }} onBusyChange={value => { setReading(value); onBusyChange?.(value) }} />
      {allowed('recommend') && <label><input type="checkbox" checked={survey} onChange={e => { setSurvey(e.target.checked); markDraft() }} />Recommend an on-site survey</label>}
      {allowed('request-information') && <button disabled={stale} onClick={() => submit('request-information')}>Request additional information</button>}
      {allowed('recommend') && <button disabled={stale} onClick={() => submit('recommend')}>Record recommendation</button>}
      {allowed('resolve') && <button disabled={stale} onClick={() => submit('resolve')}>Resolve report</button>}
      {allowed('reply') && <button disabled={stale} onClick={() => submit('reply')}>Send additional information</button>}
      {allowed('reopen') && <button disabled={stale} onClick={() => submit('reopen')}>Reopen report with reason</button>}
      {allowed('follow-ups') && <>
        {report.status !== 'RESOLVED' && <label>Feedback type<select value={followType} onChange={e => { setFollowType(e.target.value); markDraft() }}>
          {Object.entries({ ACKNOWLEDGED: 'Read advice', AGREED: 'Agree with advice', QUESTION: 'Ask a question', ACTION_TAKEN: 'Care action taken', IMPROVED: 'Improved', NOT_IMPROVED: 'Not improved' }).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select></label>}
        {report.cropSeasonId && <FollowUpActivityPicker seasonId={report.cropSeasonId} value={activityId} onChange={value => { setActivityId(value); markDraft() }} />}
        <p>Linking an existing care activity is optional, including after the season is completed.</p>
        <button disabled={stale} onClick={() => submit('follow-ups')}>{report.status === 'RESOLVED' ? 'Report no improvement / request review' : 'Send follow-up'}</button>
      </>}
      {allowed('refer') && <><label>Referral destination<select value={referralType} onChange={e => { setReferralType(e.target.value); markDraft() }}>
        <option value="DEVICE_INCIDENT">MF-03 device incident</option><option value="UAV_VIDEO">MF-02 video (pending integration)</option></select></label>
        <p>Video/service integration is pending; manual consultation remains available.</p>
        <button disabled={stale} onClick={() => submit('refer')}>Record referral request</button></>}
      {allowed('link-incident') && owner && <><a href="/app/device-incidents" target="_blank" rel="noreferrer">Open device incident flow (MF-03)</a>
        <ApiState loading={incidents.loading} error={incidents.error} reload={incidents.reload} />
        <button type="button" onClick={incidents.reload}>Refresh incident choices</button>
        <label>Existing device incident<select value={incidentId} onChange={e => { setIncidentId(e.target.value); markDraft() }}><option value="">Choose an incident</option>
          {(incidents.data ?? []).filter(row => row.farmId === report.farmId).map(row => <option key={row.id} value={row.id}>#{row.id} {row.deviceCode || 'Device not yet identified'} — {row.description}</option>)}</select></label>
        <button disabled={stale} onClick={() => submit('link-incident')}>Link device incident</button></>}
    </fieldset>}
    {pending && <p role="status">Saving consultation…</p>}
    {evidence && <CropSupportEvidence key={`evidence:${report.id}`} report={report} />}
    {engineer && <SupportingSeasons key={`seasons:${report.id}`} reportId={report.id} />}
  </SectionCard>
}
