import { useState } from 'react'
import { api } from '../../services/api'
import { listAll } from '../../services/api/resources'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'
import { PaperGroup, readPaper } from './PaperEvidence'
import EvidenceImages from './EvidenceImages'

const evidenceFields = [
  ['signedAgreement', 'Signed service agreement'],
  ['oldDeviceImages', 'Old device serial evidence'],
  ['newDeviceImages', 'New device serial evidence'],
]

export default function ServicePlan({ job, canWork, canReview, onUpdated, children }) {
  const path = '/api/operations/service-jobs/' + job.id + '/plans'
  const plans = useApiQuery(path + ':' + job.revision, signal => api.request(path, { signal }))
  const latest = plans.data?.[0]
  const open = ['IN_PROGRESS', 'REWORK'].includes(job.status)
  const propose = open && canWork && plans.data && (!latest || ['REJECTED', 'APPLIED'].includes(latest.status))
  const prices = useApiQuery('service-prices', signal => listAll('/api/public-content', {}, signal), Boolean(propose))
  const options = useApiQuery('service-targets:' + job.id + ':' + job.revision, signal => listAll('/api/service-jobs/' + job.id + '/replacement-options', {}, signal), Boolean(propose))
  const [note, setNote] = useState('')
  const [target, setTarget] = useState('')
  const [lines, setLines] = useState([])
  const [evidence, setEvidence] = useState({})
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const replacement = latest?.uavId || latest?.sensorNodeId || latest?.gatewayId
  const fields = replacement ? evidenceFields : evidenceFields.slice(0, 1)
  async function submit(endpoint, body) {
    if (busy) return
    setBusy(true); setError('')
    try {
      await api.request(endpoint, { method: 'POST', body: { expectedRevision: job.revision, ...body } })
      onUpdated(await api.request('/api/service-jobs/' + job.id))
    } catch (err) { setError(err.message) } finally { setBusy(false) }
  }
  function changeLine(i, patch) { setLines(lines.map((line, index) => index === i ? { ...line, ...patch } : line)) }
  function proposePlan(event) {
    event.preventDefault()
    const device = options.data?.find(row => row.code === target)
    submit(path, { note, lines, ...(device && { sensorNodeId: device.sensorNodeId, uavId: device.uavId, gatewayId: device.gatewayId }) })
  }
  const decisionPath = '/api/operations/service-plans/' + latest?.id
  return <section aria-label="Service plan"><h3>Service plan</h3><ApiState {...plans} />
    {error && <p role="alert">{error}</p>}
    {plans.data?.map(plan => {
      const paper = readPaper(plan.evidenceJson)
      return <article key={plan.id}><h4>Plan #{plan.id} · {plan.status}</h4><p>{plan.note}</p><p>{plan.reviewNote}</p>
        {readPaper(plan.priceLinesJson)?.map((line, i) => <p key={i}>{line.name} × {line.quantity} · {line.totalVnd} VND · {line.reason}</p>)}
        {evidenceFields.map(([key, label]) => paper?.[key]?.length > 0 && <div key={key}>{label}<EvidenceImages images={paper[key]} /></div>)}
      </article>
    })}
    {propose && <form onSubmit={proposePlan}><fieldset disabled={busy}><legend>Propose service work</legend>
      <label>Service plan note<textarea required maxLength={2000} value={note} onChange={e => setNote(e.target.value)} /></label>
      <ApiState {...options} /><label>Replacement device<select value={target} onChange={e => setTarget(e.target.value)}><option value="">Keep current device</option>
        {options.data?.map(d => <option key={d.code} value={d.code}>{d.code} · {d.name}</option>)}</select></label>
      <ApiState {...prices} />
      {lines.map((line, i) => <fieldset key={i}><legend>Component {i+1}</legend>
        <label>Published price<select required value={line.publicContentId || ''} onChange={e => changeLine(i, { publicContentId: Number(e.target.value) })}>
          <option value="">Choose component</option>{prices.data?.filter(d => d.replacementPriceVnd != null).map(d => <option key={d.id} value={d.id}>{d.title} · {d.replacementPriceVnd} VND</option>)}</select></label>
        <label>Quantity<input type="number" required min="1" max="1000" value={line.quantity || ''} onChange={e => changeLine(i, { quantity: Number(e.target.value) })} /></label>
        <label><input type="checkbox" checked={line.warranty} onChange={e => changeLine(i, { warranty: e.target.checked })} />Covered by warranty</label>
        <label>Charge or warranty reason<input required maxLength={1000} value={line.reason} onChange={e => changeLine(i, { reason: e.target.value })} /></label>
        <button type="button" onClick={() => setLines(lines.filter((_, index) => index !== i))}>Remove component</button></fieldset>)}
      <button type="button" disabled={lines.length >= 100} onClick={() => setLines([...lines, { publicContentId: 0, quantity: 1, warranty: false, reason: '' }])}>Add priced component</button>
      <button>Submit service plan</button></fieldset></form>}
    {open && canReview && latest?.status === 'PROPOSED' && <form onSubmit={e => { e.preventDefault(); submit(decisionPath + '/review', { approve: e.nativeEvent.submitter?.value === 'approve', note }) }}>
      <fieldset disabled={busy}><label>Service plan review note<textarea required maxLength={2000} value={note} onChange={e => setNote(e.target.value)} /></label>
      <button value="approve">Approve service plan</button><button value="return">Return service plan</button></fieldset></form>}
    {open && canWork && latest?.status === 'APPROVED' && <fieldset disabled={busy}><legend>Record agreed work</legend>
      {fields.map(([key, label]) => <PaperGroup key={key} label={label} images={evidence[key]} onChange={images => setEvidence(prev => ({ ...prev, [key]: images }))} />)}
      <button disabled={fields.some(([key]) => !evidence[key]?.length)} onClick={() => submit(decisionPath + '/execute', evidence)}>Record service plan execution</button></fieldset>}
    {children?.(latest?.status === 'EXECUTED')}
  </section>
}
