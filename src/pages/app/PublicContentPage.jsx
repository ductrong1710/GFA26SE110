import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages, money } from '../../services/api/workflow'
import { Workspace, Panel, State, Field, Button, useAction } from '../../components/workflow/WorkflowUi'
import EvidenceUpload from '../../components/workflow/EvidenceUpload'

const kinds = { DEVICE: 'Thiết bị cho thuê', COMPONENT: 'Linh kiện', SERVICE: 'Giới thiệu dịch vụ', INSTALLATION: 'Quy trình lắp đặt', WARRANTY: 'Bảo hành', MAINTENANCE: 'Bảo trì', RETURN: 'Thu hồi', CONTACT: 'Liên hệ nhanh' }
const empty = { kind: 'DEVICE', title: '', body: '', estimatedMonthlyRentVnd: '', replacementPriceVnd: '' }
export default function PublicContentPage() {
  const query = useApiQuery('managed-content', signal => allPages('/api/admin/public-content', {}, signal))
  const [record, setRecord] = useState(empty)
  const [image, setImage] = useState([])
  const [removeImage, setRemoveImage] = useState(false)
  const action = useAction(() => { query.reload(); setRecord(empty); setImage([]); setRemoveImage(false) })
  const change = (key, value) => setRecord(old => ({ ...old, [key]: value }))
  return <Workspace title="Nội dung & bảng giá Homepage" description="Quản lý sản phẩm, giá thuê, giá linh kiện và chính sách công khai. Giá đã chốt trong hồ sơ không thay đổi theo bảng giá mới.">
    <div className="wf-two-col"><Panel title={record.id ? `Sửa nội dung #${record.id}` : 'Thêm nội dung'}><form className="wf-form" onSubmit={e => {
      e.preventDefault(); action.run(() => api.request(`/api/admin/public-content${record.id ? `/${record.id}` : ''}`, { method: record.id ? 'PUT' : 'POST', body: {
        kind: record.kind, title: record.title, body: record.body, expectedRevision: record.revision ?? 0,
        estimatedMonthlyRentVnd: record.kind === 'DEVICE' && record.estimatedMonthlyRentVnd !== '' && record.estimatedMonthlyRentVnd != null ? Number(record.estimatedMonthlyRentVnd) : null,
        replacementPriceVnd: ['DEVICE', 'COMPONENT'].includes(record.kind) && record.replacementPriceVnd !== '' && record.replacementPriceVnd != null ? Number(record.replacementPriceVnd) : null,
        image: image[0] ?? null, removeImage,
      } }))
    }}>
      <Field title="Loại nội dung"><select value={record.kind} onChange={e => change('kind', e.target.value)}>{Object.entries(kinds).map(([key, title]) => <option key={key} value={key}>{title}</option>)}</select></Field>
      <Field title="Tên / tiêu đề" required maxLength={200} value={record.title} onChange={e => change('title', e.target.value)} />
      <Field title={record.kind === 'CONTACT' ? 'Đường dẫn https://… hoặc tel:…' : 'Mô tả / thông số / điều kiện'}><textarea required maxLength={4000} rows={5} value={record.body} onChange={e => change('body', e.target.value)} /></Field>
      {record.kind === 'DEVICE' && <Field title="Giá thuê / tháng (VND)" type="number" min="0" step="0.01" value={record.estimatedMonthlyRentVnd ?? ''} onChange={e => change('estimatedMonthlyRentVnd', e.target.value)} />}
      {['DEVICE', 'COMPONENT'].includes(record.kind) && <Field title="Giá thay thế / đơn vị (VND)" type="number" min="0" step="0.01" value={record.replacementPriceVnd ?? ''} onChange={e => change('replacementPriceVnd', e.target.value)} />}
      <EvidenceUpload title="Ảnh đại diện (chỉ dùng ảnh đầu tiên)" value={image} onChange={value => { setImage(value.slice(0, 1)); setRemoveImage(false) }} />
      {record.hasImage && <label><input type="checkbox" checked={removeImage} onChange={e => { setRemoveImage(e.target.checked); setImage([]) }} /> Gỡ ảnh hiện tại</label>}
      {action.errorView}<div className="wf-toolbar"><Button type="submit" primary disabled={action.busy}>Lưu nội dung</Button>{record.id && <Button onClick={() => { setRecord(empty); setImage([]); setRemoveImage(false) }}>Hủy chỉnh sửa</Button>}</div>
    </form></Panel><Panel title="Danh sách nội dung"><State query={query}><ul className="wf-list">{query.data?.map(row => <li key={row.id}><div><small>{kinds[row.kind]} · {row.published ? 'Đang công khai' : 'Đang ẩn'}</small><h3>{row.title}</h3><p>{row.estimatedMonthlyRentVnd != null ? `${money(row.estimatedMonthlyRentVnd)}/tháng` : row.replacementPriceVnd != null ? money(row.replacementPriceVnd) : ''}</p><div className="wf-toolbar"><Button onClick={() => { setRecord(row); setImage([]); setRemoveImage(false) }}>Chỉnh sửa</Button><Button disabled={action.busy} onClick={() => action.run(() => api.post(`/api/admin/public-content/${row.id}/${row.published ? 'unpublish' : 'publish'}`, { expectedRevision: row.revision }))}>{row.published ? 'Ẩn nội dung' : 'Đăng công khai'}</Button></div></div></li>)}</ul>{!query.data?.length && <p className="wf-empty">Chưa có nội dung. Thêm sản phẩm đầu tiên để hiển thị trên Homepage.</p>}</State></Panel></div>
  </Workspace>
}
