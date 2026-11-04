import { useWorkflowText, useWorkflowFormat } from '../../utils/useWorkflowText'
import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { rentalDemo } from '../../components/workflow/rentalDemo'
import { allPages, iso } from "../../services/api/workflow"
import { Workspace, Panel, State, Badge, Field, Button, PaperGallery, useAction } from '../../components/workflow/WorkflowUi'

export default function RentalsPage() {
  const t = useWorkflowText()
  const { time, money } = useWorkflowFormat()

  const { activeRole } = useAuth()
  const [params, setParams] = useSearchParams()
  const demo = params.get('demo') === '1'
  const [status, setStatus] = useState('')
  const liveList = useApiQuery(`rentals:${activeRole}`, signal => allPages('/api/rental-contracts', {}, signal), !demo)
  const id = params.get('id') || (demo ? String(rentalDemo[0].id) : null)
  const liveQuery = useApiQuery(`rental:${id}`, signal => api.get(`/api/rental-contracts/${id}`, {}, signal), Boolean(id) && !demo)
  const list = demo ? { data: rentalDemo, loading: false, error: null } : liveList
  const query = demo ? { data: rentalDemo.find(item => String(item.id) === id), loading: false, error: null } : liveQuery
  const action = useAction(() => { liveQuery.reload(); liveList.reload() })
  const row = query.data
  const [mode, setMode] = useState('')
  return <Workspace title={t("Hồ sơ thuê & gia hạn")} description={t("Hợp đồng ký giấy, giá đã chốt và thiết bị bàn giao. Thanh toán thực hiện bên ngoài hệ thống.")}>
    <div className="wf-toolbar"><Button onClick={() => { setParams(demo ? {} : { demo: '1' }); setStatus(''); setMode('') }}>{demo ? 'Xem dữ liệu thật' : 'Xem dữ liệu mẫu'}</Button>{demo && <p role="status">Dữ liệu giả lập · 4 hồ sơ mẫu · Chỉ xem, không lưu vào hệ thống.</p>}</div>
    <div className="wf-two-col"><Panel title={t("Danh sách hồ sơ")}><Field title={t("Trạng thái thuê")}><select value={status} onChange={e => setStatus(e.target.value)}><option value="">{t("Tất cả")}</option>{['DRAFT', 'ACTIVE', 'EXPIRED', 'ENDED'].map(v => <option key={v}>{v}</option>)}</select></Field><State query={list}><ul className="wf-list">{list.data?.filter(row => !status || row.status === status).map(row => <li key={row.id}><div><Badge value={row.status} /><p>{t("Nông trại #")}{row.farmId}</p><small>{t("Kết thúc ")}{time(row.endAt)}</small></div><Button onClick={() => { setParams({ ...(demo ? { demo: '1' } : {}), id: row.id }); setMode('') }}>{t("Hồ sơ #")}{row.id}</Button></li>)}</ul>{!list.data?.length && <p className="wf-empty">{t("Hồ sơ được tạo sau khi Admin duyệt báo cáo lắp đặt và giấy bàn giao.")}</p>}</State></Panel>
      <Panel title={id ? (t("Chi tiết hồ sơ #") + (id)) : t("Chọn hồ sơ để xem")}><State query={query}>{row && <>
        <Badge value={row.status} /><dl className="wf-facts"><div><dt>{t("Bắt đầu thuê")}</dt><dd>{time(row.startAt)}</dd></div><div><dt>{t("Kết thúc thuê")}</dt><dd>{time(row.endAt)}</dd></div><div><dt>{t("Tiền kỳ thuê ban đầu")}</dt><dd>{money(row.totalRentVnd)}</dd></div></dl>
        <PaperGallery value={row.paperworkJson} />{demo && <p className="wf-muted">Hồ sơ mẫu chưa đính kèm ảnh hợp đồng, bàn giao hoặc hóa đơn.</p>}<ul className="wf-list">{row.devices.map(d => <li key={d.id}><div><strong>{d.name} · {d.code}</strong><p>{d.installationLocation}</p><small>{d.returnedAt ? (t("Đã kết thúc liên kết: ") + (time(d.returnedAt))) : t("Đang được gán")}</small></div></li>)}</ul>
        {!demo && activeRole === ROLES.ADMINISTRATOR && row.status !== 'ENDED' && <form className="wf-form" onSubmit={e => { e.preventDefault(); if (!mode) return; const form = new FormData(e.currentTarget); action.run(() => api.post(`/api/rental-contracts/${row.id}/${mode}`, { expectedRevision: row.revision, note: form.get('note'), ...(mode === 'extend' ? { endAt: iso(form.get('endAt')) } : { devicesReturned: form.has('returned') }) })) }}>
          <Field title={t("Thao tác")}><select required value={mode} onChange={e => setMode(e.target.value)}><option value="" disabled>Select action</option><option value="extend">{t("Ghi nhận gia hạn đã xác nhận")}</option><option value="end">{t("Kết thúc sau thu hồi")}</option></select></Field>
          {mode && <>{mode === 'extend' ? <Field title={t("Ngày kết thúc mới")} name="endAt" type="datetime-local" required /> : <label><input name="returned" type="checkbox" required />{t(" Xác nhận đã thực sự nhận lại toàn bộ thiết bị đang gán")}</label>}
          <Field title={t("Ghi chú / tham chiếu thỏa thuận ngoài hệ thống")} name="note" required maxLength={2000} />{action.errorView}<Button type="submit" disabled={action.busy}>{t("Xác nhận cập nhật hồ sơ")}</Button></>}
        </form>}
        <h3>{t("Lịch sử hồ sơ")}</h3><ol className="wf-timeline">{row.history.map(h => <li key={h.id}><strong>{h.action}</strong><p>{h.note}</p><small>{time(h.createdAt)}</small></li>)}</ol>
      </>}</State></Panel></div>
  </Workspace>
}
