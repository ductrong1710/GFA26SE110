import { useWorkflowText, useWorkflowFormat } from '../../utils/useWorkflowText'
import { useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages } from "../../services/api/workflow"
import { Workspace, Panel, State, Field, Button, Badge, useAction } from '../../components/workflow/WorkflowUi'
import EvidenceUpload from '../../components/workflow/EvidenceUpload'

const optionalNumber = value => value === '' || value == null ? null : Number(value)
export default function CropSeasonsPage() {
  const t = useWorkflowText()
  const { money } = useWorkflowFormat()

  const { activeRole } = useAuth()
  const { farmWorkspace } = useOutletContext()
  const owner = activeRole === ROLES.FARM_OWNER
  const list = useApiQuery(`crop-seasons:${activeRole}`, signal => allPages('/api/crop-seasons', {}, signal))
  const [id, setId] = useState(null)
  const [create, setCreate] = useState(false)
  const [images, setImages] = useState([])
  const [selected, setSelected] = useState([])
  const query = useApiQuery(`season-activities:${id}:${list.data?.find(s => s.id === id)?.revision}`, signal => allPages(`/api/crop-seasons/${id}/activities`, {}, signal), Boolean(id))
  const compare = useApiQuery(`season-comparison:${selected.join(',')}`, signal => api.get(`/api/crop-seasons/compare?${selected.map(id => `ids=${id}`).join('&')}`, {}, signal), selected.length >= 2)
  const action = useAction(() => { list.reload(); query.reload(); setCreate(false); setImages([]) })
  const row = list.data?.find(row => row.id === id)
  return <Workspace title={t("Mùa vụ & nhật ký canh tác")} description={t("Ghi nhận thủ công để đối chiếu lịch sử. Thông tin chưa biết được giữ trống, không suy đoán số liệu.")} actions={owner && <Button primary onClick={() => setCreate(v => !v)}>{t("Thêm mùa vụ")}</Button>}>
    {create && <Panel title={t("Tạo mùa vụ")}><form className="wf-form" onSubmit={e => {
      e.preventDefault(); const form = Object.fromEntries(new FormData(e.currentTarget))
      action.run(() => api.post('/api/crop-seasons', { expectedRevision: 0, farmId: Number(form.farmId), zoneId: null, name: form.name, crop: form.crop, areaHectares: optionalNumber(form.area), startDate: form.startDate || null, expectedEndDate: null, cropAgeDays: null, notes: form.notes || null }))
    }}>
      <Field title={t("Nông trại")}><select name="farmId" required><option value="">{t("Chọn nông trại")}</option>{farmWorkspace.farms.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}</select></Field><Field title={t("Tên mùa vụ")} name="name" required maxLength={200} /><Field title={t("Loại cây")} name="crop" required maxLength={200} /><Field title={t("Diện tích ha (nếu biết)")} name="area" type="number" min="0.0001" step="0.0001" /><Field title={t("Ngày bắt đầu (nếu biết)")} name="startDate" type="date" /><Field title={t("Ghi chú")} name="notes" maxLength={4000} />{action.errorView}<Button type="submit" disabled={action.busy}>{t("Lưu mùa vụ")}</Button>
    </form></Panel>}
    <div className="wf-two-col"><Panel title={t("Mùa vụ đã ghi nhận")}><State query={list}><ul className="wf-list">{list.data?.map(season => <li key={season.id}><div><h3>{season.name}</h3><p>{season.crop}{t(" · Nông trại #")}{season.farmId}</p><Badge value={season.status} /><label><input type="checkbox" checked={selected.includes(season.id)} onChange={e => setSelected(old => e.target.checked ? [...old, season.id] : old.filter(id => id !== season.id))} />{t(" So sánh")}</label></div><Button onClick={() => setId(season.id)}>{t("Nhật ký")}</Button></li>)}</ul>{!list.data?.length && <p className="wf-empty">{t("Chưa có mùa vụ. Thêm mùa vụ để bắt đầu ghi chép.")}</p>}</State></Panel>
      <Panel title={row ? row.name : t("Chọn mùa vụ để xem nhật ký")}>{row && <>
        <State query={query}><ul className="wf-list">{query.data?.map(activity => <li key={activity.id}><div><strong>{activity.date} · {activity.type}</strong><p>{activity.description}</p><small>{money(activity.costVnd)}</small></div></li>)}</ul>{!query.data?.length && <p>{t("Chưa có hoạt động được ghi nhận.")}</p>}</State>
        {owner && row.status === 'ACTIVE' && <>
          <form className="wf-form" onSubmit={e => { e.preventDefault(); const form = Object.fromEntries(new FormData(e.currentTarget)); action.run(() => api.post(`/api/crop-seasons/${id}/activities`, { expectedRevision: row.revision, date: form.date, type: form.type, description: form.description, costVnd: optionalNumber(form.cost), notes: null, images })) }}>
            <h3>{t("Ghi hoạt động chăm sóc")}</h3><Field title={t("Ngày thực hiện")} name="date" type="date" required /><Field title={t("Hoạt động")}><select name="type" required defaultValue=""><option value="" disabled>Select activity type</option><option value="WATERING">{t("Tưới nước")}</option><option value="FERTILIZING">{t("Bón phân")}</option><option value="PESTICIDE">{t("Phun thuốc")}</option><option value="INSPECTION">{t("Kiểm tra sâu bệnh")}</option><option value="OTHER">{t("Khác")}</option></select></Field><Field title={t("Nội dung")} name="description" required maxLength={4000} /><Field title={t("Chi phí VND (nếu có)")} name="cost" type="number" min="0" step="0.01" /><EvidenceUpload title={t("Ảnh nhật ký")} value={images} onChange={setImages} />{action.errorView}<Button type="submit" disabled={action.busy}>{t("Lưu hoạt động")}</Button>
          </form>
          <details><summary>{t("Kết thúc mùa vụ và ghi kết quả")}</summary><form className="wf-form" onSubmit={e => { e.preventDefault(); const form = Object.fromEntries(new FormData(e.currentTarget)); action.run(() => api.post(`/api/crop-seasons/${id}/complete`, { expectedRevision: row.revision, endDate: form.endDate || null, yieldKg: optionalNumber(form.yieldKg), totalCostVnd: optionalNumber(form.cost), revenueVnd: optionalNumber(form.revenue), notes: form.notes || null })) }}>
            <Field title={t("Ngày kết thúc (nếu biết)")} name="endDate" type="date" /><Field title={t("Sản lượng kg (nếu biết)")} name="yieldKg" type="number" min="0" step="0.0001" /><Field title={t("Tổng chi phí VND (nếu biết)")} name="cost" type="number" min="0" step="0.01" /><Field title={t("Doanh thu VND (nếu biết)")} name="revenue" type="number" min="0" step="0.01" /><Field title={t("Ghi chú kết quả")} name="notes" maxLength={4000} />{action.errorView}<Button type="submit" disabled={action.busy}>{t("Xác nhận kết thúc mùa vụ")}</Button>
          </form></details>
        </>}
      </>}</Panel></div>
    {selected.length >= 2 && <Panel title={t("So sánh mùa vụ đã chọn")}><State query={compare}><div className="wf-report-grid">{compare.data?.map((season, i) => <article key={season.id ?? i}><h3>{season.name}</h3><dl><dt>{t("Diện tích")}</dt><dd>{season.areaHectares ?? t("Chưa có dữ liệu")} ha</dd><dt>{t("Sản lượng")}</dt><dd>{season.yieldKg ?? t("Chưa có dữ liệu")} kg</dd><dt>{t("Chi phí")}</dt><dd>{money(season.totalCostVnd)}</dd><dt>{t("Doanh thu")}</dt><dd>{money(season.revenueVnd)}</dd><dt>{t("Lợi nhuận")}</dt><dd>{money(season.profitVnd)}</dd></dl></article>)}</div></State></Panel>}
  </Workspace>
}
