import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'
import EvidenceImages from './EvidenceImages'
import { DataTable, SectionCard } from './ui'
import { displayTime } from '../../utils/display'

const emptyZone = () => ({ name: '', crop: '', cropAgeDays: '', symptoms: '', installationLocation: '' })
const emptySurvey = () => ({ expectedRevision: 0, farmName: '', farmLocation: '', farmArea: '', monitoringNeeds: '', notes: '', zones: [emptyZone()], images: [] })

export default function SurveyPanel({ consultationId, editable }) {
  const [page, setPage] = useState(1)
  const [draft, setDraft] = useState(null)
  const [selected, setSelected] = useState(null)
  const [pending, setPending] = useState(false)
  const [readingImages, setReadingImages] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const path = `/api/consultations/${consultationId}/surveys`
  const query = useApiQuery(`surveys:${consultationId}:${page}`, signal => api.request(path, { query: { page }, signal }))
  async function open(id, edit = false) {
    setPending(true); setError(''); setNotice('')
    try {
      const row = await api.request(`${path}/${id}`)
      if (edit) {
        setSelected(null)
        setDraft({ ...row, expectedRevision: row.revision, farmArea: row.farmArea ?? '', notes: row.notes ?? '',
          zones: row.zones.map(zone => ({ ...zone, crop: zone.crop ?? '', cropAgeDays: zone.cropAgeDays ?? '', symptoms: zone.symptoms ?? '' })) })
      } else { setSelected(row); setDraft(null) }
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  async function save(event) {
    event.preventDefault(); if (pending || readingImages) return
    setPending(true); setError(''); setNotice('')
    try {
      const saved = await api.request(path, { method: 'POST', body: {
        expectedRevision: draft.expectedRevision, farmName: draft.farmName, farmLocation: draft.farmLocation,
        farmArea: draft.farmArea === '' ? null : Number(draft.farmArea), monitoringNeeds: draft.monitoringNeeds, notes: draft.notes || null,
        images: draft.images ?? [],
        zones: draft.zones.map(zone => ({ name: zone.name, crop: zone.crop || null, cropAgeDays: zone.cropAgeDays === '' ? null : Number(zone.cropAgeDays),
          symptoms: zone.symptoms || null, installationLocation: zone.installationLocation })),
      } })
      setSelected(saved); setDraft(null); setPage(1); query.reload(); setNotice(`Survey revision ${saved.revision} saved.`)
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  const field = name => ({ value: draft[name], onChange: event => setDraft({ ...draft, [name]: event.target.value }) })
  const zoneField = (index, name) => ({ value: draft.zones[index][name], onChange: event => setDraft({ ...draft,
    zones: draft.zones.map((zone, i) => i === index ? { ...zone, [name]: event.target.value } : zone) }) })
  return <SectionCard title="Farm surveys" description="Survey revisions preserve earlier observations and proposed installation locations.">
    <ApiState {...query} />
    {error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {editable && !draft && !query.loading && !query.error && page === 1 && <button className="app-ui-button" disabled={pending} onClick={() => {
      const latest = query.data?.items[0]
      if (latest) open(latest.id, true)
      else { setDraft(emptySurvey()); setSelected(null); setError(''); setNotice('') }
    }}>{query.data?.items.length ? 'Revise latest survey' : 'Record survey'}</button>}
    {draft && <form aria-label="Survey revision" className="management-form" onSubmit={save}>
      <fieldset disabled={pending || readingImages} className="management-form"><legend>Revision {draft.expectedRevision + 1}</legend>
        <label>Farm name<input required maxLength={150} {...field('farmName')} /></label>
        <label>Farm location<input required maxLength={500} {...field('farmLocation')} /></label>
        <label>Farm area (ha, optional)<input type="number" min="0.0001" max="100000000" step="0.0001" {...field('farmArea')} /></label>
        <label>Monitoring needs<textarea required maxLength={4000} {...field('monitoringNeeds')} /></label>
        <label>Survey notes (optional)<textarea maxLength={4000} {...field('notes')} /></label>
        <EvidenceImages images={draft.images} onChange={images => setDraft(value => ({ ...value, images }))} onBusyChange={setReadingImages} />
        {draft.zones.map((zone, index) => <fieldset key={index} className="management-form"><legend>Zone {index + 1}</legend>
          <label>Zone name<input required maxLength={150} {...zoneField(index, 'name')} /></label>
          <label>Crop<input maxLength={150} {...zoneField(index, 'crop')} /></label>
          <label>Crop age (days, optional)<input type="number" min="0" max="36500" step="1" {...zoneField(index, 'cropAgeDays')} /></label>
          <label>Crop symptoms (optional)<textarea maxLength={2000} {...zoneField(index, 'symptoms')} /></label>
          <label>Proposed installation location<textarea required maxLength={1000} {...zoneField(index, 'installationLocation')} /></label>
          {draft.zones.length > 1 && <button type="button" onClick={() => setDraft({ ...draft, zones: draft.zones.filter((_, i) => i !== index) })}>Remove zone {index + 1}</button>}
        </fieldset>)}
        <button type="button" disabled={draft.zones.length >= 100} onClick={() => setDraft({ ...draft, zones: [...draft.zones, emptyZone()] })}>Add zone</button>
        <div className="management-row-actions"><button type="submit" className="app-ui-button app-ui-button--primary">Save survey revision</button>
          <button type="button" onClick={() => { setDraft(null); setError('') }}>Discard draft</button></div>
      </fieldset>
    </form>}
    {selected && <section aria-label="Survey details"><h3>Revision {selected.revision}: {selected.farmName}</h3><p>{selected.farmLocation} · {selected.farmArea ?? 'Unknown'} ha</p>
      <p>{selected.monitoringNeeds}</p><p>{selected.notes}</p><ul>{selected.zones.map((zone, index) => <li key={zone.id ?? index}>
        <strong>{zone.name}</strong> · {zone.crop || 'Crop not recorded'} · Age: {zone.cropAgeDays ?? 'Unknown'} days
        <p>{zone.symptoms}</p><p>Installation: {zone.installationLocation}</p></li>)}</ul><EvidenceImages images={selected.images} /></section>}
    <DataTable caption="Survey revisions" loading={query.loading} rows={query.data?.items ?? []} columns={[
      { key: 'revision', header: 'Revision' }, { key: 'farmName', header: 'Farm' }, { key: 'createdAt', header: 'Recorded', render: displayTime },
      { key: 'action', header: 'Action', render: (_, row) => <button disabled={pending || readingImages} onClick={() => open(row.id)}>View revision {row.revision}</button> },
    ]} footer={<div className="management-row-actions"><button disabled={page === 1 || query.loading} onClick={() => setPage(page - 1)}>Previous revisions</button>
      <span>Page {page}</span><button disabled={query.loading || page * 20 >= (query.data?.pagination.totalItems ?? 0)} onClick={() => setPage(page + 1)}>More revisions</button></div>} />
  </SectionCard>
}
