import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS as P } from '../../config/permissions'
import ApiState from '../../components/app/ApiState'
import InstallationDossiers from '../../components/app/InstallationDossiers'
import EvidenceImages from '../../components/app/EvidenceImages'
import JobAssignmentForm from '../../components/app/JobAssignmentForm'
import { DataTable, PageHeader, SectionCard, StatusBadge, TablePagination } from '../../components/app/ui'
import { displayTime } from '../../utils/display'

export default function InstallationRequestsPage() {
  const { can } = useAuth()
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState(null)
  const [note, setNote] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const query = useApiQuery(`installation-requests:${page}`, signal => api.request('/api/installation-requests', { query: { page }, signal }))
  async function open(id) {
    setPending(true); setError(''); setNotice(''); setNote(''); setSelected(null)
    try { setSelected(await api.request(`/api/installation-requests/${id}`)) }
    catch (err) { setError(err.message) } finally { setPending(false) }
  }
  async function review(event) {
    event.preventDefault(); if (pending) return
    const action = event.nativeEvent.submitter?.value
    if (!action) return
    setPending(true); setError(''); setNotice('')
    try {
      setSelected(await api.request(`/api/installation-requests/${selected.id}/${action}`, { method: 'POST', body: { note } }))
      setNote(''); query.reload(); setNotice(action === 'approve' ? 'Request approved for scheduling.' : 'Request returned for correction.')
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  return <><PageHeader title="Installation requests" description="Review confirmed proposals before scheduling installation work." />
    {can(P.INSTALLATIONS_REVIEW) && <InstallationDossiers onApproved={async id => { query.reload(); await open(id) }} />}
    <ApiState loading={query.loading} error={query.error} />{error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {selected && <SectionCard title={`Installation request #${selected.id}`} actions={<StatusBadge status={selected.status} />}>
      <dl><dt>Farm</dt><dd>#{selected.farmId}</dd><dt>Customer</dt><dd>#{selected.ownerId}</dd><dt>Consultation / proposal</dt><dd>#{selected.consultationId} / #{selected.proposalId}</dd>
        <dt>Rental duration</dt><dd>{selected.rentalMonths} months</dd><dt>Requested installation</dt><dd>{displayTime(selected.requestedInstallationAt)}</dd>
        <dt>Customer plan confirmation</dt><dd>{selected.planConfirmationNote}</dd><dt>Deposit confirmation</dt><dd>{selected.depositConfirmationNote}</dd></dl>
      <ul>{selected.lines.map(line => <li key={line.id}>{line.quantity} × {line.deviceType} · {line.installationLocation}</li>)}</ul>
      <EvidenceImages images={selected.depositBills} />
      {selected.reviewedAt && <p>Reviewed {displayTime(selected.reviewedAt)} by user #{selected.reviewedByUserId}: {selected.reviewNote}</p>}
      {selected.status === 'RETURNED' && <p>Assigned engineer: prepare a revised proposal in consultation #{selected.consultationId}, confirm it with the customer, and submit again.</p>}
      {can(P.INSTALLATIONS_REVIEW) && selected.status === 'APPROVED' && <JobAssignmentForm key={selected.id} requestId={selected.id}
        onSaved={row => { setSelected(null); setNotice(`Installation job #${row.id} created. Open Installation jobs to manage the appointment.`) }} />}
      {can(P.INSTALLATIONS_REVIEW) && selected.status === 'SUBMITTED' && <form className="management-form" onSubmit={review}><fieldset disabled={pending}>
        <label>Review note or correction reason<textarea required maxLength={2000} value={note} onChange={event => setNote(event.target.value)} /></label>
        <button type="submit" value="approve">Approve for scheduling</button><button type="submit" value="return">Return for correction</button>
      </fieldset></form>}
    </SectionCard>}
    <DataTable caption="Installation requests" loading={query.loading} rows={query.data?.items ?? []} columns={[
      { key: 'id', header: 'Request' }, { key: 'farmId', header: 'Farm' }, { key: 'status', header: 'Status', render: value => <StatusBadge status={value} /> },
      { key: 'submittedAt', header: 'Submitted', render: displayTime },
      { key: 'action', header: 'Action', render: (_, row) => <button disabled={pending} onClick={() => open(row.id)}>Open request #{row.id}</button> },
    ]} footer={query.data?.pagination.totalItems > 0 && <TablePagination page={page} totalPages={query.data.pagination.totalPages} pageSize={query.data.pagination.pageSize}
      totalItems={query.data.pagination.totalItems} onPageChange={setPage} disabled={query.loading} label="Installation requests table pagination" />} />
  </>
}
