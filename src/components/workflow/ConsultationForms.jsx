import { useWorkflowText, useWorkflowFormat } from '../../utils/useWorkflowText'
import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages, iso } from "../../services/api/workflow"
import { Panel, Field, Button, State, useAction } from './WorkflowUi'
import EvidenceUpload from './EvidenceUpload'

export function SurveyForm({ id, revision, onDone }) {
  const t = useWorkflowText()

  const [images, setImages] = useState([])
  const [zones, setZones] = useState([{ name: '', crop: '' }])
  const action = useAction(onDone)
  return <Panel title={t("Nhập thông tin nông trại sau tư vấn bên ngoài")} description={t("Không yêu cầu tuổi cây hoặc vị trí đặt cảm biến/gateway. KTV khảo sát vị trí thực tế khi đến lắp.")}><form className="wf-form" onSubmit={e => {
    e.preventDefault(); const values = Object.fromEntries(new FormData(e.currentTarget))
    action.run(() => api.post(`/api/consultations/${id}/surveys`, { ...values, expectedRevision: revision, farmArea: values.farmArea ? Number(values.farmArea) : null, zones, images }))
  }}>
    <Field title={t("Tên nông trại")} name="farmName" required maxLength={150} /><Field title={t("Địa chỉ / vị trí nông trại")} name="farmLocation" required maxLength={500} /><Field title={t("Diện tích (ha, nếu biết)")} name="farmArea" type="number" min="0.0001" step="any" />
    <Field title={t("Nhu cầu giám sát")} name="monitoringNeeds" required maxLength={4000} /><Field title={t("Ghi chú tình trạng cây / nhu cầu")} name="notes" maxLength={4000} />
    {zones.map((zone, i) => <fieldset key={i}><legend>{t("Khu vực ")}{i + 1}</legend><Field title={t("Tên khu vực")} required value={zone.name} onChange={e => setZones(old => old.map((row, n) => n === i ? { ...row, name: e.target.value } : row))} /><Field title={t("Loại cây (nếu biết)")} value={zone.crop} onChange={e => setZones(old => old.map((row, n) => n === i ? { ...row, crop: e.target.value } : row))} /></fieldset>)}
    <Button onClick={() => setZones(old => [...old, { name: '', crop: '' }])} disabled={zones.length >= 100}>{t("Thêm khu vực")}</Button><EvidenceUpload title={t("Ảnh khảo sát nếu có")} value={images} onChange={setImages} />{action.errorView}<Button primary type="submit" disabled={action.busy}>{t("Lưu khảo sát nhu cầu")}</Button>
  </form></Panel>
}
export function ProposalForm({ id, survey, revision, onDone }) {
  const t = useWorkflowText()
  const { money } = useWorkflowFormat()

  const query = useApiQuery('rental-products', signal => allPages('/api/public-content', {}, signal))
  const [lines, setLines] = useState([{ publicContentId: '', surveyZoneId: '', quantity: 1 }])
  const action = useAction(onDone)
  const products = query.data?.filter(x => x.kind === 'DEVICE' && x.estimatedMonthlyRentVnd != null) ?? []
  const change = (i, key, value) => setLines(old => old.map((row, n) => n === i ? { ...row, [key]: value } : row))
  return <Panel title={t("Nhập thiết bị và thời hạn đã chốt bên ngoài")}><State query={query}><form className="wf-form" onSubmit={e => {
    e.preventDefault(); const form = new FormData(e.currentTarget)
    action.run(() => api.post(`/api/consultations/${id}/proposals`, { expectedRevision: revision, surveyId: survey.id, rentalMonths: Number(form.get('months')), requestedInstallationAt: iso(form.get('appointment')), notes: form.get('notes') || null,
      lines: lines.map(line => ({ publicContentId: Number(line.publicContentId), surveyZoneId: Number(line.surveyZoneId), quantity: Number(line.quantity), deviceType: products.find(p => p.id === Number(line.publicContentId)).title })),
    }))
  }}>
    {lines.map((line, i) => <fieldset key={i}><legend>{t("Thiết bị ")}{i + 1}</legend><Field title={t("Sản phẩm theo bảng giá công khai")}><select required value={line.publicContentId} onChange={e => change(i, 'publicContentId', e.target.value)}><option value="">{t("Chọn thiết bị")}</option>{products.map(p => <option key={p.id} value={p.id}>{p.title} · {money(p.estimatedMonthlyRentVnd)}{t("/tháng")}</option>)}</select></Field><Field title={t("Khu vực")}><select required value={line.surveyZoneId} onChange={e => change(i, 'surveyZoneId', e.target.value)}><option value="" disabled>Select a zone</option>{survey.zones.map(z => <option key={z.id} value={z.id}>{z.name}</option>)}</select></Field><Field title={t("Số lượng")} type="number" required min="1" max="1000" value={line.quantity} onChange={e => change(i, 'quantity', e.target.value)} /><Button disabled={lines.length === 1} onClick={() => setLines(old => old.filter((_, n) => n !== i))}>{t("Bỏ dòng")}</Button></fieldset>)}
    <Button onClick={() => setLines(old => [...old, { publicContentId: '', surveyZoneId: '', quantity: 1 }])} disabled={lines.length >= 100}>{t("Thêm sản phẩm")}</Button>
    <Field title={t("Thời hạn thuê (tháng)")} name="months" type="number" min="1" max="120" required defaultValue={12} /><Field title={t("Ngày giờ khách mong muốn lắp")} name="appointment" type="datetime-local" required /><Field title={t("Ghi chú thỏa thuận")} name="notes" maxLength={4000} />{action.errorView}<Button type="submit" primary disabled={action.busy || !products.length}>{t("Lưu phương án với giá đã chốt")}</Button>
  </form></State></Panel>
}
