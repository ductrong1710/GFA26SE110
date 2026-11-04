import { useWorkflowText } from '../../utils/useWorkflowText'
import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { iso, localInput } from '../../services/api/workflow'
import { Field, Button, State, useAction } from './WorkflowUi'

export default function AssignmentForm({ path, revision = 0, kind = 'installation', jobId, type, onDone, assignmentId }) {
  const t = useWorkflowText()

  const [start, setStart] = useState(() => localInput(Date.now() + 86400000))
  const [end, setEnd] = useState(() => localInput(Date.now() + 93600000))
  const [technician, setTechnician] = useState('')
  const [note, setNote] = useState('')
  const action = useAction(onDone)
  const valid = Boolean(start && end && new Date(start) < new Date(end))
  const query = useApiQuery(`availability:${start}:${end}:${kind}:${jobId}`, signal => api.get('/api/operations/available-technicians', {
    from: iso(start), to: iso(end), ...(jobId ? { excludeType: kind === 'installation' ? 'INSTALLATION' : 'SERVICE', excludeId: jobId } : {}),
  }, signal), valid)
  const options = query.data?.technicians ?? []
  const selected = options.some(t => t.id === Number(technician)) ? technician : ''
  return <form className="wf-form" onSubmit={e => { e.preventDefault(); action.run(() => api.post(path, {
    expectedRevision: revision, technicianId: Number(selected), scheduledAt: iso(start), scheduledEndAt: iso(end), note,
    ...(type ? { type } : {}), ...(assignmentId ? { rentalDeviceAssignmentId: Number(assignmentId) } : {}),
  })) }}>
    <div className="wf-two-col"><Field title={t("Bắt đầu dự kiến")} type="datetime-local" required value={start} onChange={e => { setStart(e.target.value); setTechnician('') }} />
      <Field title={t("Kết thúc dự kiến")} type="datetime-local" required value={end} onChange={e => { setEnd(e.target.value); setTechnician('') }} /></div>
    {!valid && <p role="alert">{t("Giờ kết thúc phải sau giờ bắt đầu.")}</p>}
    <State query={query}><Field title={t("Kỹ thuật viên không trùng lịch")}><select required value={selected} onChange={e => setTechnician(e.target.value)}><option value="">{t("Chọn kỹ thuật viên")}</option>{options.map(technician => <option key={technician.id} value={technician.id}>{technician.fullName}{query.data?.recommended?.id === technician.id ? t(" · Đề xuất") : ''}</option>)}</select></Field>
      {valid && !query.loading && !query.error && !options.length && <p className="wf-muted">{t("Không có KTV rảnh trong khung giờ này. Hãy đổi lịch.")}</p>}</State>
    <Field title={t("Ghi chú / lý do phân công")} required maxLength={2000} value={note} onChange={e => setNote(e.target.value)} />
    <small>{t("Đã kiểm tra lịch lắp đặt, bảo trì và sửa chữa, gồm 30 phút di chuyển. BE kiểm tra lại khi lưu.")}</small>
    {action.errorView}<Button type="submit" primary disabled={action.busy || query.loading || !selected || !valid}>{action.busy ? t("Đang lưu…") : t("Xác nhận phân công")}</Button>
  </form>
}
