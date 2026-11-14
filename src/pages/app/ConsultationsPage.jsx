import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from '../../components/app/ApiState'
import SurveyPanel from '../../components/app/SurveyPanel'
import SiteVisitPanel from '../../components/app/SiteVisitPanel'
import ProposalPanel from '../../components/app/ProposalPanel'
import ConsultationBinding from '../../components/app/ConsultationBinding'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS as P } from '../../config/permissions'
import { DataTable, PageHeader, SectionCard, StatusBadge, TablePagination } from '../../components/app/ui'
import { displayTime } from '../../utils/display'
import '../../styles/management.css'

export default function ConsultationsPage() {
  const { can } = useAuth()
  const manages = can(P.CONSULTATIONS_MANAGE)
  const [inbox, setInbox] = useState(manages)
  const [page, setPage] = useState(1)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const query = useApiQuery(`consultations:${inbox}:${page}`, signal => api.request('/api/consultations', { query: { inbox, page }, signal }))
  async function claim(id) {
    setPending(true); setError('')
    try { await api.request(`/api/consultations/${id}/claim`, { method: 'POST' }); setInbox(false); setPage(1); query.reload() }
    catch (err) { setError(err.message); query.reload() } finally { setPending(false) }
  }
  return <>
    <PageHeader title="Consultations" description="Receive consultation requests and follow up with your assigned customers." />
    <div className="management-row-actions">{[true, false].map(value => <button key={String(value)} className="app-ui-button" aria-pressed={inbox === value}
      onClick={() => { setInbox(value); setPage(1); setError('') }}>{value ? 'New requests' : manages ? 'My consultations' : 'All consultations'}</button>)}</div>
    <ApiState {...query} />{error && <p role="alert">{error}</p>}
    <DataTable caption="Consultations" loading={query.loading} rows={query.data?.items ?? []} columns={[
      { key: 'id', header: 'Reference', render: value => `#${value}` }, { key: 'serviceNeed', header: 'Service need' },
      { key: 'status', header: 'Status', render: value => <StatusBadge status={value} /> },
      { key: 'createdAt', header: 'Received', render: displayTime },
      { key: 'action', header: 'Action', render: (_, row) => inbox && manages ? <button disabled={pending} className="app-ui-button" onClick={() => claim(row.id)}>Claim request #{row.id}</button>
        : <Link to={`/app/consultations/${row.id}`}>Open #{row.id}</Link> },
    ]} footer={query.data?.pagination.totalItems > 0 && <TablePagination page={page} totalPages={query.data.pagination.totalPages} pageSize={query.data.pagination.pageSize}
      totalItems={query.data.pagination.totalItems} onPageChange={setPage} disabled={query.loading} label="Consultations table pagination" />} />
  </>
}

export function ConsultationDetailPage() {
  const { id } = useParams()
  return <ConsultationDetail key={id} id={id} />
}

function ConsultationDetail({ id }) {
  const [submitted, setSubmitted] = useState(false)
  const { can, user } = useAuth()
  const manages = can(P.CONSULTATIONS_MANAGE)
  const query = useApiQuery(`consultation:${id}`, signal => api.request(`/api/consultations/${id}`, { signal }))
  const [values, setValues] = useState({ notes: '', contactResult: '', nextAction: '' })
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  async function followUp(event) {
    event.preventDefault(); setPending(true); setError(''); setNotice('')
    try {
      const updated = await api.request(`/api/consultations/${id}/follow-ups`, { method: 'POST', body: values })
      query.replaceData(updated); setValues({ notes: '', contactResult: '', nextAction: '' }); setNotice('Follow-up recorded.')
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  const row = query.data
  const visit = useApiQuery('site-visit:' + id, signal => api.request('/api/consultations/' + id + '/site-visit', { signal }), Boolean(row))
  const channel = visit.data?.channel ?? row?.channel
  const pendingSite = visit.data?.current?.channel === 'ON_SITE' && visit.data.current.reviewStatus === 'PENDING'
  const canSurvey = Boolean(visit.data && ((channel !== 'ON_SITE' && !pendingSite) || visit.data.current?.scheduledAt))
  const dossierDecision = row?.history.filter(item => ['INSTALLATION_SUBMITTED', 'INSTALLATION_APPROVED', 'INSTALLATION_RETURNED'].includes(item.action)).at(-1)
  const locked = submitted || (dossierDecision && dossierDecision.action !== 'INSTALLATION_RETURNED')
  const editable = manages && row?.engineerId === user?.id && row?.status === 'IN_PROGRESS'
  return <><Link to="/app/consultations">← Consultations</Link><PageHeader title={`Consultation #${id}`} /><ApiState {...query} />
    {row && <>
      <SectionCard title={row.customerName} actions={<StatusBadge status={row.status} />}>
        <dl><dt>Phone</dt><dd>{row.phone}</dd><dt>Email</dt><dd>{row.email || 'Not provided'}</dd><dt>Farm location</dt><dd>{row.farmLocation}</dd>
          <dt>Area (ha)</dt><dd>{row.farmArea ?? 'Not provided'}</dd><dt>Service need</dt><dd>{row.serviceNeed}</dd><dt>Content</dt><dd>{row.content}</dd></dl>
      </SectionCard>
      <SectionCard title="Follow-up history"><ol>{row.history.map(item => <li key={item.id}><strong>{item.action}</strong> · {displayTime(item.createdAt)}
        {item.userId && <span> · User #{item.userId}</span>}<p>{item.notes}</p><p>{item.contactResult}</p><p>{item.nextAction}</p></li>)}</ol></SectionCard>
      {can(P.USERS_MANAGE) && <ConsultationBinding consultation={row} onSaved={query.replaceData} />}
      <ApiState {...visit} />
      {visit.data && <SiteVisitPanel consultationId={id} visit={visit.data} onSaved={visit.replaceData} editable={editable && !locked} admin={can(P.USERS_MANAGE)} />}
      <SurveyPanel key={id} consultationId={id} editable={editable && !locked && canSurvey} />
      <ProposalPanel key={`proposal:${id}`} consultationId={id} linked={Boolean(row.ownerId && row.farmId)} customerName={row.customerName} customerEmail={row.email ?? ''} onSubmitted={() => setSubmitted(true)} editable={editable && !locked} canConfirmDeposit={editable || can(P.USERS_MANAGE)} />
      {editable && <SectionCard title="Record follow-up"><form className="management-form" onSubmit={followUp}>
        <fieldset disabled={pending} className="management-form">{[['notes', 'Notes', 4000], ['contactResult', 'Contact result', 1000], ['nextAction', 'Next action', 1000]].map(([name, label, max]) =>
          <label key={name}>{label}<textarea required maxLength={max} value={values[name]} onChange={event => setValues({ ...values, [name]: event.target.value })} /></label>)}
          <button className="app-ui-button app-ui-button--primary" type="submit">{pending ? 'Saving…' : 'Save follow-up'}</button></fieldset>
        {error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
      </form></SectionCard>}
    </>}
  </>
}
