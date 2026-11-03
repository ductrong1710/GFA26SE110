import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { DataTable, SectionCard } from './ui'
import ApiState from './ApiState'
import { PaperGroup, readPaper } from './PaperEvidence'
import EvidenceImages from './EvidenceImages'
import { displayTime } from '../../utils/display'

export default function ServiceReports({ job, canConfirm, canReview, onUpdated }) {
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState(null)
  const [note, setNote] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const path = `/api/service-jobs/${job.id}`
  const query = useApiQuery(`service-reports:${job.id}:${job.revision}:${page}`, signal => api.request(`${path}/reports`, { query: { page }, signal }))
  async function open(id) {
    setPending(true); setError(''); setNotice(''); setNote(''); setSelected(null)
    try { setSelected(await api.request(`${path}/reports/${id}`)) }
    catch (err) { setError(err.message) } finally { setPending(false) }
  }
  async function decide(event) {
    event.preventDefault(); if (pending) return
    const action = event.nativeEvent.submitter?.value
    if (!action) return
    setPending(true); setError(''); setNotice('')
    try {
      const updated = await api.request(`${path}/${action}`, { method: 'POST', body: { expectedRevision: job.revision, reportId: selected.id, note } })
      onUpdated(updated); setSelected(null); setNote(''); query.reload(); setNotice('Service decision recorded.')
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  const latest = page === 1 && selected?.id === query.data?.items[0]?.id
  const confirm = latest && canConfirm && job.status === 'REPORT_SUBMITTED'
  const review = latest && canReview && ['REPORT_SUBMITTED', 'OWNER_CONFIRMED'].includes(job.status)
  return <SectionCard title="Service reports" description="Review work, test evidence and previous report versions before confirming operation.">
    <ApiState loading={query.loading} error={query.error} reload={query.reload} />
    {pending && <p role="status">Loading service report…</p>}{error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {selected && <section aria-label="Service report details"><h3>Report revision {selected.revision}</h3>
      {[['cause', 'Cause'], ['work', 'Work performed'], ['components', 'Components used'], ['testResults', 'Test results'], ['notes', 'Notes']].map(([name, label]) =>
        <div key={name}><strong>{label}</strong><p style={{ whiteSpace: 'pre-wrap' }}>{selected[name]}</p></div>)}
      <p>Connectivity: {selected.connectivityPassed ? 'Passed' : 'Failed'} · Operation: {selected.operationPassed ? 'Passed' : 'Failed'}</p>
      <EvidenceImages images={selected.images} />
      {selected.paperworkJson && <PaperGroup label="Signed service handover" images={readPaper(selected.paperworkJson)} />}
      {selected.replacement && <section aria-label="Reported replacement"><h4>Replacement recorded with this report</h4>
        <p>{selected.replacement.oldDeviceCode} → {selected.replacement.newDeviceCode}</p>
        <p>{displayTime(selected.replacement.createdAt)} · Technician #{selected.replacement.technicianId}</p><p>{selected.replacement.note}</p>
      </section>}
      {selected.confirmedAt && <p>Operation confirmed {displayTime(selected.confirmedAt)}: {selected.confirmationNote}</p>}
      {selected.reviewedAt && <p>{selected.approved ? 'Approved' : 'Returned'} {displayTime(selected.reviewedAt)}: {selected.reviewNote}</p>}
      {(confirm || review) && <form className="management-form" onSubmit={decide}><fieldset disabled={pending}>
        <label>Decision note<textarea required maxLength={2000} value={note} onChange={event => setNote(event.target.value)} /></label>
        {confirm && <button type="submit" value="confirm-operation">Confirm operation</button>}
        {review && <><button type="submit" value="return">Request service rework</button>
          {job.status === 'OWNER_CONFIRMED' && <><button type="submit" value="approve" disabled={!selected.connectivityPassed || !selected.operationPassed}>Approve service result</button>
            {(!selected.connectivityPassed || !selected.operationPassed) && <p>Resolve failed checks before approval.</p>}</>}</>}
      </fieldset></form>}
    </section>}
    <DataTable caption="Service report revisions" loading={query.loading} rows={query.data?.items ?? []} columns={[
      { key: 'revision', header: 'Revision' }, { key: 'technicianId', header: 'Technician' }, { key: 'createdAt', header: 'Submitted', render: displayTime },
      { key: 'action', header: 'Action', render: (_, row) => <button disabled={pending} onClick={() => open(row.id)}>View report revision {row.revision}</button> },
    ]} footer={<div className="management-row-actions"><button disabled={page === 1 || query.loading} onClick={() => setPage(page - 1)}>Previous reports</button>
      <span>Page {page}</span><button disabled={query.loading || page * 20 >= (query.data?.pagination.totalItems ?? 0)} onClick={() => setPage(page + 1)}>More reports</button></div>} />
  </SectionCard>
}
