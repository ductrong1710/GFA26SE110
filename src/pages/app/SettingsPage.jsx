import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import EvidenceImages from '../../components/app/EvidenceImages'
import { DataTable, PageHeader, TablePagination } from '../../components/app/ui'
import ManagementDialog from '../../components/app/ManagementDialog'
import QuickContactSettings from '../../components/app/QuickContactSettings'
import { listAll } from '../../services/api/resources'
import { formatVnd } from '../../utils/display'
import '../../styles/management.css'
import '../../styles/content-management.css'

const categories = { SERVICE: 'Dịch vụ', DEVICE: 'Thiết bị', INSTALLATION: 'Lắp đặt', WARRANTY: 'Bảo hành', MAINTENANCE: 'Bảo trì', RETURN: 'Hoàn trả' }
const resource = { SERVICE: 'services', DEVICE: 'devices', INSTALLATION: 'installations', WARRANTY: 'warranties', MAINTENANCE: 'maintenance', RETURN: 'returns' }
const resources = Object.values(resource)
const endpoint = kind => `/api/admin/public-${resource[kind]}`
const PAGE_SIZE = 10

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
      estimatedMonthlyRentVnd: rent ? Number(rent) : null,
      image: images[0] ?? null, removeImage: !images.length && data.has('remove') }
    setSaving(true)
    setError('')
    try {
      await api.request(entry ? `${endpoint(entry.kind)}/${entry.id}` : endpoint(kind), { method: entry ? 'PUT' : 'POST', body })
      onSaved()
    } catch (cause) { setError(cause.message) }
    finally { setSaving(false) }
  }
  return <form className="content-editor-form" onSubmit={save}><fieldset className="management-form" disabled={saving}>
    <legend>{entry ? 'Chỉnh sửa nội dung' : 'Tạo bản nháp'}</legend>
    {entry?.published && <p>Thay đổi sẽ hiển thị trên trang chủ ngay khi lưu.</p>}
    <label>Danh mục<select disabled={Boolean(entry)} value={kind} onChange={e => setKind(e.target.value)}>{Object.entries(categories).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
    <label>Tiêu đề<input name="title" required maxLength={200} defaultValue={entry?.title} /></label>
    <label>Nội dung<textarea name="body" required maxLength={4000} defaultValue={entry?.body} /></label>
    {kind === 'DEVICE' && <label>Giá thuê mỗi tháng (VND)<input type="number" name="rent" min="0" step="0.01" defaultValue={entry?.estimatedMonthlyRentVnd} /></label>}
    {entry?.hasImage && <label><input type="checkbox" name="remove" disabled={Boolean(images.length)} />Xóa ảnh hiện tại</label>}
    <EvidenceImages images={images} maxImages={1} onChange={setImages} onBusyChange={setReading} />
    {error && <p role="alert">{error}</p>}
    <div className="content-editor-actions"><button className="app-ui-button app-ui-button--primary" disabled={reading}>Lưu nội dung</button><button className="app-ui-button" type="button" disabled={reading} onClick={onCancel}>Hủy</button></div>
  </fieldset></form>
}

