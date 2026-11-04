import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'
import EvidenceImages from './EvidenceImages'
import { SectionCard } from './ui'
import { displayTime } from '../../utils/display'

export default function InstallationDossiers({ onApproved }) {
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState(null)
  const [note, setNote] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState('')
  const list = useApiQuery('pending-dossiers:' + page, signal => api.request('/api/installation-dossiers', { signal, query: { page } }))
  const detail = useApiQuery('review-dossier:' + selected?.id, signal => api.request(
    '/api/consultations/' + selected.consultationId + '/proposals/' + selected.id, { signal }), Boolean(selected))
  const proposal = detail.data
  async function decide(event) {
    event.preventDefault()
    const action = event.nativeEvent.submitter?.value
    if (!action || busy || !proposal) return
    setBusy(true); setError(''); setNotice('')
    try {
      const decision = await api.request('/api/installation-dossiers/' + selected.id + '/review', { method: 'POST',
        body: { approve: action === 'approve', expectedRevision: proposal.revision, note } })
      setSelected(null); setNote(''); list.reload()
      if (decision.installationRequestId) {
        setNotice('Installation request #' + decision.installationRequestId + ' approved for scheduling.')
        await onApproved(decision.installationRequestId)
      } else setNotice('Dossier returned for correction.')
    } catch (err) { setError(err.message) } finally { setBusy(false) }
  }
  return <SectionCard title="Installation dossiers awaiting review"><ApiState {...list} />
    {error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {list.data?.items.length === 0 && <p>No pending dossiers.</p>}
    {list.data?.items.map(row => <p key={row.id}>{row.customerName} · {row.farmName} · {displayTime(row.submittedAt)} <button disabled={busy} onClick={() => {
      setSelected(row); setNote(''); setError(''); setNotice('')
    }}>Review dossier #{row.id}</button></p>)}
    {page > 1 && <button disabled={busy} onClick={() => setPage(page - 1)}>Previous dossiers</button>}
    {page * 20 < (list.data?.pagination.totalItems ?? 0) && <button disabled={busy} onClick={() => setPage(page + 1)}>More dossiers</button>}
    <ApiState {...detail} />
    {proposal && <form onSubmit={decide}><fieldset disabled={busy}><legend>Dossier #{proposal.id}</legend>
      <p>{selected.customerName} · {selected.phone} · {selected.farmName} · {selected.farmLocation}</p>
      <p>Installation {displayTime(proposal.requestedInstallationAt)} · Rental {proposal.rentalMonths} months</p>
      <ul>{proposal.lines.map(line => <li key={line.id}>{line.quantity} × {line.productName || line.deviceType} · {line.installationLocation}</li>)}</ul>
      <p>Plan confirmation: {proposal.planConfirmationNote}</p><p>Deposit confirmation: {proposal.depositConfirmationNote}</p>
      <EvidenceImages images={proposal.depositBills} />
      <label>Dossier review note<textarea required maxLength={2000} value={note} onChange={e => setNote(e.target.value)} /></label>
      <button value="approve">Approve dossier</button><button value="return">Return dossier</button>
    </fieldset></form>}
  </SectionCard>
}
