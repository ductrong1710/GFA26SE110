import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'
import { PaperGroup } from './PaperEvidence'
import { parseVietnamDateTime } from '../../utils/display'
import EvidenceImages from './EvidenceImages'

export default function InstallationReportForm({ job, onSaved }) {
  const [draft, setDraft] = useState(null)
  const [pending, setPending] = useState(false)
  const [readingImages, setReadingImages] = useState(false)
  const [error, setError] = useState('')
  const [receipt, setReceipt] = useState(null)
  const query = useApiQuery(`report-devices:${job.id}:${job.revision}`, signal => api.request(`/api/installation-jobs/${job.id}/devices`, { signal }))
  async function submit(event) {
    event.preventDefault(); if (pending || readingImages) return
    setPending(true); setError('')
    try {
      const row = await api.request(`/api/installation-jobs/${job.id}/reports`, { method: 'POST', toastMessage: 'Installation report submitted successfully.', body: { expectedRevision: job.revision, notes: draft.notes, issues: draft.issues || null,
        images: draft.images, completedAt: draft.completedAt ? parseVietnamDateTime(draft.completedAt).toISOString() : undefined, paperwork: draft.paperwork,
        devices: draft.devices.map(d => ({ proposalLineId: d.proposalLineId, sensorNodeId: d.sensorNodeId, uavId: d.uavId, gatewayId: d.gatewayId,
          installationLocation: d.installationLocation, accessoriesChecked: d.accessoriesChecked === 'true', connectivityPassed: d.connectivityPassed === 'true',
          operationPassed: d.operationPassed === 'true', testResults: d.testResults })) } })
      setReceipt(row.id); setDraft(null); onSaved(row)
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  const deviceField = (index, name) => ({ value: draft.devices[index][name], onChange: event => setDraft({ ...draft,
    devices: draft.devices.map((d, i) => i === index ? { ...d, [name]: event.target.value } : d) }) })
  return <section aria-label="Technician installation report"><ApiState loading={query.loading} error={query.error} />
    {error && <p role="alert">{error}</p>}{receipt && <p role="status">Report #{receipt} submitted. Awaiting handover confirmation.</p>}
    {!draft && !receipt && query.data?.length > 0 && <button onClick={() => setDraft({ notes: '', issues: '', images: [], completedAt: '', paperwork: { contract: [], handover: [], bills: [], paperContractNumber: '' }, devices: query.data.map(d => ({ ...d,
      installationLocation: '', accessoriesChecked: '', connectivityPassed: '', operationPassed: '', testResults: '' })) })}>Prepare installation report</button>}
    {!query.loading && !query.error && query.data?.length === 0 && <p>An administrator must allocate devices before you can prepare the report.</p>}
    {draft && <form className="management-form" onSubmit={submit}><fieldset disabled={pending || readingImages} className="management-form"><legend>Installation report</legend>
      {draft.devices.map((device, index) => <fieldset key={device.id} className="management-form"><legend>{device.code} · {device.name}</legend>
        <label>Installation location for {device.code}<textarea required maxLength={1000} {...deviceField(index, 'installationLocation')} /></label>
        {[['accessoriesChecked', 'Accessories'], ['connectivityPassed', 'Connectivity'], ['operationPassed', 'Operation']].map(([key, label]) =>
          <label key={key}>{label} for {device.code}<select required {...deviceField(index, key)}><option value="">Record result</option><option value="true">Passed</option><option value="false">Failed</option></select></label>)}
        <label>Test evidence for {device.code}<textarea required maxLength={2000} {...deviceField(index, 'testResults')} /></label>
      </fieldset>)}
      <label>Handover and installation notes<textarea required maxLength={4000} value={draft.notes} onChange={event => setDraft({ ...draft, notes: event.target.value })} /></label>
      <label>Issues<textarea required={draft.devices.some(d => [d.accessoriesChecked, d.connectivityPassed, d.operationPassed].includes('false'))} maxLength={4000}
        value={draft.issues} onChange={event => setDraft({ ...draft, issues: event.target.value })} /></label>
      <EvidenceImages images={draft.images} onChange={images => setDraft(value => ({ ...value, images }))} onBusyChange={setReadingImages} />
      <label>Handover completed at (Vietnam time)<input type="datetime-local" value={draft.completedAt} onChange={e => setDraft({ ...draft, completedAt: e.target.value })} /></label>
      <label>Paper contract number<input maxLength={100} value={draft.paperwork.paperContractNumber} onChange={e => setDraft({ ...draft, paperwork: { ...draft.paperwork, paperContractNumber: e.target.value } })} /></label>
      {Object.entries({ contract: 'Signed contract', handover: 'Signed installation handover', bills: 'Payment evidence (optional)' }).map(([key, label]) =>
        <PaperGroup key={key} label={label} images={draft.paperwork[key]} onBusyChange={setReadingImages} onChange={images => setDraft(old => ({ ...old, paperwork: { ...old.paperwork, [key]: images } }))} />)}
      <button type="submit" disabled={!draft.paperwork.contract.length || !draft.paperwork.handover.length}>Submit installation report</button><button type="button" onClick={() => setDraft(null)}>Discard report draft</button>
    </fieldset></form>}
  </section>
}