export default function SettingsPage() {
  const [page, setPage] = useState(1)
  const [filter, setFilter] = useState('ALL')
  const [editing, setEditing] = useState(null)
  const [details, setDetails] = useState(null)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const list = useApiQuery('admin-content-by-kind', async signal => {
    const groups = await Promise.all(resources.map(name => listAll(`/api/admin/public-${name}`, {}, signal)))
    const items = groups.flat().sort((a, b) => b.id - a.id)
    return { items }
  })
  const allItems = list.data?.items ?? []
  const isContactTab = filter === 'CONTACT'
  const filteredItems = filter === 'ALL' ? allItems : isContactTab ? [] : allItems.filter(entry => entry.kind === filter)
  const pageCount = Math.max(1, Math.ceil(filteredItems.length / PAGE_SIZE))
  const pageStart = (page - 1) * PAGE_SIZE
  const items = filteredItems.slice(pageStart, pageStart + PAGE_SIZE)
  const columns = [
    { key: 'title', header: 'Tiêu đề', render: (_, entry) => <strong className="content-table-title">{entry.title}</strong> },
    { key: 'kind', header: 'Danh mục', render: (_, entry) => <span className="content-table-category">{categories[entry.kind]}</span> },
    { key: 'published', header: 'Trạng thái', render: (_, entry) => <span className={`content-table-status ${entry.published ? 'is-published' : 'is-draft'}`}>
      {entry.published ? 'Đang hiển thị' : 'Đã ẩn'}</span> },
    { key: 'actions', header: 'Thao tác', render: (_, entry) => <div className="content-table-actions">
      <button className="app-ui-button" type="button" onClick={() => setDetails(entry)}>Xem chi tiết</button>
    </div> },
  ]
  async function publish(entry) {
    setPending(true)
    setError('')
    try {
      await api.post(`${endpoint(entry.kind)}/${entry.id}/${entry.published ? 'unpublish' : 'publish'}`, { expectedRevision: entry.revision })
      list.reload()
      return true
    } catch (cause) { setError(cause.message); list.reload(); return false }
    finally { setPending(false) }
  }
  return <>
  <main className="app-ui app-ui-page content-management">
    <PageHeader eyebrow="QUẢN TRỊ HOMEPAGE" title="Nội dung công khai" description={isContactTab
      ? 'Cập nhật Zalo, Messenger và số điện thoại hiển thị trên Homepage.'
      : 'Quản lý thiết bị, dịch vụ và các thông tin khách hàng nhìn thấy trên homepage.'}
      actions={!isContactTab && <button className="app-ui-button app-ui-button--primary" disabled={pending || Boolean(editing)} onClick={() => setEditing('new')}>＋ Tạo nội dung</button>} />
    {editing && !isContactTab && <ManagementDialog className="content-editor-dialog" title={editing === 'new' ? 'Tạo nội dung homepage' : 'Chỉnh sửa nội dung homepage'} onClose={() => setEditing(null)}>
      <EntryForm entry={editing === 'new' ? null : editing}
        onSaved={() => { setEditing(null); setFilter('ALL'); setPage(1); list.reload() }} onCancel={() => setEditing(null)} />
    </ManagementDialog>}
    {error && <p className="content-management-alert" role="alert">{error}</p>}
    <section className="content-list-panel" aria-label={isContactTab ? 'Quản lý liên hệ nhanh' : 'Danh sách nội dung Homepage'}>
      {!isContactTab && <div className="content-list-heading"><div><p className="app-ui-eyebrow">DANH MỤC</p><h2 id="content-list-heading">Nội dung homepage</h2></div>
        <span>{filteredItems.length} mục</span></div>}
      <div className="content-category-filters" aria-label="Lọc danh mục">
        <button aria-pressed={filter === 'ALL'} onClick={() => { setFilter('ALL'); setPage(1) }}>Tất cả</button>
        {Object.entries(categories).map(([key, label]) => <button key={key} aria-pressed={filter === key}
          onClick={() => { setFilter(key); setPage(1) }}>{label}</button>)}
        <button aria-pressed={isContactTab} onClick={() => { setFilter('CONTACT'); setPage(1); setEditing(null) }}>Liên hệ</button>
      </div>
      {isContactTab ? <QuickContactSettings /> : <>
        <DataTable className="content-management-table" caption="Nội dung Homepage" columns={columns} rows={items}
          loading={list.loading} error={list.error} errorAction={<button className="app-ui-button" type="button" onClick={list.reload}>Thử lại</button>}
          emptyState={<div className="content-empty-state"><strong>Chưa có nội dung trong danh mục này</strong>
            <span>Tạo một mục mới để bắt đầu quản lý nội dung homepage.</span></div>} />
        {filteredItems.length > 0 && <TablePagination page={page} totalPages={pageCount} pageSize={PAGE_SIZE}
          totalItems={filteredItems.length} onPageChange={setPage} disabled={Boolean(editing)} label="Phân trang nội dung Homepage" />}
      </>}
    </section>
  </main>
  {details && <ManagementDialog title="Chi tiết nội dung" onClose={() => setDetails(null)}>
    <div className="management-drawer-body content-details-body">
      <dl className="management-detail-grid">
        <div><dt>Tiêu đề</dt><dd>{details.title}</dd></div>
        <div><dt>Danh mục</dt><dd>{categories[details.kind]}</dd></div>
        <div><dt>Trạng thái</dt><dd>{details.published ? 'Đang hiển thị trên Homepage' : 'Đã ẩn khỏi Homepage'}</dd></div>
        {details.estimatedMonthlyRentVnd != null && <div><dt>Giá thuê</dt><dd>{formatVnd(details.estimatedMonthlyRentVnd)} / tháng</dd></div>}
        <div className="content-details-body-text"><dt>Nội dung</dt><dd>{details.body}</dd></div>
      </dl>
    </div>
    <footer>
      <button className="app-ui-button" type="button" onClick={() => { setEditing(details); setDetails(null) }}>Chỉnh sửa</button>
      <button className={`app-ui-button ${details.published ? '' : 'app-ui-button--primary'}`} type="button" disabled={pending}
        onClick={async () => { if (await publish(details)) setDetails(null) }}>{details.published ? 'Ẩn nội dung' : 'Công khai nội dung'}</button>
      <button className="app-ui-button" type="button" onClick={() => setDetails(null)}>Đóng</button>
    </footer>
  </ManagementDialog>}
  </>
}
