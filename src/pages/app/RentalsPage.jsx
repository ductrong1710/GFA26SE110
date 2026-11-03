import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages, time, money, iso } from '../../services/api/workflow'
import { Workspace, Panel, State, Badge, Field, Button, PaperGallery, useAction } from '../../components/workflow/WorkflowUi'

export default function RentalsPage() {
  const { activeRole } = useAuth()
  const [params, setParams] = useSearchParams()
  const [status, setStatus] = useState('')
  const list = useApiQuery(`rentals:${activeRole}`, signal => allPages('/api/rental-contracts', {}, signal))
  const id = params.get('id')
  const query = useApiQuery(`rental:${id}`, signal => api.get(`/api/rental-contracts/${id}`, {}, signal), Boolean(id))
  const action = useAction(() => { query.reload(); list.reload() })
  const row = query.data
  const [mode, setMode] = useState('extend')
  return <Workspace title="Hồ sơ thuê & gia hạn" description="Hợp đồng ký giấy, giá đã chốt và thiết bị bàn giao. Thanh toán thực hiện bên ngoài hệ thống.">
    <div className="wf-two-col"><Panel title="Danh sách hồ sơ"><Field title="Trạng thái thuê"><select value={status} onChange={e => setStatus(e.target.value)}><option value="">Tất cả</option>{['DRAFT', 'ACTIVE', 'EXPIRED', 'ENDED'].map(v => <option key={v}>{v}</option>)}</select></Field><State query={list}><ul className="wf-list">{list.data?.filter(row => !status || row.status === status).map(row => <li key={row.id}><div><Badge value={row.status} /><p>Nông trại #{row.farmId}</p><small>Kết thúc {time(row.endAt)}</small></div><Button onClick={() => setParams({ id: row.id })}>Hồ sơ #{row.id}</Button></li>)}</ul>{!list.data?.length && <p className="wf-empty">Hồ sơ được tạo sau khi Admin duyệt báo cáo lắp đặt và giấy bàn giao.</p>}</State></Panel>
      <Panel title={id ? `Chi tiết hồ sơ #${id}` : 'Chọn hồ sơ để xem'}><State query={query}>{row && <>
        <Badge value={row.status} /><dl className="wf-facts"><div><dt>Bắt đầu thuê</dt><dd>{time(row.startAt)}</dd></div><div><dt>Kết thúc thuê</dt><dd>{time(row.endAt)}</dd></div><div><dt>Tiền kỳ thuê ban đầu</dt><dd>{money(row.totalRentVnd)}</dd></div></dl>
        <PaperGallery value={row.paperworkJson} /><ul className="wf-list">{row.devices.map(d => <li key={d.id}><div><strong>{d.name} · {d.code}</strong><p>{d.installationLocation}</p><small>{d.returnedAt ? `Đã kết thúc liên kết: ${time(d.returnedAt)}` : 'Đang được gán'}</small></div></li>)}</ul>
        {activeRole === ROLES.ADMINISTRATOR && row.status !== 'ENDED' && <form className="wf-form" onSubmit={e => { e.preventDefault(); const form = new FormData(e.currentTarget); action.run(() => api.post(`/api/rental-contracts/${row.id}/${mode}`, { expectedRevision: row.revision, note: form.get('note'), ...(mode === 'extend' ? { endAt: iso(form.get('endAt')) } : { devicesReturned: form.has('returned') }) })) }}>
          <Field title="Thao tác"><select value={mode} onChange={e => setMode(e.target.value)}><option value="extend">Ghi nhận gia hạn đã xác nhận</option><option value="end">Kết thúc sau thu hồi</option></select></Field>
          {mode === 'extend' ? <Field title="Ngày kết thúc mới" name="endAt" type="datetime-local" required /> : <label><input name="returned" type="checkbox" required /> Xác nhận đã thực sự nhận lại toàn bộ thiết bị đang gán</label>}
          <Field title="Ghi chú / tham chiếu thỏa thuận ngoài hệ thống" name="note" required maxLength={2000} />{action.errorView}<Button type="submit" disabled={action.busy}>Xác nhận cập nhật hồ sơ</Button>
        </form>}
        <h3>Lịch sử hồ sơ</h3><ol className="wf-timeline">{row.history.map(h => <li key={h.id}><strong>{h.action}</strong><p>{h.note}</p><small>{time(h.createdAt)}</small></li>)}</ol>
      </>}</State></Panel></div>
  </Workspace>
}
