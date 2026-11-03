import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages, iso, money } from '../../services/api/workflow'
import { Panel, Field, Button, State, useAction } from './WorkflowUi'
import EvidenceUpload from './EvidenceUpload'

export function CustomerBinding({ id, onDone }) {
  const query = useApiQuery('customer-binding-options', async signal => ({ owners: (await allPages('/api/users', {}, signal)).filter(u => u.isActive && u.roles.includes('FarmOwner')), farms: await allPages('/api/farms', {}, signal) }))
  const action = useAction(onDone)
  return <Panel title="Liên kết khách hàng với nông trại" description="Chủ vườn phải có tài khoản và đã được cấp quyền vào nông trại. Đây là liên kết hồ sơ thuê, không phải phân công tư vấn."><State query={query}><form className="wf-form" onSubmit={e => { e.preventDefault(); const form = new FormData(e.currentTarget); action.run(() => api.put(`/api/consultations/${id}/customer-farm`, { ownerId: Number(form.get('ownerId')), farmId: Number(form.get('farmId')) })) }}>
    <Field title="Chủ nông trại"><select name="ownerId" required><option value="">Chọn chủ vườn</option>{query.data?.owners.map(row => <option key={row.id} value={row.id}>{row.fullName} · {row.email}</option>)}</select></Field><Field title="Nông trại"><select name="farmId" required><option value="">Chọn nông trại</option>{query.data?.farms.map(row => <option key={row.id} value={row.id}>{row.name}</option>)}</select></Field>{action.errorView}<Button type="submit" disabled={action.busy}>Liên kết hồ sơ</Button>
  </form></State></Panel>
}
export function SurveyForm({ id, revision, onDone }) {
  const [images, setImages] = useState([])
  const [zones, setZones] = useState([{ name: '', crop: '' }])
  const action = useAction(onDone)
  return <Panel title="Ghi nhận khảo sát nhu cầu" description="Không yêu cầu tuổi cây hoặc vị trí đặt cảm biến/gateway. KTV khảo sát vị trí thực tế khi đến lắp."><form className="wf-form" onSubmit={e => {
    e.preventDefault(); const values = Object.fromEntries(new FormData(e.currentTarget))
    action.run(() => api.post(`/api/consultations/${id}/surveys`, { ...values, expectedRevision: revision, farmArea: values.farmArea ? Number(values.farmArea) : null, zones, images }))
  }}>
    <Field title="Tên nông trại" name="farmName" required maxLength={150} /><Field title="Địa chỉ / vị trí nông trại" name="farmLocation" required maxLength={500} /><Field title="Diện tích (ha, nếu biết)" name="farmArea" type="number" min="0.0001" step="any" />
    <Field title="Nhu cầu giám sát" name="monitoringNeeds" required maxLength={4000} /><Field title="Ghi chú tình trạng cây / nhu cầu" name="notes" maxLength={4000} />
    {zones.map((zone, i) => <fieldset key={i}><legend>Khu vực {i + 1}</legend><Field title="Tên khu vực" required value={zone.name} onChange={e => setZones(old => old.map((row, n) => n === i ? { ...row, name: e.target.value } : row))} /><Field title="Loại cây (nếu biết)" value={zone.crop} onChange={e => setZones(old => old.map((row, n) => n === i ? { ...row, crop: e.target.value } : row))} /></fieldset>)}
    <Button onClick={() => setZones(old => [...old, { name: '', crop: '' }])} disabled={zones.length >= 100}>Thêm khu vực</Button><EvidenceUpload title="Ảnh khảo sát nếu có" value={images} onChange={setImages} />{action.errorView}<Button primary type="submit" disabled={action.busy}>Lưu khảo sát nhu cầu</Button>
  </form></Panel>
}
export function ProposalForm({ id, survey, revision, onDone }) {
  const query = useApiQuery('rental-products', signal => allPages('/api/public-content', {}, signal))
  const [lines, setLines] = useState([{ publicContentId: '', surveyZoneId: survey.zones[0]?.id ?? '', quantity: 1 }])
  const action = useAction(onDone)
  const products = query.data?.filter(x => x.kind === 'DEVICE' && x.estimatedMonthlyRentVnd != null) ?? []
  const change = (i, key, value) => setLines(old => old.map((row, n) => n === i ? { ...row, [key]: value } : row))
  return <Panel title="Lập phương án thiết bị & giá thuê"><State query={query}><form className="wf-form" onSubmit={e => {
    e.preventDefault(); const form = new FormData(e.currentTarget)
    action.run(() => api.post(`/api/consultations/${id}/proposals`, { expectedRevision: revision, surveyId: survey.id, rentalMonths: Number(form.get('months')), requestedInstallationAt: iso(form.get('appointment')), notes: form.get('notes') || null,
      lines: lines.map(line => ({ publicContentId: Number(line.publicContentId), surveyZoneId: Number(line.surveyZoneId), quantity: Number(line.quantity), deviceType: products.find(p => p.id === Number(line.publicContentId)).title })),
    }))
  }}>
    {lines.map((line, i) => <fieldset key={i}><legend>Thiết bị {i + 1}</legend><Field title="Sản phẩm theo bảng giá công khai"><select required value={line.publicContentId} onChange={e => change(i, 'publicContentId', e.target.value)}><option value="">Chọn thiết bị</option>{products.map(p => <option key={p.id} value={p.id}>{p.title} · {money(p.estimatedMonthlyRentVnd)}/tháng</option>)}</select></Field><Field title="Khu vực"><select required value={line.surveyZoneId} onChange={e => change(i, 'surveyZoneId', e.target.value)}>{survey.zones.map(z => <option key={z.id} value={z.id}>{z.name}</option>)}</select></Field><Field title="Số lượng" type="number" required min="1" max="1000" value={line.quantity} onChange={e => change(i, 'quantity', e.target.value)} /><Button disabled={lines.length === 1} onClick={() => setLines(old => old.filter((_, n) => n !== i))}>Bỏ dòng</Button></fieldset>)}
    <Button onClick={() => setLines(old => [...old, { publicContentId: '', surveyZoneId: survey.zones[0]?.id, quantity: 1 }])} disabled={lines.length >= 100}>Thêm sản phẩm</Button>
    <Field title="Thời hạn thuê (tháng)" name="months" type="number" min="1" max="120" required defaultValue={12} /><Field title="Ngày giờ khách mong muốn lắp" name="appointment" type="datetime-local" required /><Field title="Ghi chú thỏa thuận" name="notes" maxLength={4000} />{action.errorView}<Button type="submit" primary disabled={action.busy || !products.length}>Lưu phương án với giá đã chốt</Button>
  </form></State></Panel>
}
