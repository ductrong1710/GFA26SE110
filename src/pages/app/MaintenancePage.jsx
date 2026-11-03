import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages, iso, localInput, time } from '../../services/api/workflow'
import { Workspace, Panel, Field, Button, State, Badge, JobLink, useAction } from '../../components/workflow/WorkflowUi'

const empty = { policy: '', intervalDays: 30, nextDueAt: '', technicianId: '', deviceType: 'SENSOR', enabled: true }
export default function MaintenancePage() {
  const query = useApiQuery('maintenance-schedules', async signal => ({ rows: await allPages('/api/maintenance-schedules', {}, signal), technicians: await allPages('/api/installation-jobs/technicians', {}, signal) }))
  const [record, setRecord] = useState(empty)
  const [historyId, setHistoryId] = useState(null)
  const history = useApiQuery(`maintenance-history:${historyId}`, signal => allPages(`/api/maintenance-schedules/${historyId}/occurrences`, {}, signal), Boolean(historyId))
  const action = useAction(() => { query.reload(); setRecord(empty) })
  const change = (key, value) => setRecord(old => ({ ...old, [key]: value }))
  return <Workspace title="Bảo trì định kỳ" description="Đến hạn, hệ thống tạo ca và giao KTV đã đặt trong lịch. Đổi người của ca đã sinh tại chi tiết công việc."><div className="wf-two-col">
    <Panel title={record.id ? `Chỉnh lịch #${record.id}` : 'Thêm lịch định kỳ'}><State query={query}><form className="wf-form" onSubmit={e => { e.preventDefault(); action.run(() => api.request(`/api/maintenance-schedules${record.id ? `/${record.id}` : ''}`, { method: record.id ? 'PUT' : 'POST', body: {
      policy: record.policy, intervalDays: Number(record.intervalDays), nextDueAt: iso(record.nextDueAt), technicianId: Number(record.technicianId), enabled: record.enabled, expectedRevision: record.revision ?? 0,
      rentalDeviceAssignmentId: record.rentalDeviceAssignmentId ? Number(record.rentalDeviceAssignmentId) : null, deviceType: record.rentalDeviceAssignmentId ? null : record.deviceType,
    } })) }}>
      <Field title="Chính sách / nội dung bảo trì" required maxLength={2000} value={record.policy} onChange={e => change('policy', e.target.value)} /><Field title="Loại thiết bị"><select value={record.deviceType ?? 'SENSOR'} onChange={e => change('deviceType', e.target.value)}><option value="SENSOR">Cảm biến</option><option value="GATEWAY">Gateway</option><option value="UAV">UAV</option></select></Field>
      <Field title="ID liên kết thiết bị thuê cụ thể (để trống áp dụng theo loại)" type="number" min="1" value={record.rentalDeviceAssignmentId ?? ''} onChange={e => change('rentalDeviceAssignmentId', e.target.value)} /><Field title="Chu kỳ ngày" type="number" min="1" max="3650" required value={record.intervalDays} onChange={e => change('intervalDays', e.target.value)} /><Field title="Lần đến hạn tiếp theo" type="datetime-local" required value={record.nextDueAt} onChange={e => change('nextDueAt', e.target.value)} />
      <Field title="Kỹ thuật viên phụ trách"><select required value={record.technicianId} onChange={e => change('technicianId', e.target.value)}><option value="">Chọn KTV</option>{query.data?.technicians.map(t => <option key={t.id} value={t.id}>{t.fullName}</option>)}</select></Field><label><input type="checkbox" checked={record.enabled} onChange={e => change('enabled', e.target.checked)} /> Bật lịch định kỳ</label>{action.errorView}<Button primary type="submit" disabled={action.busy}>Lưu lịch</Button>{record.id && <Button onClick={() => setRecord(empty)}>Hủy chỉnh sửa</Button>}
    </form></State></Panel>
    <Panel title="Lịch đã cấu hình"><State query={query}><ul className="wf-list">{query.data?.rows.map(row => <li key={row.id}><div><h3>{row.policy}</h3><p>{row.intervalDays} ngày/lần · KTV #{row.technicianId}</p><small>{time(row.nextDueAt)} · {row.enabled ? 'Đang bật' : 'Đã tắt'}</small><div className="wf-toolbar"><Button onClick={() => setRecord({ ...row, nextDueAt: localInput(row.nextDueAt) })}>Chỉnh lịch</Button><Button onClick={() => setHistoryId(row.id)}>Lịch sử phát sinh</Button>{row.enabled && <Button disabled={action.busy} onClick={() => action.run(() => api.post(`/api/maintenance-schedules/${row.id}/disable`, { expectedRevision: row.revision }))}>Tắt lịch</Button>}</div></div></li>)}</ul>{!query.data?.rows.length && <p className="wf-empty">Chưa có lịch định kỳ.</p>}</State></Panel></div>
    {historyId && <Panel title={`Lịch sử phát sinh từ lịch #${historyId}`}><State query={history}><ul className="wf-list">{history.data?.map(row => <li key={row.id}><div><strong>{row.deviceCode}</strong><p>{time(row.dueAt)}</p><Badge value={row.status} /></div><JobLink kind="service" id={row.jobId} /></li>)}</ul>{!history.data?.length && <p>Chưa phát sinh công việc.</p>}</State></Panel>}
  </Workspace>
}
