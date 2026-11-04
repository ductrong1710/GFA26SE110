import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { listAll } from '../../services/api/resources'
import EvidenceImages from './EvidenceImages'
import ApiState from './ApiState'
import { DataTable, SectionCard, StatusBadge } from './ui'
import { displayTime, parseVietnamDateTime } from '../../utils/display'

const emptyLine = () => ({ surveyZoneId: '', publicContentId: '', deviceType: '', quantity: 1, installationLocation: '' })
const price = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 2 })

export default function ProposalPanel({ consultationId, editable = false, canConfirmDeposit = editable, onSubmitted, linked = true }) {
  const [page, setPage] = useState(1)
  const [draft, setDraft] = useState(null)
  const [selected, setSelected] = useState(null)
  const [pending, setPending] = useState(false)
  const [note, setNote] = useState('')
  const [bills, setBills] = useState([])
  const [reading, setReading] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const path = `/api/consultations/${consultationId}/proposals`
  const query = useApiQuery(`proposals:${consultationId}:${page}`, signal => api.request(path, { query: { page }, signal }))
  const surveys = useApiQuery(`proposal-surveys:${consultationId}:${!!draft}`, signal => api.request(`/api/consultations/${consultationId}/surveys`, { signal }), !!draft)
  const catalog = useApiQuery(`proposal-products:${!!draft}`, signal => listAll('/api/public-content', {}, signal), !!draft)
  const products = catalog.data?.filter(entry => entry.kind === 'DEVICE' && entry.estimatedMonthlyRentVnd != null) ?? []
  const survey = useApiQuery(`proposal-survey:${consultationId}:${draft?.surveyId}`, signal => api.request(`/api/consultations/${consultationId}/surveys/${draft.surveyId}`, { signal }), !!draft?.surveyId)
  async function open(id) {
    setPending(true); setError(''); setNotice(''); setNote('')
    try { setSelected(await api.request(`${path}/${id}`)); setDraft(null) }
    catch (err) { setError(err.message) } finally { setPending(false) }
  }
  async function save(event) {
    event.preventDefault(); if (pending) return
    setPending(true); setError(''); setNotice('')
    try {
      const saved = await api.request(path, { method: 'POST', body: { ...draft, surveyId: Number(draft.surveyId), rentalMonths: Number(draft.rentalMonths),
        requestedInstallationAt: parseVietnamDateTime(draft.requestedInstallationAt).toISOString(), notes: draft.notes || null,
        lines: draft.lines.map(line => ({ ...line, publicContentId: Number(line.publicContentId), surveyZoneId: Number(line.surveyZoneId), quantity: Number(line.quantity) })) } })
      setSelected(saved); setDraft(null); setPage(1); query.reload(); setNotice(`Proposal revision ${saved.revision} saved.`)
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  async function transition(action) {
    if (pending) return
    setPending(true); setError(''); setNotice('')
    try {
      if (action === 'submit-installation') {
        const dossier = await api.request('/api/installation-dossiers/' + selected.id + '/submit', {
          method: 'POST', body: { expectedRevision: selected.revision, note, bills },
        })
        setSelected({ ...selected, status: dossier.status, depositBills: bills }); setBills([])
        setNotice(`Dossier #${dossier.proposalId} submitted for administrator review.`)
        onSubmitted?.()
      } else {
        setSelected(await api.request(`${path}/${selected.id}/${action}`, { method: 'POST', body: { note } }))
        setNotice(action === 'confirm-plan' ? 'Customer plan confirmation recorded.' : 'External deposit confirmation recorded.')
      }
      setNote(''); query.reload()
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  const field = name => ({ value: draft[name], onChange: event => setDraft({ ...draft, [name]: event.target.value }) })
  const lineField = (index, name) => ({ value: draft.lines[index][name], onChange: event => setDraft({ ...draft,
    lines: draft.lines.map((line, i) => i === index ? { ...line, [name]: event.target.value } : line) }) })
  const confirmation = selected?.status === 'DRAFT' && editable ? 'confirm-plan' : selected?.status === 'PLAN_CONFIRMED' && canConfirmDeposit ? 'confirm-deposit' : null
  return <SectionCard title="Device proposals" description="Each revision records the survey, proposed devices, rental duration and requested installation time.">
    <ApiState {...query} />{error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {editable && !draft && page === 1 && !query.loading && !query.error && !['SUBMITTED', 'WAITING_ADMIN'].includes(query.data?.items[0]?.status) && <button className="app-ui-button" disabled={pending} onClick={() => {
      setDraft({ expectedRevision: query.data?.items[0]?.revision ?? 0, surveyId: '', rentalMonths: '', requestedInstallationAt: '', notes: '', lines: [emptyLine()] })
      setSelected(null); setError(''); setNotice('')
    }}>Prepare proposal</button>}
    {draft && <form aria-label="Device proposal" className="management-form" onSubmit={save}><fieldset disabled={pending} className="management-form"><legend>Proposal revision {draft.expectedRevision + 1}</legend>
      <ApiState {...surveys} /><ApiState {...survey} />
      <ApiState {...catalog} />
      {catalog.data && !products.length && <p>No published device has a monthly rental price. An administrator can publish one in Homepage content.</p>}
      <label>Survey revision<select required value={draft.surveyId} onChange={event => setDraft({ ...draft, surveyId: event.target.value, lines: [emptyLine()] })}>
        <option value="">Select latest survey</option>{surveys.data?.items.slice(0, 1).map(row => <option key={row.id} value={row.id}>Revision {row.revision}: {row.farmName}</option>)}</select></label>
      <label>Rental duration (months)<input required type="number" min="1" max="120" step="1" {...field('rentalMonths')} /></label>
      <label>Requested installation time<input required type="datetime-local" {...field('requestedInstallationAt')} /></label>
      <label>Proposal notes<textarea maxLength={4000} {...field('notes')} /></label>
      {draft.lines.map((line, index) => <fieldset key={index} className="management-form"><legend>Device {index + 1}</legend>
        <label>Survey zone<select required {...lineField(index, 'surveyZoneId')}><option value="">Select zone</option>{survey.data?.zones.map(zone => <option key={zone.id} value={zone.id}>{zone.name}</option>)}</select></label>
        <label>Device type<input required maxLength={150} {...lineField(index, 'deviceType')} /></label>
        <label>Published rental product<select required {...lineField(index, 'publicContentId')}><option value="">Select priced device</option>
          {products.map(entry => <option key={entry.id} value={entry.id}>{entry.title} · {price.format(entry.estimatedMonthlyRentVnd)}/month</option>)}
        </select></label>
        <label>Quantity<input required type="number" min="1" max="1000" step="1" {...lineField(index, 'quantity')} /></label>
        <label>Installation location<textarea required maxLength={1000} {...lineField(index, 'installationLocation')} /></label>
        {draft.lines.length > 1 && <button type="button" onClick={() => setDraft({ ...draft, lines: draft.lines.filter((_, i) => i !== index) })}>Remove device {index + 1}</button>}
      </fieldset>)}
      <button type="button" disabled={draft.lines.length >= 100} onClick={() => setDraft({ ...draft, lines: [...draft.lines, emptyLine()] })}>Add proposed device</button>
      <button type="submit" className="app-ui-button app-ui-button--primary" disabled={!survey.data || !!survey.error}>Save proposal revision</button>
      <button type="button" onClick={() => { setDraft(null); setError('') }}>Discard proposal draft</button>
    </fieldset></form>}
    {selected && <section aria-label="Proposal details"><h3>Proposal revision {selected.revision}</h3><StatusBadge status={selected.status} />
      <p>Survey #{selected.surveyId} · Rental: {selected.rentalMonths} months · Requested installation: {displayTime(selected.requestedInstallationAt)}</p><p>{selected.notes}</p>
      <ul>{selected.lines.map((line, index) => <li key={line.id ?? index}>{line.quantity} × {line.deviceType} · Survey zone #{line.surveyZoneId} · {line.installationLocation}
        {line.productName && <p>{line.productName} · {price.format(line.monthlyRentVnd)}/month · Price revision {line.priceRevision}</p>}</li>)}</ul>
      {selected.lines.length > 0 && selected.lines.every(line => line.monthlyRentVnd != null) &&
        <p>Initial agreed rent: {price.format(selected.lines.reduce((sum, line) => sum + line.monthlyRentVnd * line.quantity, 0) * selected.rentalMonths)}</p>}
      {selected.planConfirmedAt && <p>Plan confirmed {displayTime(selected.planConfirmedAt)} by user #{selected.planConfirmedByUserId}: {selected.planConfirmationNote}</p>}
      {selected.depositConfirmedAt && <p>Deposit confirmed {displayTime(selected.depositConfirmedAt)} by user #{selected.depositConfirmedByUserId}: {selected.depositConfirmationNote}</p>}
      {confirmation && <form className="management-form" onSubmit={event => { event.preventDefault(); transition(confirmation) }}><fieldset disabled={pending}>
        <label>Confirmation evidence or reference<textarea required maxLength={2000} value={note} onChange={event => setNote(event.target.value)} /></label>
        <button type="submit">{confirmation === 'confirm-plan' ? 'Record customer plan confirmation' : 'Record external deposit confirmation'}</button>
      </fieldset></form>}
      {selected.dossierReviewNote && <p>Administrator review: {selected.dossierReviewNote}</p>}
      <EvidenceImages images={selected.depositBills} />
      {editable && selected.status === 'DEPOSIT_CONFIRMED' && (linked ? <form onSubmit={event => { event.preventDefault(); transition('submit-installation') }}>
        <fieldset disabled={pending}><legend>Installation dossier</legend>
          <label>Dossier note<textarea required maxLength={2000} value={note} onChange={e => setNote(e.target.value)} /></label>
          <EvidenceImages images={bills} onChange={setBills} onBusyChange={setReading} />
          <button disabled={reading || !bills.length}>Submit installation request</button>
        </fieldset></form> : <p>An administrator must link the customer and farm before submission.</p>)}
    </section>}
    <DataTable caption="Proposal revisions" loading={query.loading} rows={query.data?.items ?? []} columns={[
      { key: 'revision', header: 'Revision' }, { key: 'status', header: 'Status', render: value => <StatusBadge status={value} /> },
      { key: 'createdAt', header: 'Recorded', render: displayTime },
      { key: 'action', header: 'Action', render: (_, row) => <button disabled={pending} onClick={() => open(row.id)}>View proposal revision {row.revision}</button> },
    ]} footer={<div className="management-row-actions"><button disabled={page === 1 || query.loading} onClick={() => setPage(page - 1)}>Previous proposals</button>
      <span>Page {page}</span><button disabled={query.loading || page * 20 >= (query.data?.pagination.totalItems ?? 0)} onClick={() => setPage(page + 1)}>More proposals</button></div>} />
  </SectionCard>
}
