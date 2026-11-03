import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from '../../components/app/ApiState'
import EvidenceImages from '../../components/app/EvidenceImages'
import { PageHeader } from '../../components/app/ui'

const path = '/api/admin/public-content'
const categories = { SERVICE: 'Dịch vụ', DEVICE: 'Thiết bị', INSTALLATION: 'Lắp đặt', WARRANTY: 'Bảo hành', MAINTENANCE: 'Bảo trì', RETURN: 'Hoàn trả', CONTACT: 'Liên hệ' }

function EntryForm({ entry, onSaved, onCancel }) {
  const [kind, setKind] = useState(entry?.kind ?? 'SERVICE')
  const [images, setImages] = useState([])
  const [reading, setReading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  async function save(event) {
    event.preventDefault()
    if (reading || saving) return
    const data = new FormData(event.currentTarget)
    const rent = data.get('rent')
    const body = { expectedRevision: entry?.revision ?? 0, kind, title: data.get('title'), body: data.get('body'),
      estimatedMonthlyRentVnd: rent ? Number(rent) : null, image: images[0] ?? null, removeImage: !images.length && data.has('remove') }
    setSaving(true)
    setError('')
    try {
      await api.request(entry ? `${path}/${entry.id}` : path, { method: entry ? 'PUT' : 'POST', body })
      onSaved()
    } catch (cause) { setError(cause.message) }
    finally { setSaving(false) }
  }
  return <form onSubmit={save}><fieldset className="management-form" disabled={saving}>
    <legend>{entry ? 'Chỉnh sửa nội dung' : 'Tạo bản nháp'}</legend>
    {entry?.published && <p>Thay đổi sẽ hiển thị trên trang chủ ngay khi lưu.</p>}
    <label>Danh mục<select value={kind} onChange={e => setKind(e.target.value)}>{Object.entries(categories).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
    <label>Tiêu đề<input name="title" required maxLength={200} defaultValue={entry?.title} /></label>
    <label>Nội dung<textarea name="body" required maxLength={4000} defaultValue={entry?.body} /></label>
    {kind === 'DEVICE' && <label>Giá thuê tham khảo mỗi tháng (VND)<input type="number" name="rent" min="0" step="0.01" defaultValue={entry?.estimatedMonthlyRentVnd} /></label>}
    {entry?.hasImage && <label><input type="checkbox" name="remove" disabled={Boolean(images.length)} />Xóa ảnh hiện tại</label>}
    <EvidenceImages images={images} maxImages={1} onChange={setImages} onBusyChange={setReading} />
    {error && <p role="alert">{error}</p>}
    <button disabled={reading}>Lưu nội dung</button><button type="button" disabled={reading} onClick={onCancel}>Hủy</button>
  </fieldset></form>
}

export default function SettingsPage() {
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState(null)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const list = useApiQuery(`admin-content:${page}`, signal => api.request(path, { query: { page }, signal }))
  async function publish(entry) {
    setPending(true)
    setError('')
    try {
      await api.post(`${path}/${entry.id}/${entry.published ? 'unpublish' : 'publish'}`, { expectedRevision: entry.revision })
      list.reload()
    } catch (cause) { setError(cause.message); list.reload() }
    finally { setPending(false) }
  }
  return <div className="app-ui"><PageHeader eyebrow="ADMINISTRATION" title="Nội dung trang chủ" description="Quản lý thông tin dịch vụ, thiết bị và chính sách công khai." />
    {editing ? <EntryForm entry={editing === 'new' ? null : editing} onSaved={() => { setEditing(null); list.reload() }} onCancel={() => setEditing(null)} />
      : <button disabled={pending} onClick={() => setEditing('new')}>Tạo bản nháp</button>}
    {error && <p role="alert">{error}</p>}
    <ApiState loading={list.loading} error={list.error} reload={list.reload} />
    {list.data?.items.length === 0 && <p>Chưa có nội dung.</p>}
    <ul>{list.data?.items.map(entry => <li key={entry.id}><strong>{entry.title}</strong> · {categories[entry.kind]} · {entry.published ? 'Đã xuất bản' : 'Bản nháp'}
      <button disabled={pending || Boolean(editing)} onClick={() => setEditing(entry)}>Sửa {entry.title}</button>
      <button disabled={pending || Boolean(editing)} onClick={() => publish(entry)}>{entry.published ? 'Gỡ xuất bản' : 'Xuất bản'} {entry.title}</button></li>)}</ul>
    <button disabled={Boolean(editing) || page === 1} onClick={() => setPage(page - 1)}>Trang trước</button> Trang {page} <button
      disabled={Boolean(editing) || page >= (list.data?.pagination.totalPages ?? 0)} onClick={() => setPage(page + 1)}>Trang sau</button>
  </div>
}
