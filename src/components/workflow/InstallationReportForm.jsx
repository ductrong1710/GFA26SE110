import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { iso, localInput } from '../../services/api/workflow'
import { Field, Button, State, useAction } from './WorkflowUi'
import EvidenceUpload from './EvidenceUpload'

export default function InstallationReportForm({ job, onDone }) {
  const path = `/api/installation-jobs/${job.id}`
  const query = useApiQuery(`allocated:${job.id}:${job.revision}`, signal => api.get(`${path}/devices`, {}, signal))
  const [checks, setChecks] = useState({})
  const [paper, setPaper] = useState({ contract: [], handover: [], bills: [] })
  const [images, setImages] = useState([])
  const [completedAt, setCompletedAt] = useState(() => localInput())
  const action = useAction(onDone)
  const setCheck = (id, key, value) => setChecks(old => ({ ...old, [id]: { ...old[id], [key]: value } }))
  return <State query={query}><form className="wf-form" onSubmit={event => {
    event.preventDefault(); const form = new FormData(event.currentTarget)
    action.run(() => api.post(`${path}/reports`, { expectedRevision: job.revision, notes: form.get('notes'), issues: form.get('issues') || null, completedAt: iso(completedAt), images,
      paperwork: { ...paper, paperContractNumber: form.get('paperNumber') || null }, devices: (query.data ?? []).map(device => ({ ...device, ...checks[device.id], accessoriesChecked: Boolean(checks[device.id]?.accessoriesChecked), connectivityPassed: Boolean(checks[device.id]?.connectivityPassed), operationPassed: Boolean(checks[device.id]?.operationPassed) })),
    }))
  }}>
    {!(query.data ?? []).length && <p className="wf-error">Chưa có thiết bị được Admin cấp phát. Liên hệ Admin trước khi gửi báo cáo.</p>}
    {(query.data ?? []).map(device => <fieldset key={device.id}><legend>{device.name} · {device.code}</legend>
      <Field title="Vị trí lắp thực tế sau khảo sát" required value={checks[device.id]?.installationLocation ?? ''} onChange={e => setCheck(device.id, 'installationLocation', e.target.value)} />
      <div className="wf-checks">{[['accessoriesChecked', 'Phụ kiện đạt'], ['connectivityPassed', 'Kết nối đạt'], ['operationPassed', 'Vận hành đạt']].map(([key, title]) => <label key={key}><input type="checkbox" checked={checks[device.id]?.[key] ?? false} onChange={e => setCheck(device.id, key, e.target.checked)} />{title}</label>)}</div>
      <Field title="Kết quả chạy thử" required maxLength={2000} value={checks[device.id]?.testResults ?? ''} onChange={e => setCheck(device.id, 'testResults', e.target.value)} />
    </fieldset>)}
    <Field title="Ngày giờ hoàn tất bàn giao thực tế" type="datetime-local" required value={completedAt} onChange={e => setCompletedAt(e.target.value)} />
    <Field title="Ghi chú hoàn tất" name="notes" required maxLength={4000} /><Field title="Vấn đề phát sinh / kiểm tra chưa đạt" name="issues" maxLength={4000} />
    <Field title="Số trên hợp đồng giấy (nếu có)" name="paperNumber" maxLength={100} />
    {['contract', 'handover', 'bills'].map(key => <EvidenceUpload key={key} title={{ contract: 'Hợp đồng giấy có chữ ký', handover: 'Biên bản bàn giao có chữ ký', bills: 'Bill thanh toán nếu có' }[key]} required={key !== 'bills'} value={paper[key]} onChange={value => setPaper(old => ({ ...old, [key]: value }))} />)}
    <EvidenceUpload title="Ảnh lắp đặt" value={images} onChange={setImages} />{action.errorView}
    <Button primary type="submit" disabled={action.busy || !query.data?.length || !paper.contract.length || !paper.handover.length}>{action.busy ? 'Đang gửi…' : 'Gửi báo cáo chờ Admin duyệt'}</Button>
    <small>Chỉ ký giấy. Ngày thuê được tính từ ngày sau ngày hoàn tất bàn giao; Admin duyệt mới tạo hồ sơ chính thức.</small>
  </form></State>
}
