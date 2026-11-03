import { useState } from 'react'
import { api } from '../../services/api'
import { PaperGroup } from './PaperEvidence'
import EvidenceImages from './EvidenceImages'

export default function ServiceReportForm({ job, onSaved }) {
  const [draft, setDraft] = useState({ cause: '', work: '', components: '', testResults: '', notes: '', connectivityPassed: '', operationPassed: '', images: [], signedHandover: [] })
  const [pending, setPending] = useState(false)
  const [readingImages, setReadingImages] = useState(false)
  const [error, setError] = useState('')
  const [receipt, setReceipt] = useState(null)
  const field = name => ({ value: draft[name], onChange: event => setDraft(value => ({ ...value, [name]: event.target.value })) })
  async function submit(event) {
    event.preventDefault(); if (pending || readingImages || receipt) return
    setPending(true); setError('')
    try {
      const report = await api.request(`/api/service-jobs/${job.id}/reports`, { method: 'POST', body: { ...draft, expectedRevision: job.revision,
        connectivityPassed: draft.connectivityPassed === 'true', operationPassed: draft.operationPassed === 'true' } })
      setReceipt(report.id); onSaved(report)
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  return <section aria-label="Technician service report">
    {error && <p role="alert">{error}</p>}
    {receipt ? <p role="status">Report #{receipt} submitted. Awaiting operation confirmation.</p>
      : <form className="management-form" onSubmit={submit}><fieldset disabled={pending || readingImages} className="management-form"><legend>Service report</legend>
        {[['cause', 'Cause'], ['work', 'Work performed'], ['components', 'Components used'], ['testResults', 'Test results'], ['notes', 'Notes']].map(([name, label]) =>
          <label key={name}>{label}<textarea required maxLength={name === 'testResults' ? 2000 : 4000} {...field(name)} /></label>)}
        <p>Record “None” when no components were used.</p>
        {[['connectivityPassed', 'Connectivity'], ['operationPassed', 'Operation']].map(([name, label]) =>
          <label key={name}>{label}<select required {...field(name)}><option value="">Record result</option><option value="true">Passed</option><option value="false">Failed</option></select></label>)}
        <EvidenceImages images={draft.images} onChange={images => setDraft(value => ({ ...value, images }))} onBusyChange={setReadingImages} />
        <PaperGroup label="Signed service handover" images={draft.signedHandover} onBusyChange={setReadingImages} onChange={signedHandover => setDraft(value => ({ ...value, signedHandover }))} />
        <button type="submit" disabled={!draft.signedHandover.length}>Submit service report</button>
      </fieldset></form>}
  </section>
}
