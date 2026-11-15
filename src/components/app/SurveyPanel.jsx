import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'
import EvidenceImages from './EvidenceImages'
import { DataTable, SectionCard, TablePagination } from './ui'
import { displayVietnameseTime as displayTime } from '../../utils/display'

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
      const saved = await api.request(path, { method: 'POST', toastMessage: 'Đã lưu khảo sát nông trại.', body: {
        expectedRevision: draft.expectedRevision, farmName: draft.farmName, farmLocation: draft.farmLocation,
        farmArea: draft.farmArea === '' ? null : Number(draft.farmArea), monitoringNeeds: draft.monitoringNeeds, notes: draft.notes || null,
        images: draft.images ?? [],
        zones: draft.zones.map(zone => ({ name: zone.name, crop: zone.crop || null, cropAgeDays: zone.cropAgeDays === '' ? null : Number(zone.cropAgeDays),
          symptoms: zone.symptoms || null, installationLocation: zone.installationLocation })),
      } })
      setSelected(saved); setDraft(null); setPage(1); query.reload(); setNotice(`Đã lưu bản khảo sát số ${saved.revision}.`)
    } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  const field = name => ({ value: draft[name], onChange: event => setDraft({ ...draft, [name]: event.target.value }) })
  const zoneField = (index, name) => ({ value: draft.zones[index][name], onChange: event => setDraft({ ...draft,
    zones: draft.zones.map((zone, i) => i === index ? { ...zone, [name]: event.target.value } : zone) }) })
  return <SectionCard title="Khảo sát nông trại" description="Các phiên bản khảo sát lưu lại những quan sát trước đó và vị trí lắp đặt đề xuất.">
    <ApiState vietnamese {...query} />
    {error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {editable && !draft && !query.loading && !query.error && page === 1 && <button className="app-ui-button" disabled={pending} onClick={() => {
      const latest = query.data?.items[0]
      if (latest) open(latest.id, true)
      else { setDraft(emptySurvey()); setSelected(null); setError(''); setNotice('') }
    }}>{query.data?.items.length ? 'Chỉnh sửa bản khảo sát mới nhất' : 'Ghi nhận khảo sát'}</button>}
    {draft && <form aria-label="Phiên bản khảo sát" className="management-form" onSubmit={save}>
      <fieldset disabled={pending || readingImages} className="management-form"><legend>Phiên bản {draft.expectedRevision + 1}</legend>
        <label>Tên nông trại<input required maxLength={150} {...field('farmName')} /></label>
        <label>Địa điểm nông trại<input required maxLength={500} {...field('farmLocation')} /></label>
        <label>Diện tích nông trại (ha, không bắt buộc)<input type="number" min="0.0001" max="100000000" step="0.0001" {...field('farmArea')} /></label>
        <label>Nhu cầu giám sát<textarea required maxLength={4000} {...field('monitoringNeeds')} /></label>
        <label>Ghi chú khảo sát (không bắt buộc)<textarea maxLength={4000} {...field('notes')} /></label>
        <EvidenceImages vietnamese images={draft.images} onChange={images => setDraft(value => ({ ...value, images }))} onBusyChange={setReadingImages} />
        {draft.zones.map((zone, index) => <fieldset key={index} className="management-form"><legend>Khu vực {index + 1}</legend>
          <label>Tên khu vực<input required maxLength={150} {...zoneField(index, 'name')} /></label>
          <label>Cây trồng<input maxLength={150} {...zoneField(index, 'crop')} /></label>
          <label>Tuổi cây (ngày, không bắt buộc)<input type="number" min="0" max="36500" step="1" {...zoneField(index, 'cropAgeDays')} /></label>
          <label>Triệu chứng của cây (không bắt buộc)<textarea maxLength={2000} {...zoneField(index, 'symptoms')} /></label>
          <label>Vị trí lắp đặt đề xuất<textarea required maxLength={1000} {...zoneField(index, 'installationLocation')} /></label>
          {draft.zones.length > 1 && <button type="button" onClick={() => setDraft({ ...draft, zones: draft.zones.filter((_, i) => i !== index) })}>Xóa khu vực {index + 1}</button>}
        </fieldset>)}
        <button type="button" disabled={draft.zones.length >= 100} onClick={() => setDraft({ ...draft, zones: [...draft.zones, emptyZone()] })}>Thêm khu vực</button>
        <div className="management-row-actions"><button type="submit" className="app-ui-button app-ui-button--primary">Lưu phiên bản khảo sát</button>
          <button type="button" onClick={() => { setDraft(null); setError('') }}>Hủy bản nháp</button></div>
      </fieldset>
    </form>}
    {selected && <section aria-label="Chi tiết khảo sát"><h3>Phiên bản {selected.revision}: {selected.farmName}</h3><p>{selected.farmLocation} · {selected.farmArea ?? 'Chưa rõ'} ha</p>
      <p>{selected.monitoringNeeds}</p><p>{selected.notes}</p><ul>{selected.zones.map((zone, index) => <li key={zone.id ?? index}>
        <strong>{zone.name}</strong> · {zone.crop || 'Chưa ghi nhận cây trồng'} · Tuổi cây: {zone.cropAgeDays ?? 'Chưa rõ'} ngày
        <p>{zone.symptoms}</p><p>Vị trí lắp đặt: {zone.installationLocation}</p></li>)}</ul><EvidenceImages vietnamese images={selected.images} /></section>}
    <DataTable vietnamese caption="Các phiên bản khảo sát" loading={query.loading} rows={query.data?.items ?? []} columns={[
      { key: 'revision', header: 'Phiên bản' }, { key: 'farmName', header: 'Nông trại' }, { key: 'createdAt', header: 'Ngày ghi nhận', render: displayTime },
      { key: 'action', header: 'Thao tác', render: (_, row) => <button disabled={pending || readingImages} onClick={() => open(row.id)}>Xem phiên bản {row.revision}</button> },
    ]} footer={(query.data?.pagination?.totalItems ?? 0) > 0 && <TablePagination page={page} totalPages={query.data?.pagination?.totalPages} pageSize={query.data?.pagination?.pageSize} totalItems={query.data?.pagination?.totalItems} onPageChange={setPage} disabled={query.loading} label="Phân trang các phiên bản khảo sát" />} />
  </SectionCard>
}
